using Npgsql;

namespace ALKAROS.Catalog.ProductCatalog;

public sealed class PostgresModifierGroupRepository : IModifierGroupRepository
{
    private const string Table = "catalog.modifier_groups";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresModifierGroupRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<ModifierGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT modifier_group_id, code, name, selection_type, min_selections, max_selections, active
            FROM {Table}
            WHERE modifier_group_id = @id;
            """);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new ModifierGroup(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            (SelectionType)reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetInt32(5),
            reader.GetBoolean(6));
    }

    public async Task<ModifierGroup?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT modifier_group_id, code, name, selection_type, min_selections, max_selections, active
            FROM {Table}
            WHERE code = @code;
            """);
        command.Parameters.AddWithValue("code", code);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new ModifierGroup(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            (SelectionType)reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetInt32(5),
            reader.GetBoolean(6));
    }

    public async Task<IReadOnlyList<ModifierGroup>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<ModifierGroup>();

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT modifier_group_id, code, name, selection_type, min_selections, max_selections, active
            FROM {Table}
            ORDER BY code;
            """);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ModifierGroup(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                (SelectionType)reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetBoolean(6)));
        }

        return result;
    }

    public async Task AddAsync(ModifierGroup modifierGroup, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(modifierGroup);

        await using var command = _dataSource.CreateCommand(
            $"""
            INSERT INTO {Table} (modifier_group_id, code, name, selection_type, min_selections, max_selections, active)
            VALUES (@id, @code, @name, @selection_type, @min_selections, @max_selections, @active);
            """);
        command.Parameters.AddWithValue("id", modifierGroup.Id);
        command.Parameters.AddWithValue("code", modifierGroup.Code);
        command.Parameters.AddWithValue("name", modifierGroup.Name);
        command.Parameters.AddWithValue("selection_type", (int)modifierGroup.SelectionType);
        command.Parameters.AddWithValue("min_selections", modifierGroup.MinSelections);
        command.Parameters.AddWithValue("max_selections", modifierGroup.MaxSelections);
        command.Parameters.AddWithValue("active", modifierGroup.Active);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(ModifierGroup modifierGroup, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(modifierGroup);

        await using var command = _dataSource.CreateCommand(
            $"""
            UPDATE {Table}
            SET code = @code,
                name = @name,
                selection_type = @selection_type,
                min_selections = @min_selections,
                max_selections = @max_selections,
                active = @active
            WHERE modifier_group_id = @id;
            """);
        command.Parameters.AddWithValue("id", modifierGroup.Id);
        command.Parameters.AddWithValue("code", modifierGroup.Code);
        command.Parameters.AddWithValue("name", modifierGroup.Name);
        command.Parameters.AddWithValue("selection_type", (int)modifierGroup.SelectionType);
        command.Parameters.AddWithValue("min_selections", modifierGroup.MinSelections);
        command.Parameters.AddWithValue("max_selections", modifierGroup.MaxSelections);
        command.Parameters.AddWithValue("active", modifierGroup.Active);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
            throw new InvalidOperationException($"Modifier group {modifierGroup.Id} not found.");
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            DELETE FROM {Table} WHERE modifier_group_id = @id;
            """);
        command.Parameters.AddWithValue("id", id);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}