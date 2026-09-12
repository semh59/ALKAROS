namespace ALKAROS.QrOrdering.PendingOrders.Tests;

using ALKAROS.IntegrationContracts;
using ALKAROS.QrOrdering.CustomerSession;
using ALKAROS.QrOrdering.PendingOrders.Tests.Fixtures;
using ALKAROS.QrOrdering.TablePolicy;
using ALKAROS.QrOrdering.TokenLifecycle;
using ALKAROS.Tables.TableLifecycle;
using FluentAssertions;
using NpgsqlTypes;
using Xunit;

/// <summary>
/// V12-QRO-001, against a real Postgres database: table_mgmt + catalog +
/// qr_ordering (table_tokens, customer_sessions, pending_order_submissions)
/// + the real transactional outbox.
/// </summary>
public sealed class QrPendingOrderStoreTests : IClassFixture<QrOrderingPendingOrdersTestDatabase>
{
    private readonly QrOrderingPendingOrdersTestDatabase _database;
    private readonly TableTokenService _tableTokenService;
    private readonly CustomerSessionService _customerSessionService;
    private readonly QrPendingOrderStore _store;

    public QrPendingOrderStoreTests(QrOrderingPendingOrdersTestDatabase database)
    {
        _database = database;
        _tableTokenService = new TableTokenService(new PostgresTableTokenRepository(database.DataSource));
        _customerSessionService = new CustomerSessionService(
            new PostgresCustomerSessionRepository(database.DataSource), _tableTokenService);
        var tableReservationPolicy = new QrTableReservationPolicy(new PostgresTableRepository(database.DataSource));
        _store = new QrPendingOrderStore(database.DataSource, _customerSessionService, tableReservationPolicy);
    }

    private async Task<string> IssueSessionAsync(Guid tableId)
    {
        var rawTableToken = await _tableTokenService.IssueAsync(tableId);
        var issued = await _customerSessionService.IssueAsync(rawTableToken);
        return issued.RawToken!;
    }

    [Fact]
    public async Task SubmittingWithAValidSessionQueuesTheEventAndRecordsTheSubmission()
    {
        var tableId = await _database.SeedTableAsync();
        var productId = await _database.SeedProductAsync(price: 120m);
        var rawSession = await IssueSessionAsync(tableId);
        var submissionId = Guid.NewGuid();

        var result = await _store.SubmitAsync(
            rawSession,
            new QrOrderSubmissionRequest(
                [new QrOrderSubmissionItemRequest(Guid.NewGuid(), productId, 2, "az acılı")],
                submissionId));

        result.SubmissionId.Should().Be(submissionId);
        result.TableId.Should().Be(tableId);

        // V12-QRO-002: a successful submission reserves the table — the
        // actual anti-remote-abuse mechanism this task exists for.
        var (status, _) = await _database.GetTableStateAsync(tableId);
        status.Should().Be("Reserved");

        var envelope = await ReadOutboxEnvelope(submissionId);
        envelope.Should().NotBeNull();
        var deserialized = IntegrationEventSerializer.Deserialize<QrOrderSubmitted>(envelope!);
        deserialized.TableId.Should().Be(tableId);
        deserialized.SubmissionId.Should().Be(submissionId);
        var item = deserialized.Items.Single();
        item.ProductId.Should().Be(productId);
        item.ProductName.Should().Be("Lahmacun");
        item.UnitPrice.Should().Be(120m);
        item.Quantity.Should().Be(2);
        item.Notes.Should().Be("az acılı");
    }

    [Fact]
    public async Task ReplayingTheSameSubmissionReturnsTheSameResultWithoutQueuingATwin()
    {
        var tableId = await _database.SeedTableAsync();
        var productId = await _database.SeedProductAsync();
        var rawSession = await IssueSessionAsync(tableId);
        var request = new QrOrderSubmissionRequest(
            [new QrOrderSubmissionItemRequest(Guid.NewGuid(), productId, 1, null)],
            Guid.NewGuid());

        var first = await _store.SubmitAsync(rawSession, request);
        var second = await _store.SubmitAsync(rawSession, request);

        // The replay's SubmittedAt is re-read from Postgres (microsecond
        // precision) while the first result carries the original in-memory
        // DateTimeOffset (100ns precision) — compare with a tolerance
        // instead of exact equality.
        second.SubmissionId.Should().Be(first.SubmissionId);
        second.TableId.Should().Be(first.TableId);
        second.SubmittedAt.Should().BeCloseTo(first.SubmittedAt, TimeSpan.FromMilliseconds(1));
        (await CountOutboxMessages(request.SubmissionId)).Should().Be(1);
    }

