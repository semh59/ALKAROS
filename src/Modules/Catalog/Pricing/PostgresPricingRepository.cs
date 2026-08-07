using Npgsql;

namespace ALKAROS.Catalog.Pricing;

public sealed class PostgresPricingRepository : IPricingRepository
{
    private const string Table = "catalog.product_prices";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresPricingRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<ProductPrice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT product_price_id, product_id, price_type, price, currency_code,
                   effective_from, effective_to
            FROM {Table}
            WHERE product_price_id = @id;
            """);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadProductPrice(reader);
    }

    public async Task<ProductPrice?> GetEffectivePriceAsync(
        Guid productId,
        PriceType priceType,
        string currencyCode,
        DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currencyCode);

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT product_price_id, product_id, price_type, price, currency_code,
                   effective_from, effective_to
            FROM {Table}
            WHERE product_id = @product_id
              AND price_type = @price_type
              AND currency_code = @currency_code
              AND effective_from <= @at
              AND (effective_to IS NULL OR effective_to > @at)
            ORDER BY effective_from DESC
            LIMIT 1;
            """);
        command.Parameters.AddWithValue("product_id", productId);
        command.Parameters.AddWithValue("price_type", (int)priceType);
        command.Parameters.AddWithValue("currency_code", currencyCode);
        command.Parameters.AddWithValue("at", at);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadProductPrice(reader);
    }

    public async Task<IReadOnlyList<ProductPrice>> GetByProductAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var result = new List<ProductPrice>();

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT product_price_id, product_id, price_type, price, currency_code,
                   effective_from, effective_to
            FROM {Table}
            WHERE product_id = @product_id
            ORDER BY effective_from DESC, currency_code;
            """);
        command.Parameters.AddWithValue("product_id", productId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(ReadProductPrice(reader));

        return result;
    }

    public async Task AddAsync(ProductPrice price, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(price);

        await using var command = _dataSource.CreateCommand(
            $"""
            INSERT INTO {Table} (
                product_price_id, product_id, price_type, price, currency_code,
                effective_from, effective_to)
            VALUES (@id, @product_id, @price_type, @price, @currency_code,
                    @effective_from, @effective_to);
            """);
        command.Parameters.AddWithValue("id", price.Id);
        command.Parameters.AddWithValue("product_id", price.ProductId);
        command.Parameters.AddWithValue("price_type", (int)price.PriceType);
        command.Parameters.AddWithValue("price", price.Price);
        command.Parameters.AddWithValue("currency_code", price.CurrencyCode);
        command.Parameters.AddWithValue("effective_from", price.EffectiveFrom);
        command.Parameters.AddWithValue("effective_to", (object?)price.EffectiveTo ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(ProductPrice price, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(price);

        await using var command = _dataSource.CreateCommand(
            $"""
            UPDATE {Table}
            SET product_id = @product_id,
                price_type = @price_type,
                price = @price,
                currency_code = @currency_code,
                effective_from = @effective_from,
                effective_to = @effective_to
            WHERE product_price_id = @id;
            """);
        command.Parameters.AddWithValue("id", price.Id);
        command.Parameters.AddWithValue("product_id", price.ProductId);
        command.Parameters.AddWithValue("price_type", (int)price.PriceType);
        command.Parameters.AddWithValue("price", price.Price);
        command.Parameters.AddWithValue("currency_code", price.CurrencyCode);
        command.Parameters.AddWithValue("effective_from", price.EffectiveFrom);
        command.Parameters.AddWithValue("effective_to", (object?)price.EffectiveTo ?? DBNull.Value);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
            throw new InvalidOperationException($"Product price {price.Id} not found.");
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            DELETE FROM {Table} WHERE product_price_id = @id;
            """);
        command.Parameters.AddWithValue("id", id);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static ProductPrice ReadProductPrice(NpgsqlDataReader reader)
    {
        return new ProductPrice(
            reader.GetGuid(0),
            reader.GetGuid(1),
            (PriceType)reader.GetInt32(2),
            reader.GetDecimal(3),
            ReadTimestamp(reader.GetDateTime(5)),
            reader.GetString(4).Trim(),
            reader.IsDBNull(6) ? null : ReadTimestamp(reader.GetDateTime(6)));
    }

    private static DateTimeOffset ReadTimestamp(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return new DateTimeOffset(utc);
    }
}