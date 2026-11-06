using System.Reflection;
using System.Text.RegularExpressions;
using ALKAROS.Billing.BillFoundation;
using Xunit;

namespace ALKAROS.Host.Experience.Billing.Tests;

/// <summary>
/// Modules may not reference each other, so the "this bill belongs to this order" condition is repeated in Tables and
/// Reporting. These tests keep every copy the same condition as <see cref="BillOrderLink.Matches"/>.
/// </summary>
public sealed class BillOrderLinkCopiesTests
{
    private static string Normalize(string sql) =>
        Regex.Replace(Regex.Replace(sql, @"\s+", " "), @"\(\s+|\s+\)", match => match.Value.Trim()).Trim();

    [Theory]
    [InlineData("billing.bills", "sc.order_id")]
    [InlineData("billing.bills", "@next_order_id")]
    [InlineData("b", "o.order_id")]
    public void TheTablesCopyIsTheBillingCondition(string bill, string order)
    {
        var tables = Assembly.Load("ALKAROS.Tables").GetType("ALKAROS.Tables.TableRelease.TableBillOrderLink", throwOnError: true)!;

        var copy = (string)tables.GetMethod("Matches", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [bill, order])!;

        Assert.Equal(Normalize(BillOrderLink.Matches(bill, order)), Normalize(copy));
    }

    [Theory]
    [InlineData("rb", "rbi", "rbo")]
    [InlineData("pb", "pbi", "pbo")]
    public void TheBusinessDayReportCopiesAreTheBillingCondition(string bill, string billItem, string orderItem)
    {
        var reader = Assembly.Load("ALKAROS.Reporting").GetType("ALKAROS.Reporting.BusinessDayTotals.PostgresBusinessDayTotalsReader", throwOnError: true)!;
        var sql = (string)reader.GetField("Sql", BindingFlags.NonPublic | BindingFlags.Static)!.GetRawConstantValue()!;
        var canonical = Regex.Replace(Regex.Replace(BillOrderLink.Matches(bill, "o.order_id"), @"\bbli\b", billItem), @"\boli\b", orderItem);

        Assert.Contains(Normalize(canonical), Normalize(sql));
        Assert.Equal(2, Regex.Count(sql, "billing.bill_items"));
    }
}
