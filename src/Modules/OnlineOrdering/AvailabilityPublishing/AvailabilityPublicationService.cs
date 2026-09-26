using Npgsql;

namespace ALKAROS.OnlineOrdering.AvailabilityPublishing;

/// <summary>
/// V12-ONL-005: keeps every enabled online channel told how many units of each product it knows can
/// be sold. The single approved source is V11-INV-007's reserved/available balance projection
/// (<c>inventory.stock_balances.available_quantity</c>, read across schemas — V0-ARC-001), so a
/// cross-channel hold (V12-STK-001) lowers what the channels see at once. A product's units are the
/// fewest of its mapped stock items, each divided by its multiplier.
///
/// Ordering: each observation carries the product's source version (the sum of its balance rows'
/// row versions, which only grows), and a state is replaced only by a strictly newer observation, so
/// a delayed old observation never overwrites a newer state. A delivery locks the rows it sends
/// (<c>FOR UPDATE SKIP LOCKED</c>) until it records them as delivered: a newer observation of the same
/// product waits for that and is then sent by the next pass, and parallel passes never send a product
/// twice. Throttling: one pass sends at most one batched provider call per channel.
/// </summary>
public sealed class AvailabilityPublicationService
{
    private const int MaxProducts = 2000;

    private readonly NpgsqlDataSource _dataSource;
    private readonly IReadOnlyList<IAvailabilityChannelPublisher> _channels;

