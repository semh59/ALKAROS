using Npgsql;

namespace ALKAROS.Invoicing.Generation.OrderInvoices;

/// <summary><c>invoicing.seller_profile</c> (migration 171): a single row, replaced on save.</summary>
public sealed class PostgresSellerProfileStore(NpgsqlDataSource dataSource) : ISellerProfileStore
{
    private readonly NpgsqlDataSource _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));

    public async Task<SellerProfile?> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT legal_name, tax_id_kind, tax_id_number, tax_office, address, district, city, email
            FROM invoicing.seller_profile;
            """);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new SellerProfile(
                reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.GetString(5), reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7))
            : null;
    }

    public async Task SaveAsync(SellerProfile profile, Guid? updatedBy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Problems() is { Count: > 0 } problems)
            throw new ArgumentException("Seller profile is incomplete: " + string.Join(", ", problems), nameof(profile));

        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO invoicing.seller_profile
                (singleton, legal_name, tax_id_kind, tax_id_number, tax_office, address, district, city, email, updated_by)
            VALUES (true, @legal_name, @tax_id_kind, @tax_id_number, @tax_office, @address, @district, @city, @email, @updated_by)
            ON CONFLICT (singleton) DO UPDATE SET
                legal_name = EXCLUDED.legal_name, tax_id_kind = EXCLUDED.tax_id_kind,
                tax_id_number = EXCLUDED.tax_id_number, tax_office = EXCLUDED.tax_office,
                address = EXCLUDED.address, district = EXCLUDED.district, city = EXCLUDED.city,
                email = EXCLUDED.email, updated_by = EXCLUDED.updated_by, updated_at = now();
            """);
        command.Parameters.AddWithValue("legal_name", profile.LegalName.Trim());
        command.Parameters.AddWithValue("tax_id_kind", profile.TaxIdKind);
        command.Parameters.AddWithValue("tax_id_number", profile.TaxIdNumber);
        command.Parameters.AddWithValue("tax_office", profile.TaxOffice.Trim());
        command.Parameters.AddWithValue("address", profile.Address.Trim());
        command.Parameters.AddWithValue("district", profile.District.Trim());
        command.Parameters.AddWithValue("city", profile.City.Trim());
        command.Parameters.AddWithValue("email", (object?)profile.Email?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("updated_by", NpgsqlTypes.NpgsqlDbType.Uuid, (object?)updatedBy ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
