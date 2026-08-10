using Npgsql;

namespace ALKAROS.Identity.Authorization;

public sealed class PostgresPermissionRepository : IPermissionRepository
{
    private const string Table = "identity.permissions";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresPermissionRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<PermissionEntry?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT permission_id, code, name
            FROM {Table}
            WHERE code = @code;
            """);
        command.Parameters.AddWithValue("code", code);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadPermission(reader);
    }

    public async Task<IReadOnlyList<PermissionEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<PermissionEntry>();

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT permission_id, code, name
            FROM {Table}
            ORDER BY code;
            """);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(ReadPermission(reader));

        return result;
    }

    public async Task AddAsync(PermissionEntry permission, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permission);

        await using var command = _dataSource.CreateCommand(
            $"""
            INSERT INTO {Table} (permission_id, code, name)
            VALUES (@id, @code, @name);
            """);
        command.Parameters.AddWithValue("id", permission.Id);
        command.Parameters.AddWithValue("code", permission.Code);
        command.Parameters.AddWithValue("name", permission.Name);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new InvalidOperationException(
                $"Permission '{permission.Code}' already exists.", ex);
        }
    }

    private static PermissionEntry ReadPermission(NpgsqlDataReader reader)
        => new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2));
}