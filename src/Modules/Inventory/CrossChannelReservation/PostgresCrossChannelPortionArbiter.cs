using System.Text.Json;
using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.PortionReservations.CancellationEffects;
using Npgsql;

namespace ALKAROS.Inventory.CrossChannelReservation;

/// <summary>
/// Holds live in <c>inventory.portion_reservations</c> (status Reserved) and in
/// <c>stock_balances.reserved_quantity</c>, the same shape V11-RSV-002's single-line arbitrator
/// writes, so V11-INV-007's projection rebuild/drift check and V11-RSV-003's cancellation
/// decision work on them unchanged. What this adds: several lines decided together in the
/// caller's transaction, and the same per-row advisory lock order acceptance's consumption
/// takes (<see cref="IStockBalanceRepository.AcquireOnHandLockAsync"/>), acquired in the same
/// global (stock item, location) order, so a hold and a direct sale of the same portion are
/// serialized. The order holds within one call; a caller that goes on to lock further rows in the
/// same transaction (a consumption touching modifier stock) must lock that whole set first, in the
/// same order, before calling here — as QR acceptance does (V12-RMD-003).
/// </summary>
public sealed class PostgresCrossChannelPortionArbiter : ICrossChannelPortionArbiter
{
    private const string IdempotencyPrefix = "ccr:";
    private const int MaxChannelOrderReferenceLength = 200;
    private const int MaxHoldsPerOrder = 1000;

    private readonly NpgsqlDataSource _dataSource;
    private readonly IStockBalanceRepository _balances;
    private readonly IPortionCancellationDecisionService _cancellation;

