using Npgsql;

namespace ALKAROS.Catalog.ProductCatalog;

public sealed class PostgresTaxProfileRepository : ITaxProfileRepository
{
    private const string Table = "catalog.tax_profiles";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresTaxProfileRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<TaxProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT tax_profile_id, code, name, vat_rate, active
            FROM {Table}
            WHERE tax_profile_id = @id;
            """);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new TaxProfile(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetDecimal(3),
            reader.GetBoolean(4));
    }

    public async Task<TaxProfile?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT tax_profile_id, code, name, vat_rate, active
            FROM {Table}
            WHERE code = @code;
            """);
        command.Parameters.AddWithValue("code", code);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new TaxProfile(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetDecimal(3),
            reader.GetBoolean(4));
    }

    public async Task<IReadOnlyList<TaxProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<TaxProfile>();

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT tax_profile_id, code, name, vat_rate, active
            FROM {Table}
            ORDER BY code;
            """);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new TaxProfile(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDecimal(3),
                reader.GetBoolean(4)));
        }

        return result;
    }

    public async Task AddAsync(TaxProfile taxProfile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(taxProfile);

        await using var command = _dataSource.CreateCommand(
            $"""
            INSERT INTO {Table} (tax_profile_id, code, name, vat_rate, active)
            VALUES (@id, @code, @name, @vat_rate, @active);
            """);
        command.Parameters.AddWithValue("id", taxProfile.Id);
        command.Parameters.AddWithValue("code", taxProfile.Code);
        command.Parameters.AddWithValue("name", taxProfile.Name);
        command.Parameters.AddWithValue("vat_rate", taxProfile.VatRate);
        command.Parameters.AddWithValue("active", taxProfile.Active);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(TaxProfile taxProfile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(taxProfile);

        await using var command = _dataSource.CreateCommand(
            $"""
            UPDATE {Table}
            SET code = @code,
                name = @name,
                vat_rate = @vat_rate,
                active = @active
            WHERE tax_profile_id = @id;
            """);
        command.Parameters.AddWithValue("id", taxProfile.Id);
        command.Parameters.AddWithValue("code", taxProfile.Code);
        command.Parameters.AddWithValue("name", taxProfile.Name);
        command.Parameters.AddWithValue("vat_rate", taxProfile.VatRate);
        command.Parameters.AddWithValue("active", taxProfile.Active);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
            throw new InvalidOperationException($"Tax profile {taxProfile.Id} not found.");
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            DELETE FROM {Table} WHERE tax_profile_id = @id;
            """);
        command.Parameters.AddWithValue("id", id);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}