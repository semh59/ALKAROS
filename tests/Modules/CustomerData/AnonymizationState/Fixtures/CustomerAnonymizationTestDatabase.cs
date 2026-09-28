using ALKAROS.TestHelpers;

namespace ALKAROS.CustomerData.AnonymizationState.Tests.Fixtures;

/// <summary>
/// Applies audit (migration 015), customer_data.profiles (159) and
/// customer_data.anonymization_requests (160) in that order - matches
/// `PgTestDatabase`'s own "*.up.sql" filename-sort convention.
/// </summary>
public sealed class CustomerAnonymizationTestDatabase : PgTestDatabase
{
    public CustomerAnonymizationTestDatabase()
        : base("alkaros_cst002_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }
}
