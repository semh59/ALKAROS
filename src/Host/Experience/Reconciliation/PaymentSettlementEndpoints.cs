using ALKAROS.Identity.Authorization;
using ALKAROS.Payments.ManualResolution;
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

        // V1-RMD-283: every manual "the card WAS charged" claim with its slip number, amount, who claimed and who
        // decided. This is what a bank-statement match runs on: a manually approved payment is only as trustworthy
        // as its slip, so the list is always available to a manager (reports.view).
        group.MapGet("/manual-confirmations", async (
            string? status,
            int? limit,
            IManualCardConfirmationRepository confirmations,
            CancellationToken cancellationToken) =>
        {
            ManualCardConfirmationStatus? parsed = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<ManualCardConfirmationStatus>(status, ignoreCase: true, out var value))
                    return Results.BadRequest(new { error = new { code = "VALIDATION_FAILED", message = "Durum Pending, Approved ya da Rejected olmalı." } });
                parsed = value;
            }

            var items = await confirmations.ListAsync(parsed, limit ?? 100, cancellationToken);
            return Results.Ok(items.Select(item => new ManualCardConfirmationListItemV1(
                item.Id, item.PaymentId, item.BillId, item.SlipNumber, item.Amount, item.Status.ToString(),
                item.RequestedBy, item.RequestedAt, item.RequestNote, item.DecidedBy, item.DecidedAt, item.DecisionNote)));
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

public sealed record ManualCardConfirmationListItemV1(
    Guid ConfirmationId, Guid PaymentId, Guid BillId, string SlipNumber, decimal Amount, string Status,
    Guid RequestedBy, DateTimeOffset RequestedAt, string? RequestNote, Guid? DecidedBy, DateTimeOffset? DecidedAt, string? DecisionNote);
