using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Observability.AlertFoundation;
using ALKAROS.Observability.Foundation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace ALKAROS.Host.Experience.Observability;

/// <summary>
/// V1-RMD-251: found by an independent audit (2026-09-18) — IAlertService
/// (V1-ALT-001) and IObservabilityService's health-check surface
/// (V1-OBS-001) were domain-complete, DI-registered, unit-tested, and had
/// zero HTTP surface. Same "base filter authenticates + reports.view,
/// mutating handler escalates to a stronger permission" shape as
/// EndOfDayEndpoints/ReconciliationCaseEndpoints (V1-RMD-249/250).
/// IObservabilityService's own BeginCorrelationScope/AddTraceStep/
/// RedactPayload are in-process, DB-less helpers with no HTTP surface —
/// out of scope here, see the task's own doc.
/// </summary>
public static class ObservabilityEndpoints
{
    public const string ManagerCookieName = "alkaros.manager";
    public const string ViewPermission = ApplicationPermissions.ReportsView;
    public const string ManagePermission = ApplicationPermissions.ObservabilityManage;

    public static IServiceCollection AddObservabilityExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<System.Data.Common.DbDataSource>(
            serviceProvider => serviceProvider.GetRequiredService<NpgsqlDataSource>());
        services.TryAddSingleton<IRedactionHook, ObservabilityRedactionHook>();
        services.TryAddScoped<IHealthCheckRepository, PostgresHealthCheckRepository>();
        services.TryAddScoped<IObservabilityService, ObservabilityService>();
        services.TryAddScoped<IAlertRepository, PostgresAlertRepository>();
        services.TryAddScoped<IAlertService, AlertService>();

        services.TryAddScoped<IRoleRepository, PostgresRoleRepository>();
        services.TryAddScoped<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddScoped<IAuthorizationService, AuthorizationService>();
        services.TryAddScoped<ObservabilityAuthentication>();
        return services;
    }

    public static RouteGroupBuilder MapObservabilityApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup("/api/v1/management/observability");
        group.AddEndpointFilter<ObservabilityEndpointFilter>();

        group.MapPost("/alerts/raise", async (
            RaiseAlertV1 request,
            IAlertService alerts,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = ObservabilityEndpointFilter.RequireActorId(context);
            await authorization.AuthorizeAsync(actorId, ManagePermission, cancellationToken);
            var result = await alerts.RaiseAlertAsync(
                new RaiseAlertRequest(
                    request.AlertType, request.Severity, request.Title, request.Message,
                    request.DeduplicationKey, request.SourceReferenceType, request.SourceReferenceId,
                    actorId, request.PayloadJson),
                cancellationToken);
            return Results.Ok(AlertRaiseResultV1.From(result));
        });

        group.MapPost("/alerts/{alertId:guid}/acknowledge", async (
            Guid alertId,
            AlertActionV1 request,
            IAlertService alerts,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = ObservabilityEndpointFilter.RequireActorId(context);
            await authorization.AuthorizeAsync(actorId, ManagePermission, cancellationToken);
            var record = await alerts.AcknowledgeAlertAsync(
                new AcknowledgeAlertRequest(alertId, request.ExpectedRowVersion, actorId, request.Reason), cancellationToken);
            return Results.Ok(AlertV1.From(record));
        });

        group.MapPost("/alerts/{alertId:guid}/escalate", async (
            Guid alertId,
            AlertActionV1 request,
            IAlertService alerts,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = ObservabilityEndpointFilter.RequireActorId(context);
            await authorization.AuthorizeAsync(actorId, ManagePermission, cancellationToken);
            var record = await alerts.EscalateAlertAsync(
                new EscalateAlertRequest(alertId, request.ExpectedRowVersion, actorId, request.Reason), cancellationToken);
            return Results.Ok(AlertV1.From(record));
        });

        group.MapPost("/alerts/{alertId:guid}/suppress", async (
            Guid alertId,
            AlertActionV1 request,
            IAlertService alerts,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = ObservabilityEndpointFilter.RequireActorId(context);
            await authorization.AuthorizeAsync(actorId, ManagePermission, cancellationToken);
            var record = await alerts.SuppressAlertAsync(
                new SuppressAlertRequest(alertId, request.ExpectedRowVersion, actorId, request.Reason), cancellationToken);
            return Results.Ok(AlertV1.From(record));
        });

        group.MapPost("/alerts/{alertId:guid}/resolve", async (
            Guid alertId,
            ResolveAlertV1 request,
            IAlertService alerts,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = ObservabilityEndpointFilter.RequireActorId(context);
            await authorization.AuthorizeAsync(actorId, ManagePermission, cancellationToken);
            var record = await alerts.ResolveAlertAsync(
                new ResolveAlertRequest(alertId, request.ExpectedRowVersion, actorId, request.ResolutionReason), cancellationToken);
            return Results.Ok(AlertV1.From(record));
        });

        group.MapGet("/alerts/active", async (IAlertService alerts, CancellationToken cancellationToken) =>
        {
            var active = await alerts.GetActiveAlertsAsync(cancellationToken);
            return Results.Ok(active.Select(AlertV1.From).ToArray());
        });

        group.MapGet("/alerts/by-source", async (
            string sourceReferenceType,
            Guid sourceReferenceId,
            IAlertService alerts,
            CancellationToken cancellationToken) =>
        {
            var matches = await alerts.GetBySourceReferenceAsync(sourceReferenceType, sourceReferenceId, cancellationToken);
            return Results.Ok(matches.Select(AlertV1.From).ToArray());
        });

        group.MapGet("/alerts/{alertId:guid}", async (
            Guid alertId,
            IAlertService alerts,
            CancellationToken cancellationToken) =>
        {
            var record = await alerts.GetByIdAsync(alertId, cancellationToken)
                ?? throw new AlertNotFoundException(alertId);
            return Results.Ok(AlertV1.From(record));
        });

        group.MapGet("/alerts/{alertId:guid}/events", async (
            Guid alertId,
            IAlertService alerts,
            CancellationToken cancellationToken) =>
        {
            var events = await alerts.GetEventsAsync(alertId, cancellationToken);
            return Results.Ok(events.Select(AlertEventV1.From).ToArray());
        });

        group.MapPost("/health-checks", async (
            RecordHealthCheckV1 request,
            IObservabilityService observability,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = ObservabilityEndpointFilter.RequireActorId(context);
            await authorization.AuthorizeAsync(actorId, ManagePermission, cancellationToken);
            var record = await observability.RecordHealthCheckAsync(
                new RecordHealthCheckRequest(request.CheckType, request.Target, request.Status, request.RetentionPolicyId, request.DetailsJson),
                cancellationToken);
            return Results.Created(
                $"/api/v1/management/observability/health-checks/{record.HealthCheckId:D}", HealthCheckV1.From(record));
        });

        group.MapGet("/health-checks/unhealthy", async (IObservabilityService observability, CancellationToken cancellationToken) =>
        {
            var unhealthy = await observability.GetUnhealthyChecksAsync(cancellationToken);
            return Results.Ok(unhealthy.Select(HealthCheckV1.From).ToArray());
        });

        group.MapGet("/health-checks/by-target", async (
            string target,
            int? limit,
            IObservabilityService observability,
            CancellationToken cancellationToken) =>
        {
            var checks = await observability.GetLatestHealthChecksByTargetAsync(target, limit ?? 10, cancellationToken);
            return Results.Ok(checks.Select(HealthCheckV1.From).ToArray());
        });

        group.MapGet("/health-checks/{healthCheckId:guid}", async (
            Guid healthCheckId,
            IObservabilityService observability,
            CancellationToken cancellationToken) =>
        {
            var record = await observability.GetHealthCheckByIdAsync(healthCheckId, cancellationToken)
                ?? throw new HealthCheckNotFoundException(healthCheckId);
            return Results.Ok(HealthCheckV1.From(record));
        });

        return group;
    }
}

