using Npgsql;

namespace ALKAROS.Catalog.ProductCatalog;

public sealed class PostgresModifierRepository : IModifierRepository
{
    private const string Table = "catalog.modifiers";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresModifierRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<Modifier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT modifier_id, modifier_group_id, code, name, price_delta, product_id, active
            FROM {Table}
            WHERE modifier_id = @id;
            """);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new Modifier(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetDecimal(4),
            reader.IsDBNull(5) ? null : reader.GetGuid(5),
            reader.GetBoolean(6));
    }

    public async Task<IReadOnlyList<Modifier>> GetByModifierGroupAsync(Guid modifierGroupId, CancellationToken cancellationToken = default)
    {
        var result = new List<Modifier>();

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT modifier_id, modifier_group_id, code, name, price_delta, product_id, active
            FROM {Table}
            WHERE modifier_group_id = @modifier_group_id
            ORDER BY name, code;
            """);
        command.Parameters.AddWithValue("modifier_group_id", modifierGroupId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new Modifier(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetDecimal(4),
                reader.IsDBNull(5) ? null : reader.GetGuid(5),
                reader.GetBoolean(6)));
        }

        return result;
    }

    public async Task AddAsync(Modifier modifier, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(modifier);

        await using var command = _dataSource.CreateCommand(
            $"""
            INSERT INTO {Table} (modifier_id, modifier_group_id, code, name, price_delta, product_id, active)
            VALUES (@id, @modifier_group_id, @code, @name, @price_delta, @product_id, @active);
            """);
        command.Parameters.AddWithValue("id", modifier.Id);
        command.Parameters.AddWithValue("modifier_group_id", modifier.ModifierGroupId);
        command.Parameters.AddWithValue("code", modifier.Code);
        command.Parameters.AddWithValue("name", modifier.Name);
        command.Parameters.AddWithValue("price_delta", modifier.PriceDelta);
        command.Parameters.AddWithValue("product_id", modifier.ProductId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("active", modifier.Active);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(Modifier modifier, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(modifier);

        await using var command = _dataSource.CreateCommand(
            $"""
            UPDATE {Table}
            SET modifier_group_id = @modifier_group_id,
                code = @code,
                name = @name,
                price_delta = @price_delta,
                product_id = @product_id,
                active = @active
            WHERE modifier_id = @id;
            """);
        command.Parameters.AddWithValue("id", modifier.Id);
        command.Parameters.AddWithValue("modifier_group_id", modifier.ModifierGroupId);
        command.Parameters.AddWithValue("code", modifier.Code);
        command.Parameters.AddWithValue("name", modifier.Name);
        command.Parameters.AddWithValue("price_delta", modifier.PriceDelta);
        command.Parameters.AddWithValue("product_id", modifier.ProductId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("active", modifier.Active);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
            throw new InvalidOperationException($"Modifier {modifier.Id} not found.");
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            DELETE FROM {Table} WHERE modifier_id = @id;
            """);
        command.Parameters.AddWithValue("id", id);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}