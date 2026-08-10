using Npgsql;

namespace ALKAROS.Catalog.ProductCatalog;

public sealed class PostgresProductRepository : IProductRepository
{
    private const string Table = "catalog.products";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresProductRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT product_id, sku, name, product_type, stock_mode, category_id, tax_profile_id,
                   description, printer_route_policy, display_order, current_price, active
            FROM {Table}
            WHERE product_id = @id;
            """);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadProduct(reader);
    }

    public async Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sku);

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT product_id, sku, name, product_type, stock_mode, category_id, tax_profile_id,
                   description, printer_route_policy, display_order, current_price, active
            FROM {Table}
            WHERE sku = @sku;
            """);
        command.Parameters.AddWithValue("sku", sku);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadProduct(reader);
    }

    public async Task<IReadOnlyList<Product>> GetByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        var result = new List<Product>();

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT product_id, sku, name, product_type, stock_mode, category_id, tax_profile_id,
                   description, printer_route_policy, display_order, current_price, active
            FROM {Table}
            WHERE category_id = @category_id
            ORDER BY display_order, sku;
            """);
        command.Parameters.AddWithValue("category_id", categoryId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(ReadProduct(reader));

        return result;
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<Product>();

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT product_id, sku, name, product_type, stock_mode, category_id, tax_profile_id,
                   description, printer_route_policy, display_order, current_price, active
            FROM {Table}
            ORDER BY display_order, sku;
            """);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(ReadProduct(reader));

        return result;
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(product);

        await using var command = _dataSource.CreateCommand(
            $"""
            INSERT INTO {Table} (
                product_id, sku, name, product_type, stock_mode, category_id, tax_profile_id,
                description, printer_route_policy, display_order, current_price, active)
            VALUES (@id, @sku, @name, @product_type, @stock_mode, @category_id, @tax_profile_id,
                    @description, @printer_route_policy, @display_order, @current_price, @active);
            """);
        command.Parameters.AddWithValue("id", product.Id);
        command.Parameters.AddWithValue("sku", product.Sku);
        command.Parameters.AddWithValue("name", product.Name);
        command.Parameters.AddWithValue("product_type", (int)product.ProductType);
        command.Parameters.AddWithValue("stock_mode", (int)product.StockMode);
        command.Parameters.AddWithValue("category_id", product.CategoryId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("tax_profile_id", product.TaxProfileId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("description", (object?)product.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("printer_route_policy", (object?)product.PrinterRoutePolicy ?? DBNull.Value);
        command.Parameters.AddWithValue("display_order", product.DisplayOrder);
        command.Parameters.AddWithValue("current_price", (object?)product.CurrentPrice ?? DBNull.Value);
        command.Parameters.AddWithValue("active", product.Active);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(product);

        await using var command = _dataSource.CreateCommand(
            $"""
            UPDATE {Table}
            SET sku = @sku,
                name = @name,
                product_type = @product_type,
                stock_mode = @stock_mode,
                category_id = @category_id,
                tax_profile_id = @tax_profile_id,
                description = @description,
                printer_route_policy = @printer_route_policy,
                display_order = @display_order,
                current_price = @current_price,
                active = @active
            WHERE product_id = @id;
            """);
        command.Parameters.AddWithValue("id", product.Id);
        command.Parameters.AddWithValue("sku", product.Sku);
        command.Parameters.AddWithValue("name", product.Name);
        command.Parameters.AddWithValue("product_type", (int)product.ProductType);
        command.Parameters.AddWithValue("stock_mode", (int)product.StockMode);
        command.Parameters.AddWithValue("category_id", product.CategoryId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("tax_profile_id", product.TaxProfileId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("description", (object?)product.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("printer_route_policy", (object?)product.PrinterRoutePolicy ?? DBNull.Value);
        command.Parameters.AddWithValue("display_order", product.DisplayOrder);
        command.Parameters.AddWithValue("current_price", (object?)product.CurrentPrice ?? DBNull.Value);
        command.Parameters.AddWithValue("active", product.Active);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
            throw new InvalidOperationException($"Product {product.Id} not found.");
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            DELETE FROM {Table} WHERE product_id = @id;
            """);
        command.Parameters.AddWithValue("id", id);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Product ReadProduct(NpgsqlDataReader reader)
    {
        return new Product(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            (ProductType)reader.GetInt32(3),
            (StockMode)reader.GetInt32(4),
            reader.IsDBNull(5) ? null : reader.GetGuid(5),
            reader.IsDBNull(6) ? null : reader.GetGuid(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.GetInt32(9),
            reader.IsDBNull(10) ? null : reader.GetDecimal(10),
            reader.GetBoolean(11));
    }
}