using ALKAROS.Identity.Authorization;
using ALKAROS.Reconciliation.OnlineOrders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ALKAROS.Host.Experience.Reconciliation;

/// <summary>
/// V12-REC-001: the online order reconciliation scan and the two case actions (retry the safe next action,
/// resolve after the source is checked again). Same gate as the payment scan: the manager session plus
/// reports.view, escalated to reconciliation.manage because every call here writes. Listing and reading
/// cases stays on the V1-REC-001 case API.
/// </summary>
public static class OnlineOrderReconciliationEndpoints
{
    /// <summary>Binds the reconciliation module's reprocessing port to the OnlineOrdering inbox contract.</summary>
    public static IServiceCollection AddOnlineOrderReconciliationExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IProviderEventReprocessing, OnlineOrderingInboxReprocessing>();
        return services;
    }

    public static RouteGroupBuilder MapOnlineOrderReconciliationApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup("/api/v1/management/reconciliation/online-orders");
        group.AddEndpointFilter<ReconciliationCaseEndpointFilter>();

        group.MapPost("/scan", async (
            OnlineOrderReconciliationScanner scanner,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = ReconciliationCaseEndpointFilter.RequireActorId(context);
            await authorization.AuthorizeAsync(actorId, ReconciliationCaseEndpoints.ManagePermission, cancellationToken);
            return Results.Ok(await scanner.ScanAllAsync(cancellationToken));
        });

        group.MapPost("/cases/{caseId:guid}/retry", async (
            Guid caseId,
            OnlineOrderReconciliationActions actions,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = ReconciliationCaseEndpointFilter.RequireActorId(context);
            await authorization.AuthorizeAsync(actorId, ReconciliationCaseEndpoints.ManagePermission, cancellationToken);
            var result = await actions.RetryAsync(caseId, actorId, cancellationToken);
            return result.Outcome switch
            {
                OnlineOrderRetryOutcome.Requeued or OnlineOrderRetryOutcome.NothingToRetry =>
                    Results.Ok(new OnlineOrderRetryResultV1(result.Outcome.ToString(), result.NextAction)),
                OnlineOrderRetryOutcome.CaseNotFound => Error(context, StatusCodes.Status404NotFound, "NOT_FOUND", "İstenen mutabakat vakası bulunamadı."),
                OnlineOrderRetryOutcome.NotAnOnlineOrderCase => Error(context, StatusCodes.Status409Conflict, "NOT_AN_ONLINE_ORDER_CASE", "Bu vaka bir online sipariş vakası değil."),
                OnlineOrderRetryOutcome.CaseNotActive => Error(context, StatusCodes.Status409Conflict, "CASE_NOT_ACTIVE", "Kapanmış bir vaka yeniden denenemez."),
                OnlineOrderRetryOutcome.NotRetryable => Error(context, StatusCodes.Status409Conflict, "NOT_RETRYABLE", "Bu vakada yeniden denenecek bir işlem yok; önerilen eylemi elle uygulayın."),
                _ => throw new InvalidOperationException($"Unhandled retry outcome '{result.Outcome}'."),
            };
        });

        group.MapPost("/cases/{caseId:guid}/resolve", async (
            Guid caseId,
            OnlineOrderResolveRequestV1 request,
            OnlineOrderReconciliationActions actions,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = ReconciliationCaseEndpointFilter.RequireActorId(context);
            await authorization.AuthorizeAsync(actorId, ReconciliationCaseEndpoints.ManagePermission, cancellationToken);
            if (request is null || string.IsNullOrWhiteSpace(request.Note))
                return Error(context, StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "Çözüm için nasıl kapatıldığını anlatan bir not gerekli.");

            var result = await actions.ResolveAsync(caseId, request.ExpectedVersion, request.Note, actorId, cancellationToken);
            return result.Outcome switch
            {
                OnlineOrderResolveOutcome.Resolved => Results.Ok(ReconciliationCaseV1.From(result.Case!)),
                OnlineOrderResolveOutcome.CaseNotFound => Error(context, StatusCodes.Status404NotFound, "NOT_FOUND", "İstenen mutabakat vakası bulunamadı."),
                OnlineOrderResolveOutcome.NotAnOnlineOrderCase => Error(context, StatusCodes.Status409Conflict, "NOT_AN_ONLINE_ORDER_CASE", "Bu vaka bir online sipariş vakası değil."),
                OnlineOrderResolveOutcome.StillDiverged => Error(context, StatusCodes.Status409Conflict, "SOURCE_STILL_DIVERGED", "Kaynaklar hâlâ uyuşmuyor; önce önerilen eylemi uygulayın."),
                _ => throw new InvalidOperationException($"Unhandled resolve outcome '{result.Outcome}'."),
            };
        });

        return group;
    }

    private static IResult Error(HttpContext context, int status, string code, string message) =>
        Results.Json(
            new ReconciliationCaseApiErrorEnvelopeV1(new ReconciliationCaseApiErrorV1(code, message, status, context.TraceIdentifier)),
            statusCode: status);
}

public sealed record OnlineOrderResolveRequestV1(int ExpectedVersion, string Note);

public sealed record OnlineOrderRetryResultV1(string Outcome, string? NextAction);
