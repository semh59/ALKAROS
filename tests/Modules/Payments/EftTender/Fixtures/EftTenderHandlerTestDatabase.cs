using ALKAROS.TestHelpers;

namespace ALKAROS.Payments.EftTender.Tests.Fixtures;

/// <summary>
/// Creates a unique test database for V13-PAY-005 and applies catalog,
/// tables, orders, billing, payments (120, 121) and payment_allocations
/// (123) migrations in order. Deliberately does NOT apply cash_sessions
/// (122) or cash_transactions (124/130) — an EFT tender has zero
/// relationship to either, and this test database's own migration set is
/// itself part of the proof.
/// </summary>
public sealed class EftTenderHandlerTestDatabase : PgTestDatabase
{
    public EftTenderHandlerTestDatabase()
        : base("alkaros_pay005_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
        {
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
        }
    }
}
