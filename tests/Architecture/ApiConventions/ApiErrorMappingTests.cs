using System.Text.RegularExpressions;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Cash.Contracts;
using ALKAROS.Host.Composition.Errors;
using ALKAROS.Host.DualScreen;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Payments.PaymentAggregate;
using Xunit;

namespace ALKAROS.Architecture.ApiConventions.Tests;

/// <summary>
/// api-contract-standard.md fixes which failure is a 400, a 404 and a 409. The exception types the cashier
/// paths throw are resolved through the one Host handler; a validation error stays a 400 that names its field, a missing
/// record stays a 404 and an optimistic-lock conflict is a 409 CONCURRENCY_CONFLICT instead of the 400 or 500 it was.
/// </summary>
public sealed partial class ApiErrorMappingTests
{
    private const string Conflict = "Kayıt başka bir işlem tarafından değiştirildi; sayfayı yenileyip tekrar deneyin.";

    public static TheoryData<Exception, int, string, string> CashierPathFailures => new()
    {
        { new ArgumentException("detail", "billId"), 400, "VALIDATION_FAILED", "Hesap geçersiz veya eksik." },
        { new ArgumentOutOfRangeException("quantity", "detail"), 400, "VALIDATION_FAILED", "Adet izin verilen aralığın dışında." },
        { new DualScreenNotFoundException("detail"), 404, "NOT_FOUND", "İstenen kayıt bulunamadı." },
        { new CashSessionNotFoundException(Guid.NewGuid()), 404, "CASH_SESSION_NOT_FOUND", "Kasa oturumu bulunamadı." },
        { new BillConcurrencyException("detail"), 409, "CONCURRENCY_CONFLICT", Conflict },
        { new CashSessionConcurrencyException("detail"), 409, "CONCURRENCY_CONFLICT", Conflict },
        { new OrderConcurrencyException("detail"), 409, "CONCURRENCY_CONFLICT", Conflict },
        { new PaymentConcurrencyException("detail"), 409, "CONCURRENCY_CONFLICT", Conflict },
    };

    [Theory]
    [MemberData(nameof(CashierPathFailures))]
    public void ACashierPathFailureMapsToItsStatusCodeAndTurkishMessage(Exception exception, int status, string code, string message)
    {
        var mapped = ApiExceptionHandler.Resolve([], exception);

        Assert.Equal((status, code, message), mapped);
        Assert.Matches(StableCode(), mapped.Code);
    }

    [Fact]
    public void AnOrdinaryInvalidOperationIsNeverReportedAsAConflict()
        => Assert.Equal(500, ApiExceptionHandler.Resolve([], new InvalidOperationException("detail")).Status);

    [GeneratedRegex("^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$")]
    private static partial Regex StableCode();
}
