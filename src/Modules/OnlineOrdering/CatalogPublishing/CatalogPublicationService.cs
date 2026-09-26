using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ALKAROS.IntegrationContracts;
using ALKAROS.Messaging;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.OnlineOrdering.CatalogPublishing;

/// <summary>
/// V12-ONL-004: publishes one menu's products to one online channel. The projection is read from
/// the menu and catalog tables (a plain cross-schema read, V0-ARC-001); every product gets the
/// channel's stable external identifier before it is published, so publishing the same menu again
/// never creates a second external product. Products the channel cannot carry are listed as typed
/// validation errors and left out; capabilities the channel lacks are recorded on the publication.
/// The publication and its outbox request commit together, and the outbox retries the delivery.
/// </summary>
public sealed class CatalogPublicationService
{
    public const string RequestedEventType = "online-ordering.catalog-publication-requested.v1";

    // A single menu of more than this many products would be a data error, not a menu.
    private const int MaxMenuItems = 2000;

    private readonly NpgsqlDataSource _dataSource;
    private readonly IReadOnlyList<ICatalogChannelPublisher> _channels;

    public CatalogPublicationService(NpgsqlDataSource dataSource, IEnumerable<ICatalogChannelPublisher> channels)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _channels = (channels ?? throw new ArgumentNullException(nameof(channels))).ToList();
    }

    public async Task<CatalogPublicationSummary> RequestAsync(
        string channel,
        Guid menuId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (menuId == Guid.Empty)
            throw new ArgumentException("A menu is required.", nameof(menuId));
        if (actorId == Guid.Empty)
            throw new ArgumentException("A publication needs an actor.", nameof(actorId));
        var publisher = Channel(channel);

        // V12-RMD-005: the per-menu lock comes first, so the menu read below is never older than a concurrent
        // request's that commits after it, and a delivery in progress finishes before a new request is decided.
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await LockMenuAsync(publisher.Channel, menuId, connection, transaction, cancellationToken).ConfigureAwait(false);

        var rows = await LoadMenuAsync(menuId, cancellationToken).ConfigureAwait(false);
        var errors = new List<CatalogValidationError>();
        var items = new List<CatalogPublicationItem>();
        foreach (var row in rows)
        {
            if (row.Active && row.Price is null)
            {
                errors.Add(new CatalogValidationError(row.ProductId, CatalogValidationCode.PriceMissing));
                continue;
            }
            if (row.Active && row.HasModifiers && !publisher.SupportedCapabilities.Contains(CatalogCapability.Modifiers))
            {
                errors.Add(new CatalogValidationError(row.ProductId, CatalogValidationCode.ModifiersNotSupported));
                continue;
            }

            // A product that is not sellable is only published to switch off one the channel already
            // knows; it is never given a new external identifier just to be hidden.
            var externalId = await publisher.AssignExternalIdAsync(row.ProductId, row.Sku, row.Active, actorId, cancellationToken)
                .ConfigureAwait(false);
            if (externalId is null)
            {
                if (row.Active)
                    errors.Add(new CatalogValidationError(row.ProductId, CatalogValidationCode.ExternalIdUnavailable, row.Sku));
                continue;
            }

            // V12-RMD-005: an unsellable product without a price is switched off at the price the channel last
            // received for it; one the channel never received is simply not sent (there is nothing to switch off).
            var price = row.Price ?? await LastDeliveredPriceAsync(publisher.Channel, row.ProductId, connection, transaction, cancellationToken)
                .ConfigureAwait(false);
            if (price is null)
                continue;
            items.Add(new CatalogPublicationItem(row.ProductId, externalId, row.Name, price.Value, row.Active));
        }

        var unsupported = Enum.GetValues<CatalogCapability>().Where(c => !publisher.SupportedCapabilities.Contains(c)).ToList();
        var contentHash = ContentHash(items);

        var status = items.Count == 0
            ? CatalogPublicationStatus.NothingToPublish
            : await LatestContentHashAsync(publisher.Channel, menuId, connection, transaction, cancellationToken).ConfigureAwait(false) == contentHash
                ? CatalogPublicationStatus.Unchanged
                : CatalogPublicationStatus.Pending;

        var publicationId = Guid.NewGuid();
        await InsertPublicationAsync(
            publicationId, publisher.Channel, menuId, status, contentHash, items, errors, unsupported, actorId,
            connection, transaction, cancellationToken).ConfigureAwait(false);
        if (status == CatalogPublicationStatus.Pending)
        {
            await OutboxStore.EnqueueAsync(
                new OutboxEnvelope(
                    RequestedEventType,
                    "catalog_publication",
                    publicationId,
                    IntegrationEventSerializer.Serialize(new CatalogPublicationRequested(publicationId, publisher.Channel))),
                connection, transaction, cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new CatalogPublicationSummary(publicationId, publisher.Channel, status, items.Count, errors, unsupported);
    }

    /// <summary>
    /// Delivers a pending publication (the outbox consumer's work). At-least-once safe: a publication
    /// already delivered is not sent again. A failure is recorded and rethrown so the outbox retries.
    /// </summary>
    public async Task DeliverAsync(Guid publicationId, CancellationToken cancellationToken = default)
    {
        string channel;
        Guid menuId;
        await using (var command = _dataSource.CreateCommand(
            "SELECT channel, status, menu_id FROM online_ordering.catalog_publications WHERE publication_id = $1;"))
        {
            command.Parameters.AddWithValue(publicationId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                throw new InvalidOperationException($"Catalog publication '{publicationId}' does not exist.");
            channel = reader.GetString(0);
            if (reader.GetString(1) != nameof(CatalogPublicationStatus.Pending))
                return;
            menuId = reader.GetGuid(2);
        }

        // V12-RMD-005: deliveries of one menu are serialized and an overtaken publication is never sent, so the
        // channel always ends on the newest requested content even when an older publication's retry comes late.
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await LockMenuAsync(channel, menuId, connection, transaction, cancellationToken).ConfigureAwait(false);
        await using (var supersede = new NpgsqlCommand(
            """
            UPDATE online_ordering.catalog_publications p
            SET status = 'Superseded'
            WHERE p.publication_id = $1 AND p.status = 'Pending'
              AND EXISTS (SELECT 1 FROM online_ordering.catalog_publications newer
                          WHERE newer.channel = p.channel AND newer.menu_id = p.menu_id
                            AND newer.requested_at > p.requested_at
                            AND newer.status IN ('Pending', 'Delivered'))
            RETURNING 1;
            """, connection, transaction))
        {
            supersede.Parameters.AddWithValue(publicationId);
            if (await supersede.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
        }

        var items = new List<CatalogPublicationItem>();
        await using (var command = _dataSource.CreateCommand(
            """
            SELECT product_id, external_sku, title, price, active
            FROM online_ordering.catalog_publication_items
            WHERE publication_id = $1
            ORDER BY external_sku
            LIMIT $2;
            """))
        {
            command.Parameters.AddWithValue(publicationId);
            command.Parameters.AddWithValue(MaxMenuItems);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                items.Add(new CatalogPublicationItem(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3), reader.GetBoolean(4)));
        }

        string? jobId;
        try
        {
            jobId = await Channel(channel).PublishAsync(items, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // A timeout that is not a shutdown is recorded like any other failure (V12-RMD-005).
            await using var failure = _dataSource.CreateCommand(
                """
                UPDATE online_ordering.catalog_publications
                SET delivery_attempts = delivery_attempts + 1, last_error = left($2, 200)
                WHERE publication_id = $1;
                """);
            failure.Parameters.AddWithValue(publicationId);
            failure.Parameters.AddWithValue(ex.GetType().Name + ": " + ex.Message);
            await failure.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        await using (var delivered = new NpgsqlCommand(
            """
            UPDATE online_ordering.catalog_publications
            SET status = 'Delivered', provider_job_id = $2, delivered_at = now(), delivery_attempts = delivery_attempts + 1
            WHERE publication_id = $1 AND status = 'Pending';
            """, connection, transaction))
        {
            delivered.Parameters.AddWithValue(publicationId);
            delivered.Parameters.AddWithValue((object?)jobId ?? DBNull.Value);
            await delivered.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task LockMenuAsync(
        string channel, Guid menuId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var lockCommand = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtext('online-ordering.catalog:' || $1 || ':' || $2::text));", connection, transaction);
        lockCommand.Parameters.AddWithValue(channel);
        lockCommand.Parameters.AddWithValue(menuId);
        await lockCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<decimal?> LastDeliveredPriceAsync(
        string channel, Guid productId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT i.price
            FROM online_ordering.catalog_publication_items i
            JOIN online_ordering.catalog_publications p ON p.publication_id = i.publication_id
            WHERE p.channel = $1 AND i.product_id = $2 AND p.status = 'Delivered'
            ORDER BY p.delivered_at DESC, p.publication_id
            LIMIT 1;
            """, connection, transaction);
        command.Parameters.AddWithValue(channel);
        command.Parameters.AddWithValue(productId);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as decimal?;
    }

    private ICatalogChannelPublisher Channel(string channel) =>
        _channels.SingleOrDefault(c => string.Equals(c.Channel, channel, StringComparison.Ordinal))
        ?? throw new UnknownCatalogChannelException(channel);

    private async Task<IReadOnlyList<MenuRow>> LoadMenuAsync(Guid menuId, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT p.product_id, p.sku, p.name, p.current_price,
                   (m.active AND mi.active AND p.active) AS sellable,
                   EXISTS (
                       SELECT 1 FROM catalog.product_modifier_groups pmg
                       JOIN catalog.modifier_groups g ON g.modifier_group_id = pmg.modifier_group_id
                       WHERE pmg.product_id = p.product_id AND g.active) AS has_modifiers
            FROM menu.menu_items mi
            JOIN menu.menus m ON m.menu_id = mi.menu_id
            JOIN catalog.products p ON p.product_id = mi.product_id
            WHERE mi.menu_id = $1
            ORDER BY mi.display_order, p.sku
            LIMIT $2;
            """);
        command.Parameters.AddWithValue(menuId);
        command.Parameters.AddWithValue(MaxMenuItems + 1);

        var rows = new List<MenuRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new MenuRow(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetDecimal(3), reader.GetBoolean(4), reader.GetBoolean(5)));
        }

        if (rows.Count > MaxMenuItems)
            throw new InvalidOperationException($"Menu '{menuId}' has more than {MaxMenuItems} products; refusing a partial publication.");
        return rows;
    }

    /// <summary>
    /// V12-RMD-005: the content the channel has or will get — the latest publication's, when it was delivered or its
    /// delivery is still queued. A publication whose outbox message died will never arrive, so it does not count
    /// and the same content can be published again.
    /// </summary>
    private static async Task<string?> LatestContentHashAsync(
        string channel, Guid menuId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT CASE
                       WHEN p.status = 'Delivered' THEN p.content_sha256
                       WHEN EXISTS (SELECT 1 FROM outbox_messages m
                                    WHERE m.aggregate_id = p.publication_id AND m.event_type = $3
                                      AND m.status IN ('pending', 'in_flight')) THEN p.content_sha256
                   END
            FROM online_ordering.catalog_publications p
            WHERE p.channel = $1 AND p.menu_id = $2 AND p.status IN ('Pending', 'Delivered')
            ORDER BY p.requested_at DESC, p.publication_id
            LIMIT 1;
            """, connection, transaction);
        command.Parameters.AddWithValue(channel);
        command.Parameters.AddWithValue(menuId);
        command.Parameters.AddWithValue(RequestedEventType);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
    }

    private static async Task InsertPublicationAsync(
        Guid publicationId, string channel, Guid menuId, CatalogPublicationStatus status, string contentHash,
        List<CatalogPublicationItem> items, List<CatalogValidationError> errors,
        List<CatalogCapability> unsupported, Guid actorId,
        NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using (var command = new NpgsqlCommand(
            """
            INSERT INTO online_ordering.catalog_publications (
                publication_id, channel, menu_id, status, content_sha256, item_count,
                validation_errors, unsupported_capabilities, requested_by)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9);
            """, connection, transaction))
        {
            command.Parameters.AddWithValue(publicationId);
            command.Parameters.AddWithValue(channel);
            command.Parameters.AddWithValue(menuId);
            command.Parameters.AddWithValue(status.ToString());
            command.Parameters.AddWithValue(contentHash);
            command.Parameters.AddWithValue(items.Count);
            command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = JsonSerializer.Serialize(
                errors.Select(e => new { e.ProductId, code = e.Code.ToString(), e.Detail })) });
            command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = JsonSerializer.Serialize(
                unsupported.Select(c => c.ToString())) });
            command.Parameters.AddWithValue(actorId);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var item in items)
        {
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO online_ordering.catalog_publication_items (publication_id, product_id, external_sku, title, price, active)
                VALUES ($1, $2, $3, $4, $5, $6);
                """, connection, transaction);
            command.Parameters.AddWithValue(publicationId);
            command.Parameters.AddWithValue(item.ProductId);
            command.Parameters.AddWithValue(item.ExternalSku);
            command.Parameters.AddWithValue(item.Title);
            command.Parameters.AddWithValue(item.Price);
            command.Parameters.AddWithValue(item.Active);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static string ContentHash(IEnumerable<CatalogPublicationItem> items)
    {
        var canonical = string.Join('\n', items
            .OrderBy(i => i.ExternalSku, StringComparer.Ordinal)
            .Select(i => string.Join('\u001f', i.ExternalSku, i.Title, i.Price.ToString("0.00", CultureInfo.InvariantCulture), i.Active ? "1" : "0")));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private sealed record MenuRow(Guid ProductId, string Sku, string Name, decimal? Price, bool Active, bool HasModifiers);
}
