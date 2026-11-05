using System.Text.RegularExpressions;
using Xunit;

namespace ALKAROS.Architecture.Tests;

/// <summary>
/// The SQL rules the Python boundary audit cannot see: which schema a statement writes is checked there, but not
/// that one rule ("cancel a bill") is written once, nor that table rows are changed only through the Tables module.
/// </summary>
public static class HostForeignWriteLockTests
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    // The one statement that cancels bills lives in the Billing module's repository.
    private static readonly Regex BillCancelStatement = new(
        @"UPDATE\s+(billing\.bills|\{\w*BillsTable\})[^;]*'Cancelled'", Options);

    // Host files that still write table rows themselves (soft cache pointers and the sent-check marker). The list
    // may only shrink: a new writer goes through ITableReleasePort, and a file that stops writing leaves the list.
    private static readonly Dictionary<string, int> HostTableWriters = new()
    {
        ["src/Host/DualScreen/DualScreenStore.Orders.cs"] = 1,
        ["src/Host/Experience/NfcOrdering/NfcOrderingStore.cs"] = 1,
        ["src/Host/Experience/Orders/CashierHandoffStore.cs"] = 4,
        ["src/Host/Experience/Orders/PendingOrderConfirmation/PendingOrderConfirmationStore.cs"] = 1,
        ["src/Host/Experience/Orders/TableDraft/TableDraftService.cs"] = 1,
        ["src/Host/Experience/Tables/TableManagementStore.cs"] = 1,
    };

    private static readonly Regex TableRowWrite = new(
        @"\b(UPDATE|INSERT\s+INTO|DELETE\s+FROM)\s+table_mgmt\.(tables|table_sent_checks|table_merges)\b", Options);

    [Fact]
    public static void BillsAreCancelledByOneStatementInTheBillingModule()
    {
        var offenders = SourceFiles("src")
            .Where(f => BillCancelStatement.IsMatch(File.ReadAllText(Path.Combine(RepoRoot(), f))))
            .ToArray();

        Assert.Equal(["src/Modules/Billing/BillFoundation/PostgresBillRepository.cs"], offenders);
    }

    [Fact]
    public static void HostCodeChangesTableRowsOnlyOnTheKnownList()
    {
        var writers = SourceFiles("src/Host")
            .Select(f => (File: f, Count: TableRowWrite.Count(File.ReadAllText(Path.Combine(RepoRoot(), f)))))
            .Where(w => w.Count > 0)
            .ToDictionary(w => w.File, w => w.Count);

        Assert.Equal(
            HostTableWriters.OrderBy(p => p.Key, StringComparer.Ordinal),
            writers.OrderBy(p => p.Key, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("UPDATE table_mgmt.tables SET current_bill_id = NULL", true)]
    [InlineData("delete   from table_mgmt.table_sent_checks WHERE order_id = @id", true)]
    [InlineData("UPDATE table_mgmt.table_merges SET status = 'Unmerged'", true)]
    [InlineData("SELECT table_id FROM table_mgmt.tables", false)]
    [InlineData("UPDATE table_mgmt.zones SET name = @n", false)]
    public static void TheTableWritePatternSeesEveryWriteOfTheTableRows(string sql, bool expected)
        => Assert.Equal(expected, TableRowWrite.IsMatch(sql));

    [Theory]
    [InlineData("UPDATE billing.bills SET status = 'Cancelled', cancelled_at = @t", true)]
    [InlineData("UPDATE {BillsTable}\n            SET status = 'Cancelled'", true)]
    [InlineData("UPDATE billing.bills SET status = 'Paid'", false)]
    [InlineData("UPDATE invoicing.invoices SET status = 'Cancelled'", false)]
    [InlineData("UPDATE billing.bills SET cancelled_at = @t, status = 'Cancelled'", true)]
    [InlineData("UPDATE billing.bills SET status = CASE WHEN @x THEN 'Cancelled' ELSE status END", true)]
    [InlineData("UPDATE billing.bills SET row_version = row_version + 1; SELECT 'Cancelled'", false)]
    public static void TheBillCancelPatternSeesAnySpellingOfTheStatement(string sql, bool expected)
        => Assert.Equal(expected, BillCancelStatement.IsMatch(sql));

    private static IEnumerable<string> SourceFiles(string folder)
    {
        var root = RepoRoot();
        return Directory.GetFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(f => Path.GetRelativePath(root, f).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "ALKAROS.slnx")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("Could not locate the repository root.");
    }
}