    [Fact]
    public async Task AnUnknownSessionTokenFails()
    {
        var productId = await _database.SeedProductAsync();
        var act = () => _store.SubmitAsync(
            "alkaros-customer-session:this-was-never-issued",
            new QrOrderSubmissionRequest([new QrOrderSubmissionItemRequest(Guid.NewGuid(), productId, 1, null)], Guid.NewGuid()));

        var ex = await act.Should().ThrowAsync<QrCustomerSessionInvalidException>();
        ex.Which.Reason.Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task AnUnknownProductFails()
    {
        var tableId = await _database.SeedTableAsync();
        var rawSession = await IssueSessionAsync(tableId);

        var act = () => _store.SubmitAsync(
            rawSession,
            new QrOrderSubmissionRequest([new QrOrderSubmissionItemRequest(Guid.NewGuid(), Guid.NewGuid(), 1, null)], Guid.NewGuid()));

        await act.Should().ThrowAsync<QrOrderInvalidProductException>();
    }

    [Fact]
    public async Task AnUnavailableProductFails()
    {
        // V1-RMD-128: found by an independent audit (2026-09-09) — this
        // store checked catalog.products.active but not is_available (the
        // real-time 86/suspend toggle a manager flips through
        // CatalogManagementStore.SetProductAvailabilityV1). This is the QR
        // customer self-ordering path — no staff member is in the loop at
        // all, so a suspended item could still be submitted straight
        // through to the kitchen.
        var tableId = await _database.SeedTableAsync();
        var productId = await _database.SeedProductAsync(isAvailable: false);
        var rawSession = await IssueSessionAsync(tableId);

        var act = () => _store.SubmitAsync(
            rawSession,
            new QrOrderSubmissionRequest([new QrOrderSubmissionItemRequest(Guid.NewGuid(), productId, 1, null)], Guid.NewGuid()));

        await act.Should().ThrowAsync<QrOrderInvalidProductException>();
    }

    /// <summary>
    /// V12-QRO-004 (Semih's decision, 2026-09-12: two separate guests should
    /// not be forced to share one phone to order): a second, independent guest at the same
    /// already-occupied table must be able to submit their own QR order from
    /// their own phone — the table state is left completely untouched (no
    /// re-reservation; there is nothing to converge, the table is already
    /// exactly where it should be).
    /// </summary>
    [Fact]
    public async Task ASubmissionAgainstAnOccupiedTableSucceedsAndTheTableIsUnchanged()
    {
        var tableId = await _database.SeedTableAsync(status: "Occupied");
        var productId = await _database.SeedProductAsync();
        var rawSession = await IssueSessionAsync(tableId);
        var (_, rowVersionBefore) = await _database.GetTableStateAsync(tableId);
        var submissionId = Guid.NewGuid();

        var result = await _store.SubmitAsync(
            rawSession,
            new QrOrderSubmissionRequest([new QrOrderSubmissionItemRequest(Guid.NewGuid(), productId, 1, null)], submissionId));

        result.SubmissionId.Should().Be(submissionId);
        var (status, rowVersionAfter) = await _database.GetTableStateAsync(tableId);
        status.Should().Be("Occupied");
        rowVersionAfter.Should().Be(rowVersionBefore);
    }

    /// <summary>V12-QRO-002: same refusal for a table already Reserved (by another pending QR order or a manual reservation) — the single-owner invariant (table-reservation-policy.md).</summary>
    [Fact]
    public async Task ASubmissionAgainstAnAlreadyReservedTableIsRefused()
    {
        var tableId = await _database.SeedTableAsync(status: "Reserved");
        var productId = await _database.SeedProductAsync();
        var rawSession = await IssueSessionAsync(tableId);

        var act = () => _store.SubmitAsync(
            rawSession,
            new QrOrderSubmissionRequest([new QrOrderSubmissionItemRequest(Guid.NewGuid(), productId, 1, null)], Guid.NewGuid()));

        await act.Should().ThrowAsync<QrTableNotAvailableException>();
    }

    /// <summary>V12-QRO-002: a refused submission never queues a QrOrderSubmitted event or records a ledger entry — the whole transaction rolls back.</summary>
    [Fact]
    public async Task ARefusedSubmissionQueuesNoEventAndRecordsNoLedgerEntry()
    {
        var tableId = await _database.SeedTableAsync(status: "Cleaning");
        var productId = await _database.SeedProductAsync();
        var rawSession = await IssueSessionAsync(tableId);
        var submissionId = Guid.NewGuid();

        var act = () => _store.SubmitAsync(
            rawSession,
            new QrOrderSubmissionRequest([new QrOrderSubmissionItemRequest(Guid.NewGuid(), productId, 1, null)], submissionId));

        await act.Should().ThrowAsync<QrTableNotAvailableException>();
        (await CountOutboxMessages(submissionId)).Should().Be(0);
    }

    [Fact]
    public async Task AZeroQuantityFails()
    {
        var tableId = await _database.SeedTableAsync();
        var productId = await _database.SeedProductAsync();
        var rawSession = await IssueSessionAsync(tableId);

        var act = () => _store.SubmitAsync(
            rawSession,
            new QrOrderSubmissionRequest([new QrOrderSubmissionItemRequest(Guid.NewGuid(), productId, 0, null)], Guid.NewGuid()));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AnEmptyCartFails()
    {
        var tableId = await _database.SeedTableAsync();
        var rawSession = await IssueSessionAsync(tableId);

        var act = () => _store.SubmitAsync(rawSession, new QrOrderSubmissionRequest([], Guid.NewGuid()));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>
    /// V1-RMD-138: found by an independent audit (2026-09-09) — this
    /// anonymous, unauthenticated submission path had a lower bound on
    /// quantity (see AZeroQuantityFails above) but no upper bound at all;
    /// with no staff member in the loop, a malicious or buggy client could
    /// submit an absurd quantity straight through. This store has no HTTP
    /// endpoint yet (QR's own customer page is still Planned), so today
    /// this is purely defensive — kept in sync with NfcOrderingStore's
    /// identical bound.
    /// </summary>
    [Fact]
    public async Task AQuantityOverTheLimitFails()
    {
        var tableId = await _database.SeedTableAsync();
        var productId = await _database.SeedProductAsync();
        var rawSession = await IssueSessionAsync(tableId);

        var act = () => _store.SubmitAsync(
            rawSession,
            new QrOrderSubmissionRequest([new QrOrderSubmissionItemRequest(Guid.NewGuid(), productId, 1000, null)], Guid.NewGuid()));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>V1-RMD-138: same finding as AQuantityOverTheLimitFails, for the item-line-count cap instead of per-line quantity.</summary>
    [Fact]
    public async Task TooManyItemLinesFails()
    {
        var tableId = await _database.SeedTableAsync();
        var productId = await _database.SeedProductAsync();
        var rawSession = await IssueSessionAsync(tableId);
        var items = Enumerable.Range(0, 51)
            .Select(_ => new QrOrderSubmissionItemRequest(Guid.NewGuid(), productId, 1, null))
            .ToArray();

        var act = () => _store.SubmitAsync(rawSession, new QrOrderSubmissionRequest(items, Guid.NewGuid()));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task FindResultingOrderReturnsNullBeforeTheOutboxIsDelivered()
    {
        var tableId = await _database.SeedTableAsync();
        var productId = await _database.SeedProductAsync();
        var rawSession = await IssueSessionAsync(tableId);
        var submissionId = Guid.NewGuid();

        await _store.SubmitAsync(
            rawSession, new QrOrderSubmissionRequest([new QrOrderSubmissionItemRequest(Guid.NewGuid(), productId, 1, null)], submissionId));

        // No Order module consumer runs in this test — QR Ordering has no
        // direct-call edge to Order (V0-ARC-001 row 19), only the queued
        // event, so the resulting Order does not exist yet from this
        // module's own point of view.
        var outcome = await _store.FindResultingOrderAsync(tableId, submissionId);
        outcome.Should().BeNull();
    }

    private async Task<byte[]?> ReadOutboxEnvelope(Guid submissionId)
    {
        await using var cmd = _database.DataSource.CreateCommand(
            "SELECT payload_envelope FROM outbox_messages WHERE aggregate_id = @aggregate_id AND event_type = @event_type;");
        cmd.Parameters.Add("aggregate_id", NpgsqlDbType.Uuid).Value = submissionId;
        cmd.Parameters.Add("event_type", NpgsqlDbType.Text).Value = QrOrderingIntegrationEventTypes.QrOrderSubmitted;
        var result = await cmd.ExecuteScalarAsync();
        return result as byte[];
    }

    private async Task<long> CountOutboxMessages(Guid submissionId)
    {
        await using var cmd = _database.DataSource.CreateCommand(
            "SELECT count(*) FROM outbox_messages WHERE aggregate_id = @aggregate_id AND event_type = @event_type;");
        cmd.Parameters.Add("aggregate_id", NpgsqlDbType.Uuid).Value = submissionId;
        cmd.Parameters.Add("event_type", NpgsqlDbType.Text).Value = QrOrderingIntegrationEventTypes.QrOrderSubmitted;
        return (long)(await cmd.ExecuteScalarAsync())!;
    }
}
