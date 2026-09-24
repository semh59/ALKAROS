using ALKAROS.Identity.Authorization;
using ALKAROS.Reconciliation.Payments;
using ALKAROS.Reporting.Payments;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ALKAROS.Host.Experience.Reconciliation;

/// <summary>
/// V1-RMD-265: PaymentSettlementReportService (V13-RPT-001) and
/// PaymentReconciliationScanner (V13-REC-001) were built, tested and
/// DI-registered but had no caller anywhere in the running host, so neither the
/// report nor the payment discrepancy scan could ever be reached. Same shape as
/// ReconciliationCaseEndpoints: the manager session plus reports.view is the
/// base gate, and the scan (which writes reconciliation cases) escalates to
/// reconciliation.manage.
/// </summary>
public static class PaymentSettlementEndpoints
{
    public static RouteGroupBuilder MapPaymentSettlementApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup("/api/v1/management/payments");
        group.AddEndpointFilter<ReconciliationCaseEndpointFilter>();

        group.MapGet("/settlement-report", async (
            DateOnly businessDate,
            Guid? terminalId,
            IPaymentSettlementReportService reports,
            CancellationToken cancellationToken) =>
        {
            var result = await reports.GetReportAsync(
                new PaymentSettlementReportFilter(businessDate, terminalId), cancellationToken);
            return Results.Ok(result);
        });

        group.MapPost("/reconciliation-scan", async (
            PaymentReconciliationScanner scanner,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = ReconciliationCaseEndpointFilter.RequireActorId(context);
            await authorization.AuthorizeAsync(actorId, ReconciliationCaseEndpoints.ManagePermission, cancellationToken);
            var results = await scanner.ScanAllAsync(cancellationToken);
            return Results.Ok(results);
        });

        return group;
    }
}