    public PostgresCrossChannelPortionArbiter(
        NpgsqlDataSource dataSource,
        IStockBalanceRepository balances,
        IPortionCancellationDecisionService cancellation)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _balances = balances ?? throw new ArgumentNullException(nameof(balances));
        _cancellation = cancellation ?? throw new ArgumentNullException(nameof(cancellation));
    }

    public async Task<CrossChannelReservationResult> ReserveAsync(
        CrossChannelReservationRequest request,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        Validate(request);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        var mappings = await LoadMappingsAsync(request, connection, transaction, cancellationToken).ConfigureAwait(false);

        var unconfigured = new List<UnconfiguredLine>();
        var planned = new List<PlannedHold>();
        foreach (var line in request.Lines)
        {
            var lineMappings = mappings.Where(m => m.ProductId == line.ProductId).ToList();
            if (lineMappings.Count == 0)
            {
                unconfigured.Add(new UnconfiguredLine(line.OrderItemId, line.ProductId, StockConfigurationGap.ProductHasNoStockMapping));
                continue;
            }

            foreach (var mapping in lineMappings)
            {
                if (mapping.DefaultLocationId is not { } locationId)
                {
                    unconfigured.Add(new UnconfiguredLine(line.OrderItemId, line.ProductId, StockConfigurationGap.StockItemHasNoDefaultLocation));
                    continue;
                }

                planned.Add(new PlannedHold(
                    line.OrderItemId,
                    mapping.StockItemId,
                    locationId,
                    line.Quantity * mapping.QuantityMultiplier,
                    mapping.TrackingUnitCode.Trim().ToLowerInvariant()));
            }
        }

        if (unconfigured.Count > 0)
            return CrossChannelReservationResult.NotConfigured(unconfigured);

        var required = planned
            .GroupBy(h => (h.StockItemId, h.StockLocationId))
            .Select(g => (Pair: g.Key, Quantity: g.Sum(h => h.Quantity), OrderItemIds: g.Select(h => h.OrderItemId).Distinct().ToList()))
            .OrderBy(r => r.Pair.StockItemId)
            .ThenBy(r => r.Pair.StockLocationId)
            .ToList();

        foreach (var (pair, _, _) in required)
        {
            await _balances.AcquireOnHandLockAsync(pair.StockItemId, pair.StockLocationId, connection, transaction, cancellationToken)
                .ConfigureAwait(false);
        }

        // Checked only once every row lock is held: a concurrent identical request has
        // either committed its holds already (and is visible now) or is still waiting.
        var existing = await LoadExistingHoldsAsync(request.OrderId, connection, transaction, cancellationToken).ConfigureAwait(false);
        if (existing.Count > 0)
        {
            if (!IsSameRequest(existing, planned))
                throw new CrossChannelReservationConflictException(request.OrderId);
            // Held again only while every hold is still Reserved; once consumed, a repeat must never read as
            // "held" or its caller would consume the portion a second time (V12-RMD-003).
            return existing.All(h => h.Status == "Reserved")
                ? CrossChannelReservationResult.Held(CrossChannelReservationOutcome.Replayed, existing)
                : CrossChannelReservationResult.AlreadyConsumed();
        }

        var available = await LoadAvailableAsync(required.Select(r => r.Pair).ToList(), connection, transaction, cancellationToken)
            .ConfigureAwait(false);
        var shortages = required
            .Select(r => new StockShortage(
                r.Pair.StockItemId,
                r.Pair.StockLocationId,
                r.Quantity,
                available.TryGetValue(r.Pair, out var quantity) ? quantity : 0m,
                r.OrderItemIds))
            .Where(s => s.AvailableQuantity < s.RequiredQuantity)
            .ToList();
        if (shortages.Count > 0)
            return CrossChannelReservationResult.OutOfStock(shortages);

        foreach (var (pair, quantity, _) in required)
        {
            await HoldBalanceAsync(pair.StockItemId, pair.StockLocationId, quantity, connection, transaction, cancellationToken)
                .ConfigureAwait(false);
        }

        var metadata = JsonSerializer.Serialize(new
        {
            channel = request.Channel.ToString(),
            channelOrderReference = request.ChannelOrderReference.Trim()
        });
        var now = DateTimeOffset.UtcNow;
        var holds = new List<CrossChannelHold>(planned.Count);
        foreach (var hold in planned)
        {
            var reservationId = Guid.NewGuid();
            await InsertHoldAsync(reservationId, request, hold, metadata, now, connection, transaction, cancellationToken)
                .ConfigureAwait(false);
            holds.Add(new CrossChannelHold(
                reservationId, hold.OrderItemId, hold.StockItemId, hold.StockLocationId, hold.Quantity, hold.UnitCode, "Reserved"));
        }

        return CrossChannelReservationResult.Held(CrossChannelReservationOutcome.Reserved, holds);
    }

    public async Task<IReadOnlyList<CancellationDecisionResult>> CompensateAsync(
        Guid orderId,
        Guid actorId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var results = await CompensateAsync(orderId, actorId, reason, connection, transaction, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return results;
    }

    public async Task<IReadOnlyList<CancellationDecisionResult>> CompensateAsync(
        Guid orderId,
        Guid actorId,
        string reason,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        if (orderId == Guid.Empty)
            throw new InvalidCrossChannelReservationException("OrderId cannot be empty.");
        if (actorId == Guid.Empty)
            throw new InvalidCrossChannelReservationException("ActorId cannot be empty.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidCrossChannelReservationException("A compensation reason is required.");

        const string sql = @"
            SELECT id, order_item_id, stock_item_id, stock_location_id
            FROM inventory.portion_reservations
            WHERE order_id = $1 AND idempotency_key LIKE 'ccr:%' AND status <> 'Consumed'
            ORDER BY id
            LIMIT $2;";

        var targets = new List<(Guid ReservationId, Guid OrderItemId, Guid StockItemId, Guid StockLocationId)>();
        await using (var cmd = new NpgsqlCommand(sql, connection, transaction))
        {
            cmd.Parameters.AddWithValue(orderId);
            cmd.Parameters.AddWithValue(MaxHoldsPerOrder + 1);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                targets.Add((reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3)));
        }

        if (targets.Count > MaxHoldsPerOrder)
            throw new InvalidOperationException(
                $"Order '{orderId}' has more than {MaxHoldsPerOrder} holds; refusing a partial compensation.");

        // The same lock order as ReserveAsync (stock item, then location, compared as Guids), so a compensation and
        // a reservation touching the same rows can never wait on each other in a cycle.
        foreach (var pair in targets
                     .Select(t => (t.StockItemId, t.StockLocationId))
                     .Distinct()
                     .OrderBy(p => p.StockItemId)
                     .ThenBy(p => p.StockLocationId))
        {
            await _balances.AcquireOnHandLockAsync(pair.StockItemId, pair.StockLocationId, connection, transaction, cancellationToken)
                .ConfigureAwait(false);
        }

        var results = new List<CancellationDecisionResult>(targets.Count);
        foreach (var (reservationId, orderItemId, _, _) in targets)
        {
            var decision = await _cancellation.ProcessCancellationAsync(
                new ProcessCancellationCommand(
                    reservationId,
                    orderItemId,
                    actorId,
                    reason.Trim(),
                    IdempotencyKey: $"ccr-comp:{reservationId:N}"),
                connection,
                transaction,
                cancellationToken).ConfigureAwait(false);
            results.Add(decision);
        }

        return results;
    }

    private static void Validate(CrossChannelReservationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.IsDefined(request.Channel))
            throw new InvalidCrossChannelReservationException($"Unknown channel '{request.Channel}'.");
        if (string.IsNullOrWhiteSpace(request.ChannelOrderReference))
            throw new InvalidCrossChannelReservationException("ChannelOrderReference cannot be empty.");
        if (request.ChannelOrderReference.Trim().Length > MaxChannelOrderReferenceLength)
            throw new InvalidCrossChannelReservationException(
                $"ChannelOrderReference cannot exceed {MaxChannelOrderReferenceLength} characters.");
        if (request.OrderId == Guid.Empty)
            throw new InvalidCrossChannelReservationException("OrderId cannot be empty.");
        if (request.ActorId == Guid.Empty)
            throw new InvalidCrossChannelReservationException("ActorId cannot be empty.");
        if (request.Lines is null || request.Lines.Count == 0)
            throw new InvalidCrossChannelReservationException("At least one line is required.");

        var seen = new HashSet<Guid>();
        foreach (var line in request.Lines)
        {
            if (line is null)
                throw new InvalidCrossChannelReservationException("Lines cannot contain null.");
            if (line.OrderItemId == Guid.Empty)
                throw new InvalidCrossChannelReservationException("OrderItemId cannot be empty.");
            if (line.ProductId == Guid.Empty)
                throw new InvalidCrossChannelReservationException("ProductId cannot be empty.");
            if (line.Quantity <= 0m)
                throw new InvalidCrossChannelReservationException($"Line quantity must be strictly positive, got {line.Quantity}.");
            if (!seen.Add(line.OrderItemId))
                throw new InvalidCrossChannelReservationException($"OrderItemId '{line.OrderItemId}' appears more than once.");
        }
    }

    private static bool IsSameRequest(IReadOnlyList<CrossChannelHold> existing, IReadOnlyList<PlannedHold> planned)
    {
        if (existing.Any(h => h.Status is not ("Reserved" or "Consumed")))
            return false;

        static string Key(Guid orderItemId, Guid stockItemId, decimal quantity) =>
            $"{orderItemId:N}|{stockItemId:N}|{quantity.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)}";

        var stored = existing.Select(h => Key(h.OrderItemId, h.StockItemId, h.Quantity)).Order(StringComparer.Ordinal);
        var requested = planned.Select(h => Key(h.OrderItemId, h.StockItemId, h.Quantity)).Order(StringComparer.Ordinal);
        return stored.SequenceEqual(requested, StringComparer.Ordinal);
    }

    private static async Task<IReadOnlyList<StockMapping>> LoadMappingsAsync(
        CrossChannelReservationRequest request,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT m.product_id, m.stock_item_id, m.quantity_multiplier, s.default_location_id, s.tracking_unit_code
            FROM inventory.product_stock_mappings m
            JOIN inventory.stock_items s ON s.id = m.stock_item_id
            WHERE m.product_id = ANY($1)
            ORDER BY m.product_id, m.stock_item_id;";

        await using var cmd = new NpgsqlCommand(sql, connection, transaction);
        cmd.Parameters.AddWithValue(request.Lines.Select(l => l.ProductId).Distinct().ToArray());

        var mappings = new List<StockMapping>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            mappings.Add(new StockMapping(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetDecimal(2),
                reader.IsDBNull(3) ? null : reader.GetGuid(3),
                reader.GetString(4)));
        }

        return mappings;
    }

    private static async Task<IReadOnlyList<CrossChannelHold>> LoadExistingHoldsAsync(
        Guid orderId,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT id, order_item_id, stock_item_id, stock_location_id, quantity, unit_code, status
            FROM inventory.portion_reservations
            WHERE order_id = $1 AND idempotency_key LIKE 'ccr:%'
            ORDER BY id;";

        await using var cmd = new NpgsqlCommand(sql, connection, transaction);
        cmd.Parameters.AddWithValue(orderId);

        var holds = new List<CrossChannelHold>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            holds.Add(new CrossChannelHold(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.GetGuid(3),
                reader.GetDecimal(4),
                reader.GetString(5),
                reader.GetString(6)));
        }

        return holds;
    }

    private static async Task<Dictionary<(Guid StockItemId, Guid StockLocationId), decimal>> LoadAvailableAsync(
        IReadOnlyList<(Guid StockItemId, Guid StockLocationId)> pairs,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT b.stock_item_id, b.stock_location_id, b.available_quantity
            FROM inventory.stock_balances b
            JOIN unnest($1::uuid[], $2::uuid[]) AS p(stock_item_id, stock_location_id)
              ON p.stock_item_id = b.stock_item_id AND p.stock_location_id = b.stock_location_id;";

        await using var cmd = new NpgsqlCommand(sql, connection, transaction);
        cmd.Parameters.AddWithValue(pairs.Select(p => p.StockItemId).ToArray());
        cmd.Parameters.AddWithValue(pairs.Select(p => p.StockLocationId).ToArray());

        var available = new Dictionary<(Guid, Guid), decimal>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            available[(reader.GetGuid(0), reader.GetGuid(1))] = reader.GetDecimal(2);

        return available;
    }

    private static async Task HoldBalanceAsync(
        Guid stockItemId,
        Guid stockLocationId,
        decimal quantity,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            UPDATE inventory.stock_balances
            SET reserved_quantity = reserved_quantity + $3,
                available_quantity = on_hand_quantity - (reserved_quantity + $3),
                updated_at = NOW(),
                row_version = row_version + 1
            WHERE stock_item_id = $1 AND stock_location_id = $2;";

        await using var cmd = new NpgsqlCommand(sql, connection, transaction);
        cmd.Parameters.AddWithValue(stockItemId);
        cmd.Parameters.AddWithValue(stockLocationId);
        cmd.Parameters.AddWithValue(quantity);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertHoldAsync(
        Guid reservationId,
        CrossChannelReservationRequest request,
        PlannedHold hold,
        string metadata,
        DateTimeOffset now,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string reservationSql = @"
            INSERT INTO inventory.portion_reservations (
                id, order_id, order_item_id, stock_item_id, stock_location_id,
                quantity, unit_code, status, version, idempotency_key,
                reserved_at, created_by, metadata
            ) VALUES ($1, $2, $3, $4, $5, $6, $7, 'Reserved', 1, $8, $9, $10, $11::jsonb);";

        await using (var cmd = new NpgsqlCommand(reservationSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue(reservationId);
            cmd.Parameters.AddWithValue(request.OrderId);
            cmd.Parameters.AddWithValue(hold.OrderItemId);
            cmd.Parameters.AddWithValue(hold.StockItemId);
            cmd.Parameters.AddWithValue(hold.StockLocationId);
            cmd.Parameters.AddWithValue(hold.Quantity);
            cmd.Parameters.AddWithValue(hold.UnitCode);
            cmd.Parameters.AddWithValue($"{IdempotencyPrefix}{request.OrderId:N}:{hold.OrderItemId:N}:{hold.StockItemId:N}");
            cmd.Parameters.AddWithValue(now);
            cmd.Parameters.AddWithValue(request.ActorId);
            cmd.Parameters.AddWithValue(metadata);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // Same projection-idempotency row that V11-RSV-002 writes, so V11-INV-007's
        // rebuild and V11-RSV-003's terminal transition see this hold exactly once.
        const string appliedEventSql = @"
            INSERT INTO inventory.reservation_balance_applied_events (
                id, reservation_id, event_type, terminal_status, stock_item_id, stock_location_id, quantity, applied_at
            ) VALUES ($1, $2, 'Reserved', NULL, $3, $4, $5, $6);";

        await using var eventCmd = new NpgsqlCommand(appliedEventSql, connection, transaction);
        eventCmd.Parameters.AddWithValue(Guid.NewGuid());
        eventCmd.Parameters.AddWithValue(reservationId);
        eventCmd.Parameters.AddWithValue(hold.StockItemId);
        eventCmd.Parameters.AddWithValue(hold.StockLocationId);
        eventCmd.Parameters.AddWithValue(hold.Quantity);
        eventCmd.Parameters.AddWithValue(now);
        await eventCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed record StockMapping(
        Guid ProductId,
        Guid StockItemId,
        decimal QuantityMultiplier,
        Guid? DefaultLocationId,
        string TrackingUnitCode);

    private sealed record PlannedHold(
        Guid OrderItemId,
        Guid StockItemId,
        Guid StockLocationId,
        decimal Quantity,
        string UnitCode);
}
