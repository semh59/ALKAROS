using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Behavioural;
using ALKAROS.Identity.Authorization.Grants;
using ALKAROS.Identity.Authorization.Offline;
using ALKAROS.Identity.Authorization.Policies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ALKAROS.Host.Experience.OfflineReconciliation;

/// <summary>
/// V1-IAM-025 (C3): the reconnect side of bounded offline authority
/// (docs/domain/authorization-model.md §5) — the engine has existed since
/// V1-IAM-022, but until this module nothing on a live request path called
/// <see cref="IOfflineGrantReconciler.ReconcileAsync"/>. A device that
/// authorized actions offline against its budget replays them here on
/// reconnect; every one is written to <c>identity.authorization_grants</c>
/// and flagged for manager review.
/// </summary>
public static class OfflineReconciliationEndpoints
{
    public const string RoutePrefix = "/api/v1/terminals/{terminalId:guid}/offline-reconciliation";

    public static IServiceCollection AddOfflineReconciliationExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<DualScreenStore>();
        services.TryAddScoped<IOfflineAuthorityBudgetRepository, PostgresOfflineAuthorityBudgetRepository>();
        services.TryAddScoped<IAuthorizationGrantRepository, PostgresAuthorizationGrantRepository>();
        services.TryAddScoped<IAuthorizationPolicyRepository, PostgresAuthorizationPolicyRepository>();
        services.TryAddScoped<IOfflineReplayLedger, PostgresOfflineReplayLedger>();
        services.TryAddScoped<IBehaviouralTighteningRepository, PostgresBehaviouralTighteningRepository>();
        services.TryAddScoped<IOfflineGrantReconciler, OfflineGrantReconciler>();
        services.TryAddScoped<IRoleRepository, PostgresRoleRepository>();
        services.TryAddTransient<OfflineReconciliationExceptionFilter>();
        return services;
    }

    public static RouteGroupBuilder MapOfflineReconciliationApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup(RoutePrefix)
            .WithTags("OfflineReconciliation")
            .AddEndpointFilter<OfflineReconciliationExceptionFilter>();

        group.MapPost("", async (
            Guid terminalId,
            ReconcileOfflineActionsRequest request,
            HttpContext context,
            DualScreenStore sessions,
            IOfflineAuthorityBudgetRepository budgets,
            IOfflineGrantReconciler reconciler,
            IRoleRepository roles,
            NpgsqlDataSource dataSource,
            CancellationToken cancellationToken) =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var cashierToken = context.Request.Cookies[DualScreenApplication.CashierCookieName];
            var principal = await sessions.AuthenticateCashierAsync(cashierToken, terminalId, cancellationToken)
                ?? throw new OfflineReconciliationUnauthorizedException();

            // Found by an independent audit (2026-09-06): a request's BudgetId and every action's RequesterUserId
            // must belong to the authenticated caller — a cashier who learned another employee's budgetId could
            // otherwise inject offline actions attributed to that employee. V1-RMD-404 (V1-RMD-399 H-05/H-06): the
            // role and the check's server are server facts too. A device claiming a role its user does not hold
            // is refused; the live-policy re-check and the own-check read the order's real server, not the
            // device's copy.
            var budget = await budgets.GetAsync(request.BudgetId, cancellationToken)
                ?? throw new UnknownOfflineAuthorityBudgetException(request.BudgetId);
            if (budget.UserId != principal.UserId)
                throw new OfflineReconciliationIdentityMismatchException();
            if (request.Actions.Any(action => action.RequesterUserId != principal.UserId))
                throw new OfflineReconciliationIdentityMismatchException();

            var roleIds = await roles.GetRoleIdsForUserAsync(principal.UserId, cancellationToken);
            var role = roleIds.Count > 0 ? await roles.GetByIdAsync(roleIds[0], cancellationToken) : null;
            if (role is null
                || request.Actions.Any(action => !string.Equals(action.RequesterRoleCode, role.Code, StringComparison.Ordinal)))
            {
                throw new OfflineReconciliationIdentityMismatchException();
            }

            var actions = new List<OfflineAuthorizedAction>(request.Actions.Count);
            foreach (var action in request.Actions)
            {
                var servingUserId = string.Equals(action.SubjectType, OrderItemSubject, StringComparison.Ordinal)
                    && action.SubjectId is { } itemId
                        ? await ServingUserOfOrderItemAsync(dataSource, itemId, cancellationToken)
                        : null;
                actions.Add(new OfflineAuthorizedAction(
                    action.IdempotencyKey,
                    action.PermissionCode,
                    action.RequesterUserId,
                    role.Code,
                    action.ReasonCode,
                    action.Amount,
                    action.OfflineAuthorizedAt,
                    action.SubjectType,
                    action.SubjectId,
                    servingUserId));
            }

            var results = await reconciler.ReconcileAsync(request.BudgetId, actions, cancellationToken);
            return Results.Ok(BuildResponse(results));
        });

        return group;
    }

    /// <summary>The offline action subject type that names one order item (V1-RMD-404).</summary>
    private const string OrderItemSubject = "OrderItem";

    private const string ServingUserOfOrderItemSql = """
        SELECT o.serving_user_id
        FROM orders.order_items AS i
        JOIN orders.orders AS o ON o.order_id = i.order_id
        WHERE i.order_item_id = @item_id;
        """;

    private static async Task<Guid?> ServingUserOfOrderItemAsync(
        NpgsqlDataSource dataSource, Guid itemId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(ServingUserOfOrderItemSql);
        command.Parameters.AddWithValue("item_id", itemId);
        return await command.ExecuteScalarAsync(cancellationToken) is Guid servingUserId ? servingUserId : null;
    }

    // Idea 1: turn a reconnect batch from an undifferentiated list of rows
    // into a prioritized brief a manager can act on without reading every
    // line — flagged-and-pending items (the requester has an active
    // behavioural-tightening signal, idea 2) surface first, then the rest
    // of what still needs review, then what was already resolved
    // automatically or is a pure replay. Deterministic and rule-based, not
    // a model call: the summary text is a fixed Turkish template over
    // these counts, so it needs no external service, no secret, and no
    // network egress from a device that may itself have just come back
    // online.
    private static OfflineReconciliationResponseV1 BuildResponse(IReadOnlyList<OfflineReconciliationResult> results)
    {
        var pendingCount = results.Count(r => r.Status == GrantStatus.Pending);
        var deniedCount = results.Count(r => r.Status == GrantStatus.Denied);
        var flaggedCount = results.Count(r => r.IsBehaviourallyFlagged);
        // Found by an independent audit (2026-09-15): this must be scoped to
        // Pending for BuildBrief's "bunlardan {flagged} tanesi..." clause —
        // it reads as "of these [pending] ones", but a flagged action can
        // just as well end up Denied (flagging never changes Admit/Deny,
        // see OfflineGrantReconciler.ReconcileAsync). Using the unscoped
        // count there produced a sentence claiming a flagged item was in
        // the pending bucket when it had actually been denied.
        var pendingFlaggedCount = results.Count(r => r.Status == GrantStatus.Pending && r.IsBehaviourallyFlagged);
        var replayCount = results.Count(r => r.IsReplay);

        var ordered = results
            .OrderByDescending(r => r.Status == GrantStatus.Pending && r.IsBehaviourallyFlagged)
            .ThenByDescending(r => r.Status == GrantStatus.Pending)
            .ThenByDescending(r => r.Status == GrantStatus.Denied)
            .Select(result => new OfflineReconciliationResultV1(
                result.IdempotencyKey, result.GrantId, result.Status.ToString(), result.Detail, result.IsBehaviourallyFlagged))
            .ToList();

        var summary = new OfflineReconciliationSummaryV1(
            TotalActions: results.Count,
            PendingReviewCount: pendingCount,
            FlaggedCount: flaggedCount,
            DeniedCount: deniedCount,
            AlreadyReconciledCount: replayCount,
            Brief: BuildBrief(results.Count, pendingCount, pendingFlaggedCount, deniedCount, replayCount));

        return new OfflineReconciliationResponseV1(summary, ordered);
    }

    private static string BuildBrief(
        int total, int pending, int pendingFlagged, int denied, int replayed)
    {
        if (total == 0)
            return "Çevrimdışı işlem bulunamadı.";

        var parts = new List<string> { $"{total} çevrimdışı işlem değerlendirildi" };
        if (pending > 0)
        {
            var pendingText = $"{pending} tanesi yönetici onayı bekliyor";
            if (pendingFlagged > 0)
                pendingText += $" (bunlardan {pendingFlagged} tanesi anormal kullanım sinyali taşıdığı için öncelikli)";
            parts.Add(pendingText);
        }
        if (denied > 0)
            parts.Add($"{denied} tanesi otomatik reddedildi");
        if (replayed > 0)
            parts.Add($"{replayed} tanesi daha önce işlenmişti (tekrar)");

        return string.Join(", ", parts) + ".";
    }
}

