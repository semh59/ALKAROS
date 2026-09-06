using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace ALKAROS.Purchasing.Suppliers;

public sealed class PostgresSupplierRepository : ISupplierRepository
{
    private const int MaxUnpagedRows = 5000;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresSupplierRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<Supplier?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
SELECT supplier_id, code, name, tax_number, tax_office, phone, email, active, created_at, updated_at
FROM purchasing.suppliers
WHERE supplier_id = $1;";

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return MapFromReader(reader);
    }

    public async Task<Supplier?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        const string sql = @"
SELECT supplier_id, code, name, tax_number, tax_office, phone, email, active, created_at, updated_at
FROM purchasing.suppliers
WHERE code = $1;";

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(code.Trim().ToUpperInvariant());

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return MapFromReader(reader);
    }

    public async Task<Supplier?> GetByTaxNumberAsync(string taxNumber, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taxNumber);

        const string sql = @"
SELECT supplier_id, code, name, tax_number, tax_office, phone, email, active, created_at, updated_at
FROM purchasing.suppliers
WHERE tax_number = $1;";

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(taxNumber.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return MapFromReader(reader);
    }

    public async Task<IReadOnlyList<Supplier>> ListAsync(bool? activeOnly = null, CancellationToken ct = default)
    {
        string sql = @"
SELECT supplier_id, code, name, tax_number, tax_office, phone, email, active, created_at, updated_at
FROM purchasing.suppliers";

        if (activeOnly.HasValue)
        {
            sql += $" WHERE active = $1 ORDER BY code ASC LIMIT {MaxUnpagedRows + 1};";
        }
        else
        {
            sql += $" ORDER BY code ASC LIMIT {MaxUnpagedRows + 1};";
        }

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        if (activeOnly.HasValue)
        {
            cmd.Parameters.AddWithValue(activeOnly.Value);
        }

        var list = new List<Supplier>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(MapFromReader(reader));
        }

        if (list.Count > MaxUnpagedRows)
        {
            throw new InvalidOperationException(
                $"ListAsync returned more than {MaxUnpagedRows} rows; narrow the filter or paginate.");
        }

        return list;
    }

    public async Task SaveAsync(Supplier supplier, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(supplier);

        const string sql = @"
INSERT INTO purchasing.suppliers (supplier_id, code, name, tax_number, tax_office, phone, email, active, created_at, updated_at)
VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10);";

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(supplier.Id);
        cmd.Parameters.AddWithValue(supplier.Code);
        cmd.Parameters.AddWithValue(supplier.Name);
        cmd.Parameters.AddWithValue((object?)supplier.TaxNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)supplier.TaxOffice ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)supplier.Phone ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)supplier.Email ?? DBNull.Value);
        cmd.Parameters.AddWithValue(supplier.Active);
        cmd.Parameters.AddWithValue(supplier.CreatedAt);
        cmd.Parameters.AddWithValue(supplier.UpdatedAt);

        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            if (ex.ConstraintName != null && ex.ConstraintName.Contains("tax_number", StringComparison.OrdinalIgnoreCase))
            {
                throw new DuplicateSupplierTaxNumberException(supplier.TaxNumber ?? string.Empty);
            }
            throw new DuplicateSupplierCodeException(supplier.Code);
        }
    }

    public async Task UpdateAsync(Supplier supplier, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(supplier);

        const string sql = @"
UPDATE purchasing.suppliers
SET name = $2,
    tax_number = $3,
    tax_office = $4,
    phone = $5,
    email = $6,
    active = $7,
    updated_at = $8
WHERE supplier_id = $1;";

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(supplier.Id);
        cmd.Parameters.AddWithValue(supplier.Name);
        cmd.Parameters.AddWithValue((object?)supplier.TaxNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)supplier.TaxOffice ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)supplier.Phone ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)supplier.Email ?? DBNull.Value);
        cmd.Parameters.AddWithValue(supplier.Active);
        cmd.Parameters.AddWithValue(supplier.UpdatedAt);

        try
        {
            var rows = await cmd.ExecuteNonQueryAsync(ct);
            if (rows == 0)
            {
                throw new SupplierNotFoundException(supplier.Id);
            }
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            if (ex.ConstraintName != null && ex.ConstraintName.Contains("tax_number", StringComparison.OrdinalIgnoreCase))
            {
                throw new DuplicateSupplierTaxNumberException(supplier.TaxNumber ?? string.Empty);
            }
            throw new DuplicateSupplierCodeException(supplier.Code);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM purchasing.suppliers WHERE supplier_id = $1;";
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(id);
        var rows = await cmd.ExecuteNonQueryAsync(ct);
        if (rows == 0)
        {
            throw new SupplierNotFoundException(id);
        }
    }

    private static Supplier MapFromReader(DbDataReader reader)
    {
        var id = reader.GetGuid(0);
        var code = reader.GetString(1);
        var name = reader.GetString(2);
        var taxNumber = reader.IsDBNull(3) ? null : reader.GetString(3);
        var taxOffice = reader.IsDBNull(4) ? null : reader.GetString(4);
        var phone = reader.IsDBNull(5) ? null : reader.GetString(5);
        var email = reader.IsDBNull(6) ? null : reader.GetString(6);
        var active = reader.GetBoolean(7);
        var createdAt = reader.GetFieldValue<DateTimeOffset>(8);
        var updatedAt = reader.GetFieldValue<DateTimeOffset>(9);

        return new Supplier(
            id,
            code,
            name,
            taxNumber,
            taxOffice,
            phone,
            email,
            active,
            createdAt,
            updatedAt);
    }
}
