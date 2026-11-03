using System.Reflection;
using ALKAROS.Billing.Adjustments;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Host.Experience.Billing;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Payments.PaymentAggregate;
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

    // Every Billing mutation takes the bill row's FOR UPDATE lock before it reads, so a row-version race cannot be
    // produced over HTTP; the group's own mapping is asserted directly.
    [Theory]
    [InlineData(typeof(BillConcurrencyException), 409, "CONCURRENCY_CONFLICT")]
    [InlineData(typeof(OrderConcurrencyException), 409, "CONCURRENCY_CONFLICT")]
    [InlineData(typeof(PaymentConcurrencyException), 409, "CONCURRENCY_CONFLICT")]
    public void ARowVersionConflictIs409(Type exceptionType, int status, string code)
    {
        var (mappedStatus, mappedCode, message) = Map((Exception)Activator.CreateInstance(exceptionType, "detail")!);

        Assert.Equal((status, code), (mappedStatus, mappedCode));
        Assert.DoesNotContain("detail", message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnyOtherStateErrorStaysA400ThatSaysTheBillIsNotInAStateForIt()
        => Assert.Equal(
            (400, "VALIDATION_FAILED", "İstek hesabın şu anki durumuyla uyumlu değil."),
            Map(new InvalidOperationException("detail")));

    [Fact]
    public void ADiscountThatExceedsTheBillIsReportedAsThatNotAsASplitProblem()
        => Assert.Equal(
            (400, "DISCOUNT_EXCEEDS_PAYABLE", "İndirim toplamı hesap tutarını aşıyor."),
            Map(new DiscountExceedsPayableException("detail")));

    [Fact]
    public void AValidationErrorNamesItsFieldInsteadOfTheSplitRequest()
        => Assert.Equal(
            (400, "VALIDATION_FAILED", "İndirim tutarı geçersiz veya eksik."),
            Map(new ArgumentException("detail", "discountAmount")));
}