    public AvailabilityPublicationService(NpgsqlDataSource dataSource, IEnumerable<IAvailabilityChannelPublisher> channels)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _channels = (channels ?? throw new ArgumentNullException(nameof(channels))).ToList();
    }

    /// <summary>Refreshes and delivers every enabled channel once. A disabled channel is skipped entirely.</summary>
    public async Task<AvailabilityPassResult> RunPassAsync(CancellationToken cancellationToken = default)
    {
        var changed = 0;
        var delivered = 0;
        foreach (var channel in _channels.Where(c => c.IsEnabled))
        {
            changed += await RefreshAsync(channel, cancellationToken).ConfigureAwait(false);
            delivered += await DeliverAsync(channel, cancellationToken).ConfigureAwait(false);
        }

        return new AvailabilityPassResult(changed, delivered);
    }

    /// <summary>Records the current availability of every product the channel knows; returns how many quantities changed.</summary>
    public async Task<int> RefreshAsync(IAvailabilityChannelPublisher channel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        var products = await channel.PublishedProductsAsync(MaxProducts + 1, cancellationToken).ConfigureAwait(false);
        if (products.Count > MaxProducts)
            throw new InvalidOperationException($"Channel '{channel.Channel}' knows more than {MaxProducts} products; refusing a partial refresh.");
        if (products.Count == 0)
            return 0;

        var observed = await ObserveAsync(products.Select(p => p.ProductId).ToArray(), cancellationToken).ConfigureAwait(false);
        var changed = 0;
        foreach (var product in products)
        {
            if (!observed.TryGetValue(product.ProductId, out var observation))
                continue;
            changed += await RecordObservationAsync(
                channel.Channel, product, observation.Quantity, observation.Version, cancellationToken).ConfigureAwait(false);
        }

        return changed;
    }

    /// <summary>
    /// Records one observation unless a newer one is already recorded; returns 1 when the recorded
    /// quantity changed. An observation that arrives late — older than what is already recorded — is
    /// ignored rather than applied.
    /// </summary>
    public async Task<int> RecordObservationAsync(
        string channel, PublishedChannelProduct product, int quantity, long version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(product);
        await using var command = _dataSource.CreateCommand(
            """
            WITH previous AS (
                SELECT desired_quantity FROM online_ordering.availability_states WHERE channel = $1 AND product_id = $2
            ), upserted AS (
                INSERT INTO online_ordering.availability_states (channel, product_id, external_sku, desired_quantity, desired_version)
                VALUES ($1, $2, $3, $4, $5)
                ON CONFLICT (channel, product_id) DO UPDATE
                SET external_sku = EXCLUDED.external_sku,
                    desired_quantity = EXCLUDED.desired_quantity,
                    desired_version = EXCLUDED.desired_version,
                    desired_at = CASE
                        WHEN online_ordering.availability_states.desired_quantity = EXCLUDED.desired_quantity
                            THEN online_ordering.availability_states.desired_at
                        ELSE now() END
                WHERE online_ordering.availability_states.desired_version < EXCLUDED.desired_version
                RETURNING desired_quantity
            )
            SELECT count(*) FROM upserted
            WHERE NOT EXISTS (SELECT 1 FROM previous)
               OR upserted.desired_quantity <> (SELECT desired_quantity FROM previous);
            """);
        command.Parameters.AddWithValue(channel);
        command.Parameters.AddWithValue(product.ProductId);
        command.Parameters.AddWithValue(product.ExternalId);
        command.Parameters.AddWithValue(quantity);
        command.Parameters.AddWithValue(version);
        return (int)(long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
    }

    /// <summary>Sends one batch of changed availability to the channel; returns how many products it carried.</summary>
    public async Task<int> DeliverAsync(IAvailabilityChannelPublisher channel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var batch = new List<(Guid ProductId, string ExternalId, int Quantity, long Version)>();
        await using (var select = new NpgsqlCommand(
            """
            SELECT product_id, external_sku, desired_quantity, desired_version
            FROM online_ordering.availability_states
            WHERE channel = $1 AND delivered_quantity IS DISTINCT FROM desired_quantity
            ORDER BY desired_at, product_id
            LIMIT $2
            FOR UPDATE SKIP LOCKED;
            """, connection, transaction))
        {
            select.Parameters.AddWithValue(channel.Channel);
            select.Parameters.AddWithValue(channel.MaxBatchSize);
            await using var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                batch.Add((reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt64(3)));
        }

        if (batch.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }

        try
        {
            await channel.PublishAsync(batch.Select(b => new ChannelAvailability(b.ExternalId, b.Quantity)).ToList(), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            await using var failure = _dataSource.CreateCommand(
                """
                UPDATE online_ordering.availability_states
                SET delivery_attempts = delivery_attempts + 1, last_error = left($3, 200)
                WHERE channel = $1 AND product_id = ANY($2);
                """);
            failure.Parameters.AddWithValue(channel.Channel);
            failure.Parameters.AddWithValue(batch.Select(b => b.ProductId).ToArray());
            failure.Parameters.AddWithValue(ex.GetType().Name + ": " + ex.Message);
            await failure.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        foreach (var sent in batch)
        {
            await using var delivered = new NpgsqlCommand(
                """
                UPDATE online_ordering.availability_states
                SET delivered_quantity = $3, delivered_version = $4, delivered_at = now(), delivery_attempts = 0, last_error = NULL
                WHERE channel = $1 AND product_id = $2;
                """, connection, transaction);
            delivered.Parameters.AddWithValue(channel.Channel);
            delivered.Parameters.AddWithValue(sent.ProductId);
            delivered.Parameters.AddWithValue(sent.Quantity);
            delivered.Parameters.AddWithValue(sent.Version);
            await delivered.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return batch.Count;
    }

    /// <summary>
    /// Products whose channel still shows a different availability than the source after
    /// <paramref name="tolerance"/>, or whose delivery failed at least <paramref name="failedAttempts"/> times.
    /// </summary>
    public async Task<IReadOnlyList<AvailabilityDivergence>> FindDivergencesAsync(
        TimeSpan tolerance, int failedAttempts, int limit, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT channel, product_id, external_sku, desired_quantity, delivered_quantity, desired_at, delivery_attempts, last_error
            FROM online_ordering.availability_states
            WHERE delivered_quantity IS DISTINCT FROM desired_quantity
              AND (desired_at < now() - $1 OR delivery_attempts >= $2)
            ORDER BY desired_at, channel, product_id
            LIMIT $3;
            """);
        command.Parameters.AddWithValue(tolerance);
        command.Parameters.AddWithValue(failedAttempts);
        command.Parameters.AddWithValue(limit);

        var divergences = new List<AvailabilityDivergence>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            divergences.Add(new AvailabilityDivergence(
                reader.GetString(0), reader.GetGuid(1), reader.GetString(2), reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetInt32(4), reader.GetFieldValue<DateTimeOffset>(5), reader.GetInt32(6),
                reader.IsDBNull(7) ? null : reader.GetString(7)));
        }

        return divergences;
    }

    private async Task<Dictionary<Guid, (int Quantity, long Version)>> ObserveAsync(Guid[] productIds, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT m.product_id,
                   MIN(GREATEST(0, FLOOR(COALESCE(b.available_quantity, 0) / m.quantity_multiplier)))::int,
                   COALESCE(SUM(b.row_version), 0)::bigint
            FROM inventory.product_stock_mappings m
            JOIN inventory.stock_items s ON s.id = m.stock_item_id
            LEFT JOIN inventory.stock_balances b
                ON b.stock_item_id = m.stock_item_id AND b.stock_location_id = s.default_location_id
            WHERE m.product_id = ANY($1)
            GROUP BY m.product_id;
            """);
        command.Parameters.AddWithValue(productIds);

        var observed = new Dictionary<Guid, (int, long)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            observed[reader.GetGuid(0)] = (reader.GetInt32(1), reader.GetInt64(2));
        return observed;
    }
}
