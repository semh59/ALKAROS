using ALKAROS.TestHelpers;

namespace ALKAROS.Invoicing.Qnb.CredentialRegistration.Tests.Fixtures;

public sealed class QnbCredentialTestDatabase : PgTestDatabase
{
    public QnbCredentialTestDatabase()
        : base("alkaros_qnb006_")
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
            "SELECT envelope_bytes FROM invoicing.qnb_credentials WHERE credential_key = 'qnb_efatura';");
        var result = await cmd.ExecuteScalarAsync();
        return (byte[])result!;
    }
}
