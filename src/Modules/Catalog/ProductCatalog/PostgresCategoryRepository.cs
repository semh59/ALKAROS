using Npgsql;

namespace ALKAROS.Catalog.ProductCatalog;

public sealed class PostgresCategoryRepository : ICategoryRepository
{
    private const string Table = "catalog.categories";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresCategoryRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT category_id, code, name, parent_category_id, sort_order, active
            FROM {Table}
            WHERE category_id = @id;
            """);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new Category(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetGuid(3),
            reader.GetInt32(4),
            reader.GetBoolean(5));
    }

    public async Task<Category?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT category_id, code, name, parent_category_id, sort_order, active
            FROM {Table}
            WHERE code = @code;
            """);
        command.Parameters.AddWithValue("code", code);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new Category(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetGuid(3),
            reader.GetInt32(4),
            reader.GetBoolean(5));
    }

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<Category>();

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT category_id, code, name, parent_category_id, sort_order, active
            FROM {Table}
            ORDER BY sort_order, code;
            """);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new Category(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetGuid(3),
                reader.GetInt32(4),
                reader.GetBoolean(5)));
        }

        return result;
    }

    public async Task AddAsync(Category category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);

        await using var command = _dataSource.CreateCommand(
            $"""
            INSERT INTO {Table} (category_id, code, name, parent_category_id, sort_order, active)
            VALUES (@id, @code, @name, @parent_category_id, @sort_order, @active);
            """);
        command.Parameters.AddWithValue("id", category.Id);
        command.Parameters.AddWithValue("code", category.Code);
        command.Parameters.AddWithValue("name", category.Name);
        command.Parameters.AddWithValue("parent_category_id", category.ParentId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("sort_order", category.SortOrder);
        command.Parameters.AddWithValue("active", category.Active);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(Category category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);

        await using var command = _dataSource.CreateCommand(
            $"""
            UPDATE {Table}
            SET code = @code,
                name = @name,
                parent_category_id = @parent_category_id,
                sort_order = @sort_order,
                active = @active
            WHERE category_id = @id;
            """);
        command.Parameters.AddWithValue("id", category.Id);
        command.Parameters.AddWithValue("code", category.Code);
        command.Parameters.AddWithValue("name", category.Name);
        command.Parameters.AddWithValue("parent_category_id", category.ParentId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("sort_order", category.SortOrder);
        command.Parameters.AddWithValue("active", category.Active);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
            throw new InvalidOperationException($"Category {category.Id} not found.");
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            DELETE FROM {Table} WHERE category_id = @id;
            """);
        command.Parameters.AddWithValue("id", id);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}