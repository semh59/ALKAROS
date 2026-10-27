using System.Reflection;
using ALKAROS.Host.Experience.Billing;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.Billing.Tests;

public sealed class BillingSplitErrorMappingTests
{
    private static (int Status, string Code, string Message) Map(Exception exception)
    {
        var filter = typeof(BillingSplitApplication).Assembly.GetType("ALKAROS.Host.Experience.Billing.BillingSplitExceptionFilter")!;
        var map = filter.GetMethod("Map", BindingFlags.NonPublic | BindingFlags.Static)!;
        return ((int, string, string))map.Invoke(null, [exception])!;
    }

    [Fact]
    public void ACancelledRequestIsNotAServerFault()
    {
        var (status, code, _) = Map(new OperationCanceledException());

        Assert.Equal(499, status);
        Assert.Equal("REQUEST_CANCELLED", code);
    }

    [Theory]
    [InlineData("40P01")]
    [InlineData("55P03")]
    public void ADeadlockOrALockTimeoutAsksTheCashierToRetryInsteadOfShowingADatabaseFault(string sqlState)
    {
        var (status, code, message) = Map(new PostgresException("lock", "ERROR", "ERROR", sqlState));

        Assert.Equal(503, status);
        Assert.Equal("BILL_BUSY", code);
        Assert.Contains("tekrar deneyin", message);
    }

    [Fact]
    public void AnyOtherDatabaseFaultStaysADatabaseUnavailableAnswer()
    {
        var (status, code, _) = Map(new PostgresException("down", "ERROR", "ERROR", "57P01"));

        Assert.Equal(503, status);
        Assert.Equal("DATABASE_UNAVAILABLE", code);
    }
}
