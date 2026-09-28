using ALKAROS.TestHelpers;

namespace ALKAROS.CustomerData.Profiles.Tests.Fixtures;

public sealed class CustomerProfileTestDatabase : PgTestDatabase
{
    public CustomerProfileTestDatabase()
        : base("alkaros_cst001_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }

    public async Task<byte[]> RawEnvelopeBytesAsync(Guid customerId)
    {
        await using var cmd = DataSource.CreateCommand(
            "SELECT envelope_bytes FROM customer_data.profiles WHERE customer_id = @id;");
        cmd.Parameters.AddWithValue("id", customerId);
        var result = await cmd.ExecuteScalarAsync();
        return (byte[])result!;
    }
}
