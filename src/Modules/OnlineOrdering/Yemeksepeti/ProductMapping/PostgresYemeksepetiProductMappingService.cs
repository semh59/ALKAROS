using ALKAROS.Catalog.ProductCatalog;
using Npgsql;

namespace ALKAROS.OnlineOrdering.Yemeksepeti.ProductMapping;

public sealed class PostgresYemeksepetiProductMappingService : IYemeksepetiProductMappingService
{
    // Catalog's own SKU column width; a provider SKU longer than any product SKU cannot be ours.
    private const int MaxSkuLength = 100;

    private readonly NpgsqlDataSource _dataSource;
    private readonly IProductRepository _products;
    private readonly IProductModifierGroupRepository _productModifierGroups;
    private readonly IModifierGroupRepository _modifierGroups;

    public PostgresYemeksepetiProductMappingService(
        NpgsqlDataSource dataSource,
        IProductRepository products,
        IProductModifierGroupRepository productModifierGroups,
        IModifierGroupRepository modifierGroups)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _products = products ?? throw new ArgumentNullException(nameof(products));
        _productModifierGroups = productModifierGroups ?? throw new ArgumentNullException(nameof(productModifierGroups));
        _modifierGroups = modifierGroups ?? throw new ArgumentNullException(nameof(modifierGroups));
    }

    public async Task<YemeksepetiProductMapping> MapAsync(
        string externalSku,
        Guid productId,
        DateTimeOffset effectiveFrom,
        Guid actorId,
        CancellationToken cancellationToken = default) =>
        (await MapCoreAsync(externalSku, productId, effectiveFrom, actorId, requireUnowned: false, cancellationToken).ConfigureAwait(false))!;

    public Task<YemeksepetiProductMapping?> MapIfUnownedAsync(
        string externalSku,
        Guid productId,
        DateTimeOffset effectiveFrom,
        Guid actorId,
        CancellationToken cancellationToken = default) =>
        MapCoreAsync(externalSku, productId, effectiveFrom, actorId, requireUnowned: true, cancellationToken);

    private async Task<YemeksepetiProductMapping?> MapCoreAsync(
        string externalSku,
        Guid productId,
        DateTimeOffset effectiveFrom,
        Guid actorId,
        bool requireUnowned,
        CancellationToken cancellationToken)
    {
        var sku = NormalizeSku(externalSku);
        if (productId == Guid.Empty)
            throw new InvalidProductMappingRequestException("ProductId cannot be empty.");
        if (actorId == Guid.Empty)
            throw new InvalidProductMappingRequestException("ActorId cannot be empty.");

        var gap = await ProductGapAsync(productId, cancellationToken).ConfigureAwait(false);
        if (gap is { } rejection)
            throw new ProductMappingRejectedException(rejection, sku, productId);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Mapping changes are rare manager/publishing actions; one table-wide lock keeps the
        // "one open mapping per SKU and per product" check and the write a single step.
        await using (var lockCommand = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtext('online_ordering.yemeksepeti_product_mappings'));", connection, transaction))
        {
            await lockCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var openForSku = await FindOpenAsync("external_sku = $1", sku, connection, transaction, cancellationToken).ConfigureAwait(false);
        if (openForSku is not null && openForSku.ProductId == productId)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return openForSku;
        }

        // V12-RMD-005: decided under the lock, so no concurrent mapping can move the SKU in between.
        if (requireUnowned && openForSku is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        if (await HasMappingStartingAtOrAfterAsync(sku, effectiveFrom, connection, transaction, cancellationToken).ConfigureAwait(false))
            throw new ProductMappingRejectedException(ProductMappingRejection.LaterMappingExists, sku, productId);

        var openForProduct = await FindOpenAsync("product_id = $1", productId, connection, transaction, cancellationToken).ConfigureAwait(false);
        if (openForProduct is not null)
            throw new ProductMappingRejectedException(ProductMappingRejection.ProductMappedToAnotherSku, sku, productId);

        if (openForSku is not null)
        {
            await using var close = new NpgsqlCommand(
                """
                UPDATE online_ordering.yemeksepeti_product_mappings
                SET effective_to = $2, closed_by = $3
                WHERE mapping_id = $1;
                """, connection, transaction);
            close.Parameters.AddWithValue(openForSku.MappingId);
            close.Parameters.AddWithValue(effectiveFrom);
            close.Parameters.AddWithValue(actorId);
            await close.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var mapping = new YemeksepetiProductMapping(Guid.NewGuid(), sku, productId, effectiveFrom, null);
        await using (var insert = new NpgsqlCommand(
            """
            INSERT INTO online_ordering.yemeksepeti_product_mappings (
                mapping_id, external_sku, product_id, effective_from, effective_to, created_by
            ) VALUES ($1, $2, $3, $4, NULL, $5);
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue(mapping.MappingId);
            insert.Parameters.AddWithValue(sku);
            insert.Parameters.AddWithValue(productId);
            insert.Parameters.AddWithValue(effectiveFrom);
            insert.Parameters.AddWithValue(actorId);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return mapping;
    }

    public async Task<ProductMappingResolution> ResolveAsync(
        string externalSku,
        DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        var sku = NormalizeSku(externalSku);

        await using var command = _dataSource.CreateCommand(
            """
            SELECT mapping_id, product_id
            FROM online_ordering.yemeksepeti_product_mappings
            WHERE external_sku = $1
              AND effective_from <= $2
              AND (effective_to IS NULL OR effective_to > $2)
            LIMIT 2;
            """);
        command.Parameters.AddWithValue(sku);
        command.Parameters.AddWithValue(at);

        var matches = new List<(Guid MappingId, Guid ProductId)>(2);
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                matches.Add((reader.GetGuid(0), reader.GetGuid(1)));
        }

        if (matches.Count == 0)
            return new ProductMappingResolution(ProductMappingResolutionOutcome.Unmapped, sku, null, null);
        if (matches.Count > 1)
            return new ProductMappingResolution(ProductMappingResolutionOutcome.Ambiguous, sku, null, null);

        var (mappingId, productId) = matches[0];
        // A product deleted or deactivated since it was mapped is "no longer active" either way.
        var outcome = await ProductGapAsync(productId, cancellationToken).ConfigureAwait(false) switch
        {
            null => ProductMappingResolutionOutcome.Resolved,
            ProductMappingRejection.ProductRequiresModifierChoice => ProductMappingResolutionOutcome.ProductRequiresModifierChoice,
            _ => ProductMappingResolutionOutcome.ProductInactive
        };
        return new ProductMappingResolution(outcome, sku, productId, mappingId);
    }

    public async Task<string?> FindOpenSkuForProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT external_sku FROM online_ordering.yemeksepeti_product_mappings
            WHERE product_id = $1 AND effective_to IS NULL
            LIMIT 1;
            """);
        command.Parameters.AddWithValue(productId);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
    }

    public async Task<IReadOnlyList<YemeksepetiProductMapping>> ListOpenMappingsAsync(int limit, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        await using var command = _dataSource.CreateCommand(
            """
            SELECT mapping_id, external_sku, product_id, effective_from
            FROM online_ordering.yemeksepeti_product_mappings
            WHERE effective_to IS NULL AND effective_from <= now()
            ORDER BY external_sku
            LIMIT $1;
            """);
        command.Parameters.AddWithValue(limit);
        var mappings = new List<YemeksepetiProductMapping>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            mappings.Add(new YemeksepetiProductMapping(
                reader.GetGuid(0), reader.GetString(1), reader.GetGuid(2), reader.GetFieldValue<DateTimeOffset>(3), null));
        }

        return mappings;
    }

    private static string NormalizeSku(string externalSku)
    {
        if (string.IsNullOrWhiteSpace(externalSku))
            throw new InvalidProductMappingRequestException("External SKU cannot be empty.");
        var sku = externalSku.Trim();
        if (sku.Length > MaxSkuLength)
            throw new InvalidProductMappingRequestException($"External SKU cannot exceed {MaxSkuLength} characters.");
        if (sku.Any(char.IsControl))
            throw new InvalidProductMappingRequestException("External SKU cannot contain control characters.");
        return sku;
    }

    /// <summary>Why this product cannot be sold through the channel right now, or null when it can.</summary>
    private async Task<ProductMappingRejection?> ProductGapAsync(Guid productId, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(productId, cancellationToken).ConfigureAwait(false);
        if (product is null)
            return ProductMappingRejection.ProductNotFound;
        if (!product.Active)
            return ProductMappingRejection.ProductInactive;

        foreach (var link in await _productModifierGroups.GetByProductAsync(productId, cancellationToken).ConfigureAwait(false))
        {
            var group = await _modifierGroups.GetByIdAsync(link.ModifierGroupId, cancellationToken).ConfigureAwait(false);
            if (group is { Active: true, MinSelections: > 0 })
                return ProductMappingRejection.ProductRequiresModifierChoice;
        }

        return null;
    }

    private static async Task<YemeksepetiProductMapping?> FindOpenAsync<T>(
        string predicate,
        T value,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            $"""
            SELECT mapping_id, external_sku, product_id, effective_from
            FROM online_ordering.yemeksepeti_product_mappings
            WHERE {predicate} AND effective_to IS NULL
            LIMIT 1;
            """, connection, transaction);
        command.Parameters.AddWithValue(value!);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;
        return new YemeksepetiProductMapping(
            reader.GetGuid(0), reader.GetString(1), reader.GetGuid(2), reader.GetFieldValue<DateTimeOffset>(3), null);
    }

    private static async Task<bool> HasMappingStartingAtOrAfterAsync(
        string sku,
        DateTimeOffset effectiveFrom,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1 FROM online_ordering.yemeksepeti_product_mappings
                WHERE external_sku = $1 AND effective_from >= $2);
            """, connection, transaction);
        command.Parameters.AddWithValue(sku);
        command.Parameters.AddWithValue(effectiveFrom);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
    }
}