public sealed record RaiseAlertV1(
    string AlertType, AlertSeverity Severity, string Title, string Message,
    string? DeduplicationKey = null, string? SourceReferenceType = null,
    Guid? SourceReferenceId = null, string? PayloadJson = null);

public sealed record AlertActionV1(long ExpectedRowVersion, string? Reason = null);

public sealed record ResolveAlertV1(long ExpectedRowVersion, string ResolutionReason);

public sealed record AlertV1(
    Guid AlertId, string AlertType, AlertSeverity Severity, AlertStatus Status, string Title, string Message,
    string? DeduplicationKey, string? SourceReferenceType, Guid? SourceReferenceId, DateTimeOffset OpenedAt,
    DateTimeOffset? AcknowledgedAt, Guid? AcknowledgedBy, DateTimeOffset? ResolvedAt, Guid? ResolvedBy,
    string? ResolutionReason, long RowVersion)
{
    public static AlertV1 From(AlertRecord record)
        => new(record.AlertId, record.AlertType, record.Severity, record.Status, record.Title, record.Message,
            record.DeduplicationKey, record.SourceReferenceType, record.SourceReferenceId, record.OpenedAt,
            record.AcknowledgedAt, record.AcknowledgedBy, record.ResolvedAt, record.ResolvedBy,
            record.ResolutionReason, record.RowVersion);
}

public sealed record AlertRaiseResultV1(AlertV1 Alert, bool IsNewAlert, bool WasDeduplicated)
{
    public static AlertRaiseResultV1 From(AlertRaiseResult result)
        => new(AlertV1.From(result.Alert), result.IsNewAlert, result.WasDeduplicated);
}

public sealed record AlertEventV1(Guid AlertEventId, Guid AlertId, AlertEventType EventType, Guid? ActorId, string PayloadJson, DateTimeOffset CreatedAt)
{
    public static AlertEventV1 From(AlertEventRecord record)
        => new(record.AlertEventId, record.AlertId, record.EventType, record.ActorId, record.PayloadJson, record.CreatedAt);
}