public sealed record ReconcileOfflineActionsRequest(
    Guid BudgetId, IReadOnlyList<OfflineAuthorizedActionV1> Actions);

public sealed record OfflineAuthorizedActionV1(
    string IdempotencyKey,
    string PermissionCode,
    Guid RequesterUserId,
    string RequesterRoleCode,
    string ReasonCode,
    decimal Amount,
    DateTimeOffset OfflineAuthorizedAt,
    string? SubjectType = null,
    Guid? SubjectId = null,
    Guid? SubjectServingUserId = null);

public sealed record OfflineReconciliationResultV1(
    string IdempotencyKey, Guid GrantId, string Status, string Detail, bool IsFlagged);

/// <summary>
/// Deterministic, rule-based rollup of one reconnect batch (idea 1) — see
/// <see cref="OfflineReconciliationEndpoints.BuildResponse"/>.
/// </summary>
public sealed record OfflineReconciliationSummaryV1(
    int TotalActions,
    int PendingReviewCount,
    int FlaggedCount,
    int DeniedCount,
    int AlreadyReconciledCount,
    string Brief);

public sealed record OfflineReconciliationResponseV1(
    OfflineReconciliationSummaryV1 Summary,
    IReadOnlyList<OfflineReconciliationResultV1> Results);

internal sealed class OfflineReconciliationExceptionFilter : IEndpointFilter
{
    private static readonly Action<ILogger, string, string, Exception?> LogRequestFailure =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(5300, nameof(LogRequestFailure)),
            "Offline reconciliation request failed on {Path} ({TraceIdentifier}).");

    private readonly ILogger<OfflineReconciliationExceptionFilter> _logger;

    public OfflineReconciliationExceptionFilter(ILogger<OfflineReconciliationExceptionFilter> logger)
    {
        _logger = logger;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext invocationContext, EndpointFilterDelegate next)
    {
        try
        {
            return await next(invocationContext);
        }
        catch (Exception exception)
        {
            var (status, code, message) = Map(exception);
            if (status >= StatusCodes.Status500InternalServerError)
            {
                LogRequestFailure(
                    _logger,
                    invocationContext.HttpContext.Request.Path,
                    invocationContext.HttpContext.TraceIdentifier,
                    exception);
            }

            return Results.Json(
                new OfflineReconciliationErrorV1(code, message, status, invocationContext.HttpContext.TraceIdentifier),
                statusCode: status);
        }
    }

    private static (int Status, string Code, string Message) Map(Exception exception) => exception switch
    {
        OfflineReconciliationUnauthorizedException =>
            (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Geçerli bir terminal oturumu gerekiyor."),
        UnknownOfflineAuthorityBudgetException =>
            (StatusCodes.Status404NotFound, "UNKNOWN_BUDGET", "Çevrimdışı yetki bütçesi bulunamadı; yeniden bağlanın."),
        OfflineReconciliationIdentityMismatchException =>
            (StatusCodes.Status403Forbidden, "IDENTITY_MISMATCH", "Bu çevrimdışı bütçe veya işlem başka bir kullanıcıya ait."),
        ArgumentException or ArgumentNullException or BadHttpRequestException =>
            (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "Uzlaştırma isteği doğrulanamadı."),
        PostgresException or NpgsqlException =>
            (StatusCodes.Status503ServiceUnavailable, "DATABASE_UNAVAILABLE", "Uzlaştırma tamamlanamadı."),
        _ => (StatusCodes.Status500InternalServerError, "INTERNAL_ERROR", "İşlem tamamlanamadı."),
    };
}

public sealed record OfflineReconciliationErrorV1(string Code, string Message, int Status, string TraceId);

public sealed class OfflineReconciliationUnauthorizedException : Exception
{
    public OfflineReconciliationUnauthorizedException()
        : base("A valid terminal-bound cashier session is required.")
    {
    }
}

public sealed class OfflineReconciliationIdentityMismatchException : Exception
{
    public OfflineReconciliationIdentityMismatchException()
        : base("The reconciled budget or one of its actions does not belong to the authenticated caller.")
    {
    }
}
