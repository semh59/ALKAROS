using ALKAROS.Host.DualScreen;
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
        services.TryAddScoped<IOfflineGrantReconciler, OfflineGrantReconciler>();
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
            IOfflineGrantReconciler reconciler,
            CancellationToken cancellationToken) =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var cashierToken = context.Request.Cookies[DualScreenApplication.CashierCookieName];
            _ = await sessions.AuthenticateCashierAsync(cashierToken, terminalId, cancellationToken)
                ?? throw new OfflineReconciliationUnauthorizedException();

            var actions = request.Actions
                .Select(action => new OfflineAuthorizedAction(
                    action.IdempotencyKey,
                    action.PermissionCode,
                    action.RequesterUserId,
                    action.RequesterRoleCode,
                    action.ReasonCode,
                    action.Amount,
                    action.OfflineAuthorizedAt,
                    action.SubjectType,
                    action.SubjectId,
                    action.SubjectServingUserId))
                .ToList();

            var results = await reconciler.ReconcileAsync(request.BudgetId, actions, cancellationToken);
            return Results.Ok(results.Select(result => new OfflineReconciliationResultV1(
                result.IdempotencyKey, result.GrantId, result.Status.ToString(), result.Detail)));
        });

        return group;
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
    string IdempotencyKey, Guid GrantId, string Status, string Detail);

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
        ArgumentException or ArgumentNullException =>
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
