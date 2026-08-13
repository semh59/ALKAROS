using Npgsql;

namespace ALKAROS.Catalog.ProductCatalog;

public sealed class PostgresProductModifierGroupRepository : IProductModifierGroupRepository
{
    private const string Table = "catalog.product_modifier_groups";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresProductModifierGroupRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<ProductModifierGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT product_modifier_group_id, product_id, modifier_group_id
            FROM {Table}
            WHERE product_modifier_group_id = @id;
            """);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new ProductModifierGroup(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2));
    }

    public async Task<IReadOnlyList<ProductModifierGroup>> GetByProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var result = new List<ProductModifierGroup>();

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT product_modifier_group_id, product_id, modifier_group_id
            FROM {Table}
            WHERE product_id = @product_id
            ORDER BY modifier_group_id;
            """);
        command.Parameters.AddWithValue("product_id", productId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ProductModifierGroup(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2)));
        }

        return result;
    }

    public async Task AddAsync(ProductModifierGroup productModifierGroup, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(productModifierGroup);

        await using var command = _dataSource.CreateCommand(
            $"""
            INSERT INTO {Table} (product_modifier_group_id, product_id, modifier_group_id)
            VALUES (@id, @product_id, @modifier_group_id);
            """);
        command.Parameters.AddWithValue("id", productModifierGroup.Id);
        command.Parameters.AddWithValue("product_id", productModifierGroup.ProductId);
        command.Parameters.AddWithValue("modifier_group_id", productModifierGroup.ModifierGroupId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            DELETE FROM {Table} WHERE product_modifier_group_id = @id;
            """);
        command.Parameters.AddWithValue("id", id);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}