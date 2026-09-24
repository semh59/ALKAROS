using System.Data;
using System.Text.Json;
using ALKAROS.IntegrationContracts;
using ALKAROS.Messaging;
using ALKAROS.QrOrdering.CustomerSession;
using ALKAROS.QrOrdering.TablePolicy;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.QrOrdering.PendingOrders;

/// <summary>
/// V12-QRO-001. Converts an authenticated QR submission (a validated
/// customer session, V12-QRS-003) into a durably queued
/// <see cref="QrOrderSubmitted"/> integration event. QR Ordering has no
/// direct-call edge to Order (V0-ARC-001 row 19: only Identity and Table
/// Management are approved direct-call edges) — Order's own
/// <c>QrOrderSubmittedConsumer</c> materializes the actual
/// <c>orders.orders</c> row asynchronously when the outbox delivers this
/// event. Price/name snapshot is taken here, from <c>catalog.products</c>,
/// at submission time (a customer-facing request is never trusted for
/// pricing) and carried in the event payload so the consumer never needs to
/// reach back into Catalog's schema. Idempotent: replaying the same
/// <see cref="QrOrderSubmissionRequest.SubmissionId"/> returns the original
/// result without re-publishing.
/// </summary>
public sealed class QrPendingOrderStore
{
    // V1-RMD-138: found by an independent audit (2026-09-09) — same gap as
    // NfcOrderingStore (no upper bound on either a line's quantity or how
    // many item lines a single submission could carry, only the lower
    // bound below). This store has no HTTP endpoint yet (QR's own customer
    // page is still Planned — 2026-09-09 audit finding #0), so today this
    // is purely defensive; kept in sync with NfcOrderingStore's exact
    // bounds so the two anonymous ordering channels don't drift apart.
    private const int MaxQuantityPerItem = 999;
    private const int MaxItemsPerSubmission = 50;
    private const int MaxSpecialInstructionsLength = 200;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly NpgsqlDataSource _dataSource;
    private readonly CustomerSessionService _customerSessionService;
    private readonly QrTableReservationPolicy _tableReservationPolicy;

