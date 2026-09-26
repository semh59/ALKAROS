using ALKAROS.Catalog.ProductCatalog;
using ALKAROS.TestHelpers;

namespace ALKAROS.OnlineOrdering.Yemeksepeti.ProductMapping.Tests;

public sealed class ProductMappingTestDatabase : PgTestDatabase
{
    public ProductMappingTestDatabase() : base("alkaros_ysp_map_test_") { }

    protected override async Task ApplySqlAsync()
    {
        foreach (var file in new[]
                 {
                     "006-catalog.up.sql",
                     "040-wave9-schema-additions.up.sql",
                     "053-catalog-products-row-version.up.sql",
                     "103-products-prep-time.up.sql",
                     "144-yemeksepeti-product-mappings.up.sql",
                     "154-provider-neutral-inbox-and-mapping.up.sql"
                 })
        {
            await RunSqlFileAsync(file);
        }
    }

    public async Task RunSqlFileAsync(string file) =>
        await RunAsync(DataSource, await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", file)));

    public PostgresYemeksepetiProductMappingService CreateService() => new(
        DataSource,
        new PostgresProductRepository(DataSource),
        new PostgresProductModifierGroupRepository(DataSource),
        new PostgresModifierGroupRepository(DataSource));

    public async Task<Guid> SeedProductAsync(bool active = true)
    {
        var productId = Guid.NewGuid();
        await ExecuteSqlAsync(
            "INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active) VALUES ($1, $2, 'Lahmacun', 1, 1, $3);",
            productId, "LHM-" + productId.ToString("N")[..8], active);
        return productId;
    }

    public Task SetProductActiveAsync(Guid productId, bool active) =>
        ExecuteSqlAsync("UPDATE catalog.products SET active = $2 WHERE product_id = $1;", productId, active);

    /// <summary>Attaches an active modifier group to the product with the given minimum selection.</summary>
    public async Task AttachModifierGroupAsync(Guid productId, int minSelections)
    {
        var groupId = Guid.NewGuid();
        await ExecuteSqlAsync(
            "INSERT INTO catalog.modifier_groups (modifier_group_id, code, name, selection_type, min_selections, max_selections, active) VALUES ($1, $2, 'Acı seçimi', 1, $3, 1, true);",
            groupId, "MG-" + groupId.ToString("N")[..8], (short)minSelections);
        await ExecuteSqlAsync(
            "INSERT INTO catalog.product_modifier_groups (product_modifier_group_id, product_id, modifier_group_id) VALUES ($1, $2, $3);",
            Guid.NewGuid(), productId, groupId);
    }

    public async Task InsertRawMappingAsync(
        string sku, Guid productId, DateTimeOffset from, DateTimeOffset? to, string provider = "yemeksepeti")
    {
        await ExecuteSqlAsync(
            """
            INSERT INTO online_ordering.provider_product_mappings (mapping_id, external_sku, product_id, effective_from, effective_to, created_by, provider)
            VALUES ($1, $2, $3, $4, $5, $6, $7);
            """,
            Guid.NewGuid(), sku, productId, from, (object?)to ?? DBNull.Value, Guid.NewGuid(), provider);
    }

    /// <summary>V12-ONL-008: a mapping written before migration 154 (no platform column yet).</summary>
    public Task InsertRawMappingOnOldTableAsync(string sku, Guid productId, DateTimeOffset from) =>
        ExecuteSqlAsync(
            """
            INSERT INTO online_ordering.yemeksepeti_product_mappings (mapping_id, external_sku, product_id, effective_from, created_by)
            VALUES ($1, $2, $3, $4, $5);
            """,
            Guid.NewGuid(), sku, productId, from, Guid.NewGuid());

    /// <summary>V12-ONL-008: removes another platform's rows a test added.</summary>
    public Task DeletePlatformMappingsAsync(string provider) =>
        ExecuteSqlAsync("DELETE FROM online_ordering.provider_product_mappings WHERE provider = $1;", provider);

    public async Task<long> CountMappingsAsync(string sku)
    {
        await using var command = DataSource.CreateCommand(
            "SELECT count(*) FROM online_ordering.provider_product_mappings WHERE external_sku = $1;");
        command.Parameters.AddWithValue(sku);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task ExecuteSqlAsync(string sql, params object[] parameters)
    {
        await using var command = DataSource.CreateCommand(sql);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter);
        await command.ExecuteNonQueryAsync();
    }
}