public sealed record RecordHealthCheckV1(string CheckType, string Target, HealthStatus Status, string RetentionPolicyId, string? DetailsJson = null);

public sealed record HealthCheckV1(Guid HealthCheckId, string CheckType, string Target, HealthStatus Status, DateTimeOffset CheckedAt, string RetentionPolicyId, string? DetailsJson)
{
    public static HealthCheckV1 From(HealthCheckRecord record)
        => new(record.HealthCheckId, record.CheckType, record.Target, record.Status, record.CheckedAt, record.RetentionPolicyId, record.DetailsJson);
}

/// <summary>Not a domain exception — GetHealthCheckByIdAsync returns null on a miss (V1-OBS-001 has no NotFound exception of its own).</summary>
public sealed class HealthCheckNotFoundException : Exception
{
    public HealthCheckNotFoundException(Guid healthCheckId) : base($"Health check '{healthCheckId}' was not found.")
    {
        HealthCheckId = healthCheckId;
    }

    public Guid HealthCheckId { get; }
}

public sealed class ObservabilityAuthentication
{
    private readonly NpgsqlDataSource _dataSource;

    public ObservabilityAuthentication(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<Guid> AuthenticateAsync(HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rawToken = context.Request.Cookies[ObservabilityEndpoints.ManagerCookieName];
        var actorId = await ManagementSessionLookup.ResolveActorAsync(_dataSource, rawToken, allowSupervisor: true, cancellationToken);
        return actorId ?? throw new ObservabilityUnauthorizedException();
    }
}

public sealed class ObservabilityEndpointFilter : IEndpointFilter
{
    private const string ActorIdItemKey = "ObservabilityActorId";

    private readonly ObservabilityAuthentication _authentication;
    private readonly IAuthorizationService _authorization;

    public ObservabilityEndpointFilter(ObservabilityAuthentication authentication, IAuthorizationService authorization)
    {
        _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
    }

    public static Guid RequireActorId(HttpContext context)
        => context.Items[ActorIdItemKey] as Guid?
            ?? throw new InvalidOperationException($"{nameof(ObservabilityEndpointFilter)} did not run before this endpoint.");

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            var actorId = await _authentication.AuthenticateAsync(context.HttpContext, context.HttpContext.RequestAborted);
            await _authorization.AuthorizeAsync(actorId, ObservabilityEndpoints.ViewPermission, context.HttpContext.RequestAborted);
            context.HttpContext.Items[ActorIdItemKey] = actorId;
            return await next(context);
        }
        catch (ObservabilityUnauthorizedException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (AuthorizationDeniedException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (HealthCheckNotFoundException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (AlertException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (ObservabilityException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (PostgresException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (NpgsqlException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (ArgumentException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (BadHttpRequestException exception)
        {
            return MapError(context.HttpContext, exception);
        }
    }

    private static IResult MapError(HttpContext context, Exception exception)
    {
        var (status, code, message) = exception switch
        {
            ObservabilityUnauthorizedException =>
                (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
            AuthorizationDeniedException =>
                (StatusCodes.Status403Forbidden, "FORBIDDEN", "Gözlemlenebilirlik yönetimi için yeterli izin yok."),
            AlertNotFoundException =>
                (StatusCodes.Status404NotFound, "NOT_FOUND", "İstenen alarm bulunamadı."),
            HealthCheckNotFoundException =>
                (StatusCodes.Status404NotFound, "NOT_FOUND", "İstenen sağlık kontrolü bulunamadı."),
            InvalidAlertStateException =>
                (StatusCodes.Status409Conflict, "INVALID_OPERATION", "Bu işlem alarmın şu anki durumuyla uyumlu değil."),
            AlertConcurrencyException =>
                (StatusCodes.Status409Conflict, "CONCURRENCY_CONFLICT", "Alarm başka bir işlem tarafından değiştirildi."),
            UnapprovedRetentionPolicyException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "Onaylanmamış bir saklama politikası kimliği kullanıldı."),
            PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } =>
                (StatusCodes.Status409Conflict, "DUPLICATE_RESOURCE", "Aynı kimlikte bir kayıt zaten var."),
            ArgumentException or BadHttpRequestException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            NpgsqlException =>
                (StatusCodes.Status503ServiceUnavailable, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
            _ => throw exception,
        };
        return Results.Json(
            new ObservabilityApiErrorEnvelopeV1(new ObservabilityApiErrorV1(code, message, status, context.TraceIdentifier)),
            statusCode: status);
    }
}

public sealed record ObservabilityApiErrorV1(string Code, string Message, int Status, string TraceId);

public sealed record ObservabilityApiErrorEnvelopeV1(ObservabilityApiErrorV1 Error);

public sealed class ObservabilityUnauthorizedException : Exception
{
    public ObservabilityUnauthorizedException() : base("A valid observability session is required.")
    {
    }
}
