using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Reconciliation.CaseFoundation;
using ALKAROS.Reconciliation.OnlineOrders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace ALKAROS.Host.Experience.Reconciliation;

/// <summary>
/// V12-OUI-006: the online food screen's Problems tab, on the terminal's own staff session (the management reconciliation
/// console has its own session). Every active online order case is listed with what the screen needs to show it in
/// Turkish (<c>reports.view</c>); retrying its safe next action and resolving it (<c>reconciliation.manage</c>) go through
/// the same <see cref="OnlineOrderReconciliationActions"/> as the console, so the same version and source checks apply.
/// </summary>
public static class OnlineProblemsEndpoints
{
    public const string RoutePrefix = "/api/v1/terminals/{terminalId:guid}/online-problems";
    public const int MaxCases = 200;

    private static readonly CaseStatus[] ActiveStatuses = [CaseStatus.Open, CaseStatus.Investigating, CaseStatus.Escalated];

    private static readonly HashSet<string> RetryableActions = new(StringComparer.Ordinal)
    {
        OnlineOrderNextAction.ReprocessProviderEvent,
        OnlineOrderNextAction.ResendProviderCancellation,
        OnlineOrderNextAction.ResendProviderUpdate,
    };

    public static IServiceCollection AddOnlineProblemsExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<DualScreenStore>();
        return services;
    }

    public static RouteGroupBuilder MapOnlineProblemsApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup(RoutePrefix).WithTags("OnlineProblems").AddEndpointFilter<OnlineProblemsExceptionFilter>();

        group.MapGet("/", async (
            Guid terminalId,
            IReconciliationService reconciliation,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireAsync(context, terminalId, dualStore, authorization, ApplicationPermissions.ReportsView, cancellationToken);
            var problems = new List<OnlineProblemV1>();
            foreach (var status in ActiveStatuses)
            {
                foreach (var record in await reconciliation.GetCasesByStatusAsync(status, MaxCases, cancellationToken))
                {
                    if (record.CaseType != CaseType.OnlineOrderMismatch || OnlineOrderCaseDetails.TryParse(record.DetailsJson) is not { } details)
                        continue;
                    problems.Add(new OnlineProblemV1(
                        record.CaseId, details.Kind, details.Provider, details.ExternalOrderId, record.DiscrepancyAmount,
                        record.Severity.ToString(), record.Status.ToString(), record.OpenedAt, record.RowVersion, details.NextAction,
                        RetryableActions.Contains(details.NextAction)));
                }
            }

            return Results.Ok(problems.OrderByDescending(p => p.OpenedAt).ThenBy(p => p.CaseId).Take(MaxCases).ToList());
        }).RequireRateLimiting("terminal-read");

        group.MapPost("/{caseId:guid}/retry", async (
            Guid terminalId,
            Guid caseId,
            OnlineOrderReconciliationActions actions,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = await RequireAsync(context, terminalId, dualStore, authorization, ApplicationPermissions.ReconciliationManage, cancellationToken);
            var result = await actions.RetryAsync(caseId, actorId, cancellationToken);
            return result.Outcome switch
            {
                OnlineOrderRetryOutcome.Requeued => Results.Ok(new { outcome = result.Outcome.ToString() }),
                OnlineOrderRetryOutcome.NothingToRetry => Results.Ok(new { outcome = result.Outcome.ToString() }),
                OnlineOrderRetryOutcome.CaseNotFound => Refuse(404, "NOT_FOUND", "Sorun kaydı bulunamadı."),
                OnlineOrderRetryOutcome.NotAnOnlineOrderCase => Refuse(409, "NOT_AN_ONLINE_ORDER_CASE", "Bu kayıt bir online sipariş sorunu değil."),
                OnlineOrderRetryOutcome.CaseNotActive => Refuse(409, "CASE_NOT_ACTIVE", "Kapanmış bir sorun yeniden denenemez."),
                OnlineOrderRetryOutcome.NotRetryable => Refuse(409, "NOT_RETRYABLE", "Bu sorunda yeniden denenecek bir işlem yok; önerilen eylemi elle uygulayın."),
                _ => throw new InvalidOperationException($"Unhandled retry outcome '{result.Outcome}'."),
            };
        }).RequireRateLimiting("terminal-write");

        group.MapPost("/{caseId:guid}/resolve", async (
            Guid terminalId,
            Guid caseId,
            ResolveOnlineProblemRequestV1 request,
            OnlineOrderReconciliationActions actions,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = await RequireAsync(context, terminalId, dualStore, authorization, ApplicationPermissions.ReconciliationManage, cancellationToken);
            if (string.IsNullOrWhiteSpace(request.Note))
                return Refuse(400, "VALIDATION_FAILED", "Nasıl çözüldüğünü anlatan bir not yazın.");
            var result = await actions.ResolveAsync(caseId, request.ExpectedVersion, request.Note.Trim(), actorId, cancellationToken);
            return result.Outcome switch
            {
                OnlineOrderResolveOutcome.Resolved => Results.Ok(new { outcome = result.Outcome.ToString() }),
                OnlineOrderResolveOutcome.CaseNotFound => Refuse(404, "NOT_FOUND", "Sorun kaydı bulunamadı."),
                OnlineOrderResolveOutcome.NotAnOnlineOrderCase => Refuse(409, "NOT_AN_ONLINE_ORDER_CASE", "Bu kayıt bir online sipariş sorunu değil."),
                OnlineOrderResolveOutcome.StillDiverged => Refuse(409, "SOURCE_STILL_DIVERGED", "Sorun henüz ortadan kalkmadı; önce önerilen eylemi uygulayın."),
                _ => throw new InvalidOperationException($"Unhandled resolve outcome '{result.Outcome}'."),
            };
        }).RequireRateLimiting("terminal-write");

        return group;
    }

    private static IResult Refuse(int status, string code, string message) =>
        Results.Json(new { error = new { code, message } }, statusCode: status);

    private static async Task<Guid> RequireAsync(
        HttpContext context, Guid terminalId, DualScreenStore store, IAuthorizationService authorization, string permission,
        CancellationToken cancellationToken)
    {
        var cashierToken = context.Request.Cookies[DualScreenApplication.CashierCookieName];
        if (string.IsNullOrWhiteSpace(cashierToken))
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                cashierToken = authHeader["Bearer ".Length..].Trim();
        }

        var principal = await store.AuthenticateCashierAsync(cashierToken, terminalId, cancellationToken)
            ?? throw new DualScreenUnauthorizedException("Cashier authentication is required.");
        await authorization.AuthorizeAsync(principal.UserId, permission, cancellationToken);
        return principal.UserId;
    }
}

public sealed record OnlineProblemV1(
    Guid CaseId, string Kind, string? Provider, string? ExternalOrderId, decimal Amount, string Severity, string Status,
    DateTimeOffset OpenedAt, int RowVersion, string NextAction, bool CanRetry);

public sealed record ResolveOnlineProblemRequestV1(int ExpectedVersion, string? Note);

/// <summary>Turkish messages for every failure (docs/UI_STYLE_GUIDE.md §3).</summary>
public sealed class OnlineProblemsExceptionFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (Exception exception)
        {
            var (status, code, message) = exception switch
            {
                DualScreenUnauthorizedException => (401, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
                AuthorizationDeniedException => (403, "FORBIDDEN", "Bu işlem için yetkiniz yok."),
                ArgumentException or BadHttpRequestException => (400, "VALIDATION_FAILED", "İstek doğrulanamadı."),
                PostgresException or NpgsqlException => (503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
                _ => (500, "INTERNAL_ERROR", "İşlem tamamlanamadı."),
            };
            // An unexpected fault is left to the host's error handling, which logs it with its trace id.
            if (status == 500)
                throw;
            return Results.Json(new { error = new { code, message } }, statusCode: status);
        }
    }
}
