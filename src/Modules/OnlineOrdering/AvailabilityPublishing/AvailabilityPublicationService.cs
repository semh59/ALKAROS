using Npgsql;

namespace ALKAROS.OnlineOrdering.AvailabilityPublishing;

/// <summary>
/// V12-ONL-005: keeps every enabled online channel told how many units of each product it knows can
/// be sold. The single approved source is V11-INV-007's reserved/available balance projection
/// (<c>inventory.stock_balances.available_quantity</c>, read across schemas — V0-ARC-001), so a
/// cross-channel hold (V12-STK-001) lowers what the channels see at once. A product's units are the
/// fewest of its mapped stock items, each divided by its multiplier.
///
/// Ordering (V12-RMD-005): a refresh takes a per-channel transaction lock, draws one version from
/// <c>online_ordering.availability_observation_seq</c> and reads the source under that lock, so every refresh is
/// newer than the one before it — including when only a mapping's multiplier or the set of mapped stock items
/// changed, which a version derived from the balance rows alone could not see. A state is replaced only by a
/// strictly newer observation, so a delayed old observation never overwrites a newer state. A delivery locks the rows it sends
/// (<c>FOR UPDATE SKIP LOCKED</c>) until it records them as delivered: a newer observation of the same
/// product waits for that and is then sent by the next pass, and parallel passes never send a product
/// twice. Throttling: one pass sends at most one batched provider call per channel.
/// </summary>
public sealed class AvailabilityPublicationService
{
    private const int MaxProducts = 2000;

    /// <summary>V12-RMD-005: a failed row waits 30 s x 2^(n-1) before its next attempt, at most 30 minutes.</summary>
    public const int FirstRetryDelaySeconds = 30;
    public const int MaxRetryDelaySeconds = 1800;

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

