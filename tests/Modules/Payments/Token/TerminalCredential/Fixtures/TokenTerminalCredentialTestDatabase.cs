using ALKAROS.TestHelpers;

namespace ALKAROS.Payments.Token.TerminalCredential.Tests.Fixtures;

public sealed class TokenTerminalCredentialTestDatabase : PgTestDatabase
{
    public TokenTerminalCredentialTestDatabase()
        : base("alkaros_hug005_")
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
            "SELECT envelope_bytes FROM payments.token_terminal_credentials WHERE credential_key = 'token_terminal';");
        var result = await cmd.ExecuteScalarAsync();
        return (byte[])result!;
    }
}