    public QrPendingOrderStore(
        NpgsqlDataSource dataSource,
        CustomerSessionService customerSessionService,
        QrTableReservationPolicy tableReservationPolicy)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _customerSessionService = customerSessionService ?? throw new ArgumentNullException(nameof(customerSessionService));
        _tableReservationPolicy = tableReservationPolicy ?? throw new ArgumentNullException(nameof(tableReservationPolicy));
    }

    public async Task<QrOrderSubmissionResult> SubmitAsync(
        string rawSessionToken, QrOrderSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Items.Count == 0)
            throw new ArgumentException("Order items cannot be empty.", nameof(request));
        if (request.Items.Count > MaxItemsPerSubmission)
            throw new ArgumentException($"Order cannot contain more than {MaxItemsPerSubmission} item lines.", nameof(request));

        var session = await _customerSessionService.ValidateAsync(rawSessionToken, cancellationToken);
        if (!session.IsValid)
            throw new QrCustomerSessionInvalidException(session.FailureReason!);

        var tableId = session.TableId!.Value;
        var sessionId = session.SessionId!.Value;

        await using (var connection = await _dataSource.OpenConnectionAsync(cancellationToken))
        await using (var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken))
        {
            var existing = await FindSubmissionAsync(connection, transaction, request.SubmissionId, cancellationToken);
            if (existing is { } replay)
            {
                await transaction.CommitAsync(cancellationToken);
                return replay;
            }

            var catalog = await ResolveCatalogProductsAsync(
                connection, transaction, request.Items.Select(i => i.ProductId), cancellationToken);

            var items = new List<QrOrderSubmittedItem>();
            foreach (var line in request.Items)
            {
                if (line.Quantity <= 0)
                    throw new ArgumentException($"Quantity for product {line.ProductId} must be positive.", nameof(request));
                if (line.Quantity > MaxQuantityPerItem)
                    throw new ArgumentException(
                        $"Quantity for product {line.ProductId} cannot exceed {MaxQuantityPerItem}.", nameof(request));
                if (line.SpecialInstructions is { Length: > MaxSpecialInstructionsLength })
                    throw new ArgumentException(
                        $"Special instructions cannot exceed {MaxSpecialInstructionsLength} characters.", nameof(request));
                if (!catalog.TryGetValue(line.ProductId, out var product))
                    throw new QrOrderInvalidProductException(line.ProductId);

                items.Add(new QrOrderSubmittedItem(
                    line.Id, line.ProductId, product.Name, line.Quantity, product.Price, product.TaxRate, line.SpecialInstructions));
            }

            var submittedAt = DateTimeOffset.UtcNow;
            var @event = new QrOrderSubmitted(request.SubmissionId, tableId, sessionId, items, submittedAt);

            try
            {
                // V12-QRO-002: refuses outright (and rolls back below) unless
                // the table is Available — the anti-remote-abuse gate. Runs
                // before the ledger insert/outbox enqueue so a refused table
                // never queues a QrOrderSubmitted event at all.
                await _tableReservationPolicy.ReserveForSubmissionAsync(tableId, connection, transaction, cancellationToken);
                await InsertSubmissionAsync(connection, transaction, request.SubmissionId, tableId, sessionId, items, submittedAt, cancellationToken);
                await OutboxStore.EnqueueAsync(
                    new OutboxEnvelope(
                        QrOrderingIntegrationEventTypes.QrOrderSubmitted,
                        "qr_order_submission",
                        request.SubmissionId,
                        IntegrationEventSerializer.Serialize(@event)),
                    connection,
                    transaction,
                    cancellationToken);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                await transaction.RollbackAsync(cancellationToken);
                var concurrent = await FindSubmissionAsync(request.SubmissionId, cancellationToken);
                if (concurrent is { } found)
                    return found;
                throw;
            }

            await transaction.CommitAsync(cancellationToken);
            return new QrOrderSubmissionResult(request.SubmissionId, tableId, submittedAt);
        }
    }

    /// <summary>
    /// Reads whether the Order for <paramref name="submissionId"/> has been
    /// materialized yet by Order's own consumer — a plain cross-schema read
    /// (always allowed, V0-ARC-001), not a direct-call edge. Returns null
    /// while the outbox delivery is still pending.
    /// </summary>
    public async Task<QrOrderSubmissionOutcome?> FindResultingOrderAsync(
        Guid tableId, Guid submissionId, CancellationToken cancellationToken = default)
    {
        await using var cmd = _dataSource.CreateCommand(
            """
            SELECT order_id, status
            FROM orders.orders
            WHERE table_id = @table_id AND source_reference_id = @submission_id
            LIMIT 1;
            """);
        cmd.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
        cmd.Parameters.Add("submission_id", NpgsqlDbType.Uuid).Value = submissionId;
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new QrOrderSubmissionOutcome(reader.GetGuid(0), reader.GetString(1));
    }

    private static async Task<QrOrderSubmissionResult?> FindSubmissionAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid submissionId, CancellationToken cancellationToken)
    {
        await using var cmd = new NpgsqlCommand(
            """
            SELECT table_id, submitted_at
            FROM qr_ordering.pending_order_submissions
            WHERE submission_id = @submission_id;
            """, connection, transaction);
        cmd.Parameters.Add("submission_id", NpgsqlDbType.Uuid).Value = submissionId;
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new QrOrderSubmissionResult(submissionId, reader.GetGuid(0), reader.GetFieldValue<DateTimeOffset>(1));
    }

    /// <summary>Same lookup as above, on the store's own connection — used after
    /// a failed transaction has already been rolled back and cannot be reused.</summary>
    private async Task<QrOrderSubmissionResult?> FindSubmissionAsync(Guid submissionId, CancellationToken cancellationToken)
    {
        await using var cmd = _dataSource.CreateCommand(
            """
            SELECT table_id, submitted_at
            FROM qr_ordering.pending_order_submissions
            WHERE submission_id = @submission_id;
            """);
        cmd.Parameters.Add("submission_id", NpgsqlDbType.Uuid).Value = submissionId;
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new QrOrderSubmissionResult(submissionId, reader.GetGuid(0), reader.GetFieldValue<DateTimeOffset>(1));
    }

    private static async Task InsertSubmissionAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid submissionId, Guid tableId, Guid sessionId,
        IReadOnlyList<QrOrderSubmittedItem> items, DateTimeOffset submittedAt, CancellationToken cancellationToken)
    {
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO qr_ordering.pending_order_submissions
                (submission_id, table_id, customer_session_id, items_snapshot, submitted_at)
            VALUES (@submission_id, @table_id, @session_id, @items_snapshot, @submitted_at);
            """, connection, transaction);
        cmd.Parameters.Add("submission_id", NpgsqlDbType.Uuid).Value = submissionId;
        cmd.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
        cmd.Parameters.Add("session_id", NpgsqlDbType.Uuid).Value = sessionId;
        cmd.Parameters.Add("items_snapshot", NpgsqlDbType.Jsonb).Value = JsonSerializer.Serialize(items, JsonOptions);
        cmd.Parameters.Add("submitted_at", NpgsqlDbType.TimestampTz).Value = submittedAt;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<Dictionary<Guid, (string Name, decimal Price, decimal TaxRate)>> ResolveCatalogProductsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, IEnumerable<Guid> productIds, CancellationToken cancellationToken)
    {
        var ids = productIds.Distinct().ToArray();
        var result = new Dictionary<Guid, (string, decimal, decimal)>();
        if (ids.Length == 0)
            return result;

        // V1-RMD-128: found by an independent audit (2026-09-09) — this
        // query checked p.active (master-data existence) but not
        // p.is_available (the real-time 86/suspend toggle
        // CatalogManagementStore.SetProductAvailabilityV1 flips), unlike
        // the terminal-wide quick-sale path (DualScreenStore.Orders.cs)
        // which already checks both. This is the QR customer self-ordering
        // path — no staff member is in the loop at all, so a manager
        // marking an item unavailable had zero effect here: a customer
        // could still submit it straight through to the kitchen.
        await using var cmd = new NpgsqlCommand(
            """
            SELECT p.product_id, p.name, p.current_price, COALESCE(t.vat_rate, 0)
            FROM catalog.products p
            LEFT JOIN catalog.tax_profiles t ON t.tax_profile_id = p.tax_profile_id AND t.active
            WHERE p.product_id = ANY(@product_ids) AND p.active AND p.is_available AND p.current_price IS NOT NULL;
            """, connection, transaction);
        cmd.Parameters.AddWithValue("product_ids", ids);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result[reader.GetGuid(0)] = (reader.GetString(1), reader.GetDecimal(2), reader.GetDecimal(3));
        }

        return result;
    }
}

/// <summary>V12-QRO-001: the raw customer session token failed validation (unknown/revoked/expired).</summary>
public sealed class QrCustomerSessionInvalidException : Exception
{
    public string Reason { get; }

    public QrCustomerSessionInvalidException(string reason)
        : base($"Customer session is invalid: {reason}.")
    {
        Reason = reason;
    }
}

/// <summary>V12-QRO-001: a requested product does not exist, is inactive, is not available, or has no current price.</summary>
public sealed class QrOrderInvalidProductException : Exception
{
    public Guid ProductId { get; }

    public QrOrderInvalidProductException(Guid productId)
        : base($"Product {productId} was not found, is not available, or has no active price.")
    {
        ProductId = productId;
    }
}
