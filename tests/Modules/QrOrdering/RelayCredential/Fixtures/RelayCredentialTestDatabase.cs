using ALKAROS.TestHelpers;
using NpgsqlTypes;

namespace ALKAROS.QrOrdering.RelayCredential.Tests.Fixtures;

public sealed class RelayCredentialTestDatabase : PgTestDatabase
{
    public RelayCredentialTestDatabase()
        : base("alkaros_qrt003_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }

    public async Task<byte[]> RawEnvelopeBytesAsync()
    {
        await using var cmd = DataSource.CreateCommand(
            "SELECT envelope_bytes FROM qr_ordering.relay_credentials WHERE credential_key = 'cloudflare_api_token';");
        var result = await cmd.ExecuteScalarAsync();
        return (byte[])result!;
    }
}
