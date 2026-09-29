using System.Globalization;
using ALKAROS.Billing.PaymentClosure;
using ALKAROS.Cash.SessionLifecycle;
using ALKAROS.CustomerAccounts.AccountPayments;
using ALKAROS.CustomerAccounts.AccountReceipts;
using ALKAROS.CustomerAccounts.BillCharges;
using ALKAROS.CustomerAccounts.CashReceipts;
using ALKAROS.CustomerData.Profiles;
using ALKAROS.Host.Experience.CustomerAccounts;
using ALKAROS.Host.Experience.Orders;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;

namespace ALKAROS.Host.DualScreen;

/// <summary>
/// V1-RMD-442: the till's customer account surface. A bill is written to a customer's account through the V14-ACC-003
/// handler (credit terms from V1-RMD-440) and closes exactly like a cash or card tender (no fiscal document - the
/// planned V14-ACC-008 route waits on the fiscal decision). A customer pays their debt in cash into this terminal's
/// open drawer session (V14-ACC-005) and gets a receipt (V14-ACC-009). The customer's invoice tax identity is entered
/// here and shown masked (V1-RMD-453).
/// </summary>
public static partial class DualScreenApplication
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    public static void MapCustomerAccountApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var customers = app.MapGroup("/api/v1/terminals/{terminalId:guid}/customers").WithTags("CustomerAccounts");

        customers.MapGet("/", async (
            Guid terminalId,
            string? search,
            CustomerAccountsStore accounts,
            DualScreenStore store,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierPermissionAsync(context, terminalId, store, authorization, ApplicationPermissions.PaymentsTake, cancellationToken);
            return Results.Ok(await accounts.ListAsync(search, cancellationToken));
        }).RequireRateLimiting("terminal-read");

        customers.MapPost("/", async (
            Guid terminalId,
            CreateCustomerV1 request,
            CustomerAccountsStore accounts,
            DualScreenStore store,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierPermissionAsync(context, terminalId, store, authorization, ApplicationPermissions.PaymentsTake, cancellationToken);
            try
            {
                var created = await accounts.CreateAsync(request, cancellationToken);
                return Results.Created($"/api/v1/terminals/{terminalId:D}/customers/{created.CustomerId:D}", created);
            }
            catch (CustomerAccountValidationException exception)
            {
                return AccountError(context, StatusCodes.Status400BadRequest, "VALIDATION_FAILED", exception.TurkishMessage);
            }
        }).RequireRateLimiting("terminal-write");

        customers.MapPut("/{customerId:guid}/tax-identity", async (
            Guid terminalId,
            Guid customerId,
            UpdateCustomerTaxIdentityV1 request,
            CustomerAccountsStore accounts,
            DualScreenStore store,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierPermissionAsync(context, terminalId, store, authorization, ApplicationPermissions.PaymentsTake, cancellationToken);
            try
            {
                var updated = await accounts.UpdateTaxIdentityAsync(customerId, request, cancellationToken);
                return updated is null
                    ? AccountError(context, StatusCodes.Status404NotFound, "CUSTOMER_NOT_FOUND", "Müşteri bulunamadı.")
                    : Results.Ok(updated);
            }
            catch (CustomerAccountValidationException exception)
            {
                return AccountError(context, StatusCodes.Status400BadRequest, "VALIDATION_FAILED", exception.TurkishMessage);
            }
            catch (CustomerProfileConcurrencyException)
            {
                return AccountError(context, StatusCodes.Status409Conflict, "CONCURRENT_UPDATE",
                    "Müşteri kaydı başka bir kasada değişti. Listeyi yenileyip tekrar deneyin.");
            }
        }).RequireRateLimiting("terminal-write");

        customers.MapGet("/{customerId:guid}/statement", async (
            Guid terminalId,
            Guid customerId,
            CustomerAccountsStore accounts,
            DualScreenStore store,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierPermissionAsync(context, terminalId, store, authorization, ApplicationPermissions.PaymentsTake, cancellationToken);
            var statement = await accounts.GetStatementAsync(customerId, cancellationToken);
            return statement is null
                ? AccountError(context, StatusCodes.Status404NotFound, "CUSTOMER_NOT_FOUND", "Müşteri bulunamadı.")
                : Results.Ok(statement);
        }).RequireRateLimiting("terminal-read");

        app.MapPost("/api/v1/terminals/{terminalId:guid}/billing/bills/{billId:guid}/account-charge", async (
            Guid terminalId,
            Guid billId,
            AccountChargeRequestV1 request,
            IAccountChargeHandler handler,
            CustomerAccountsStore accounts,
            IBillClosureService billClosure,
            OrderSettlementService orderSettlement,
            DualScreenStore store,
            IAuthorizationService authorization,
            IHubContext<CustomerDisplayHub> customerDisplayHub,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashierPermissionAsync(
                context, terminalId, store, authorization, ApplicationPermissions.PaymentsTake, cancellationToken);
            AccountChargeResult result;
            try
            {
                result = await handler.HandleAsync(
                    new AccountChargeRequest(request.CustomerId, billId, request.Amount, request.IdempotencyKey, principal.UserId),
                    cancellationToken);
            }
            catch (AccountChargeException exception)
            {
                return MapAccountChargeError(context, exception);
            }
            catch (CustomerProfileNotFoundException)
            {
                return AccountError(context, StatusCodes.Status404NotFound, "CUSTOMER_NOT_FOUND", "Müşteri bulunamadı.");
            }

            var closed = await TryCloseBillAsync(billClosure, orderSettlement, billId, customerDisplayHub, terminalId, cancellationToken);
            var balance = (await accounts.GetAsync(request.CustomerId, cancellationToken))?.Balance ?? 0m;
            return Results.Ok(new AccountChargeResultV1(result.ApprovedAmount, closed, result.WasReplayed, balance));
        }).RequireRateLimiting("terminal-write");

        app.MapPost("/api/v1/terminals/{terminalId:guid}/cash-sessions/{cashSessionId:guid}/account-receipts", async (
            Guid terminalId,
            Guid cashSessionId,
            AccountReceiptRequestV1 request,
            ICashAccountReceiptHandler receiptHandler,
            IAccountReceiptService receipts,
            ICashSessionRepository sessionRepository,
            DualScreenStore store,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            // The cash enters this terminal's drawer: cash.drawer, payments.take and the session must be this terminal's.
            var principal = await RequireCashDrawerAsync(context, terminalId, store, authorization, cancellationToken);
            await RequireSessionOnTerminalAsync(sessionRepository, cashSessionId, terminalId, cancellationToken);
            await authorization.AuthorizeAsync(principal.UserId, ApplicationPermissions.PaymentsTake, cancellationToken);
            try
            {
                var received = await receiptHandler.ReceiveAsync(
                    new CashAccountReceiptRequest(request.CustomerId, cashSessionId, request.Amount, request.IdempotencyKey, principal.UserId),
                    cancellationToken);
                var receipt = await receipts.IssueAsync(
                    received.AccountPaymentId, $"receipt:{request.IdempotencyKey}", principal.UserId, cancellationToken);
                return Results.Ok(new AccountReceiptResultV1(
                    received.AccountPaymentId, receipt.Receipt.ReceiptNumber, received.Amount, received.BalanceAfter, received.WasReplayed));
            }
            catch (CashAccountReceiptException exception)
            {
                return MapAccountReceiptError(context, exception);
            }
            catch (AccountPaymentIdempotencyKeyReusedException)
            {
                return AccountError(context, StatusCodes.Status409Conflict, "TENDER_IDEMPOTENCY_KEY_REUSED",
                    "İşlem kimliği başka bir tahsilat için zaten kullanılmış.");
            }
        }).RequireRateLimiting("terminal-write");
    }

    private static IResult MapAccountChargeError(HttpContext context, AccountChargeException exception) => exception switch
    {
        AccountChargeCreditPolicyDeniedException denied => AccountError(context, StatusCodes.Status409Conflict, "CREDIT_DENIED",
            denied.Reason ?? "Bu müşteriye cari hesaptan borç yazılamaz."),
        AccountChargeCustomerAnonymizedException => AccountError(context, StatusCodes.Status404NotFound, "CUSTOMER_NOT_FOUND",
            "Müşteri bulunamadı."),
        AccountChargeBillNotFoundException => AccountError(context, StatusCodes.Status404NotFound, "BILL_NOT_FOUND",
            "Hesap bulunamadı."),
        AccountChargeUnsettledPaymentExistsException => AccountError(context, StatusCodes.Status409Conflict, "TENDER_UNSETTLED_PAYMENT_EXISTS",
            "Bu hesapta çözülmemiş bir kart ödemesi var; hesaba yazmadan önce kart ödemesinin sonucu netleştirilmeli."),
        AccountChargeIdempotencyKeyReusedException => AccountError(context, StatusCodes.Status409Conflict, "TENDER_IDEMPOTENCY_KEY_REUSED",
            "İşlem kimliği başka bir tahsilat için zaten kullanılmış."),
        _ => throw exception,
    };

    private static IResult MapAccountReceiptError(HttpContext context, CashAccountReceiptException exception) => exception switch
    {
        CashAccountReceiptOverpaymentException overpaid => AccountError(context, StatusCodes.Status409Conflict, "RECEIPT_EXCEEDS_BALANCE",
            $"Tahsilat, müşterinin {overpaid.Outstanding.ToString("N2", TurkishCulture)} TL borcunu aşıyor."),
        CashAccountReceiptSessionNotOpenException => AccountError(context, StatusCodes.Status409Conflict, "CLOSED_CASH_SESSION",
            "Kasa oturumu açık değil."),
        CashAccountReceiptCustomerNotFoundException => AccountError(context, StatusCodes.Status404NotFound, "CUSTOMER_NOT_FOUND",
            "Müşteri bulunamadı."),
        CashAccountReceiptCurrencyNotSupportedException => AccountError(context, StatusCodes.Status400BadRequest, "VALIDATION_FAILED",
            "Yalnız Türk lirası tahsilat alınabilir."),
        _ => throw exception,
    };

    private static IResult AccountError(HttpContext context, int status, string code, string message)
        => Results.Json(new ApiErrorEnvelope(new ApiError(code, message, status, context.TraceIdentifier)), statusCode: status);
}