    /// <summary>
    /// Records the current availability of every product the channel knows; returns how many quantities changed.
    /// States of products the channel no longer publishes are removed (V12-RMD-005).
    /// </summary>
    public async Task<int> RefreshAsync(IAvailabilityChannelPublisher channel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        var products = await channel.PublishedProductsAsync(MaxProducts + 1, cancellationToken).ConfigureAwait(false);
        if (products.Count > MaxProducts)
            throw new InvalidOperationException($"Channel '{channel.Channel}' knows more than {MaxProducts} products; refusing a partial refresh.");

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (var lockCommand = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtext('online-ordering.availability-refresh:' || $1));", connection, transaction))
        {
            lockCommand.Parameters.AddWithValue(channel.Channel);
            await lockCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var productIds = products.Select(p => p.ProductId).ToArray();
        await using (var stale = new NpgsqlCommand(
            "DELETE FROM online_ordering.availability_states WHERE channel = $1 AND NOT (product_id = ANY($2));", connection, transaction))
        {
            stale.Parameters.AddWithValue(channel.Channel);
            stale.Parameters.AddWithValue(productIds);
            await stale.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var changed = 0;
        if (products.Count > 0)
        {
            long version;
            await using (var sequence = new NpgsqlCommand(
                "SELECT nextval('online_ordering.availability_observation_seq');", connection, transaction))
            {
                version = (long)(await sequence.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
            }

            var observed = await ObserveAsync(productIds, connection, transaction, cancellationToken).ConfigureAwait(false);
            foreach (var product in products)
            {
                if (!observed.TryGetValue(product.ProductId, out var quantity))
                    continue;
                changed += await RecordObservationAsync(
                    channel.Channel, product, quantity, version, connection, transaction, cancellationToken).ConfigureAwait(false);
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
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
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await RecordObservationAsync(channel, product, quantity, version, connection, null, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> RecordObservationAsync(
        string channel, PublishedChannelProduct product, int quantity, long version,
        NpgsqlConnection connection, NpgsqlTransaction? transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(product);
        await using var command = new NpgsqlCommand(
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
            """, connection, transaction);
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

        // V12-RMD-005: rows that have never failed go first, as one batch. Only when none are waiting is a single
        // failed row whose wait is over sent on its own — so one bad SKU can neither block the others nor hide
        // among them, and each failing row is retried alone until it goes through or is reconciled.
        var batch = await SelectBatchAsync(channel, channel.MaxBatchSize, failedOnly: false, connection, transaction, cancellationToken)
            .ConfigureAwait(false);
        if (batch.Count == 0)
            batch = await SelectBatchAsync(channel, 1, failedOnly: true, connection, transaction, cancellationToken).ConfigureAwait(false);

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
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            await using var failure = _dataSource.CreateCommand(
                """
                UPDATE online_ordering.availability_states
                SET delivery_attempts = delivery_attempts + 1, last_error = left($3, 200),
                    next_attempt_at = now() + make_interval(secs => LEAST($5, $4 * power(2, delivery_attempts)))
                WHERE channel = $1 AND product_id = ANY($2);
                """);
            failure.Parameters.AddWithValue(channel.Channel);
            failure.Parameters.AddWithValue(batch.Select(b => b.ProductId).ToArray());
            failure.Parameters.AddWithValue(ex.GetType().Name + ": " + ex.Message);
            failure.Parameters.AddWithValue((double)FirstRetryDelaySeconds);
            failure.Parameters.AddWithValue((double)MaxRetryDelaySeconds);
            await failure.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        foreach (var sent in batch)
        {
            await using var delivered = new NpgsqlCommand(
                """
                UPDATE online_ordering.availability_states
                SET delivered_quantity = $3, delivered_version = $4, delivered_at = now(), delivery_attempts = 0, last_error = NULL,
                    next_attempt_at = NULL
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

    private static async Task<List<(Guid ProductId, string ExternalId, int Quantity, long Version)>> SelectBatchAsync(
        IAvailabilityChannelPublisher channel, int limit, bool failedOnly,
        NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        var batch = new List<(Guid, string, int, long)>();
        await using var select = new NpgsqlCommand(
            failedOnly
                ? """
                  SELECT product_id, external_sku, desired_quantity, desired_version
                  FROM online_ordering.availability_states
                  WHERE channel = $1 AND delivered_quantity IS DISTINCT FROM desired_quantity
                    AND delivery_attempts > 0 AND (next_attempt_at IS NULL OR next_attempt_at <= now())
                  ORDER BY next_attempt_at NULLS FIRST, product_id
                  LIMIT $2
                  FOR UPDATE SKIP LOCKED;
                  """
                : """
                  SELECT product_id, external_sku, desired_quantity, desired_version
                  FROM online_ordering.availability_states
                  WHERE channel = $1 AND delivered_quantity IS DISTINCT FROM desired_quantity AND delivery_attempts = 0
                  ORDER BY desired_at, product_id
                  LIMIT $2
                  FOR UPDATE SKIP LOCKED;
                  """,
            connection, transaction);
        select.Parameters.AddWithValue(channel.Channel);
        select.Parameters.AddWithValue(limit);
        await using var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            batch.Add((reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt64(3)));
        return batch;
    }

    private static async Task<Dictionary<Guid, int>> ObserveAsync(
        Guid[] productIds, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT m.product_id,
                   MIN(GREATEST(0, FLOOR(COALESCE(b.available_quantity, 0) / m.quantity_multiplier)))::int
            FROM inventory.product_stock_mappings m
            JOIN inventory.stock_items s ON s.id = m.stock_item_id
            LEFT JOIN inventory.stock_balances b
                ON b.stock_item_id = m.stock_item_id AND b.stock_location_id = s.default_location_id
            WHERE m.product_id = ANY($1)
            GROUP BY m.product_id;
            """, connection, transaction);
        command.Parameters.AddWithValue(productIds);

        var observed = new Dictionary<Guid, int>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            observed[reader.GetGuid(0)] = reader.GetInt32(1);
        return observed;
    }
}
