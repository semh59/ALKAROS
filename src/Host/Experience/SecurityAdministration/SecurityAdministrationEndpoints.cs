using ALKAROS.Audit.EventStore;
using ALKAROS.Identity.Authentication;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Host.Experience.Observability;
using ALKAROS.Host.Experience.SecurityAdministration.Maintenance;
using ALKAROS.Operations.OffsiteBackup;
using ALKAROS.Operations.RestoreVerification;
using ALKAROS.Security.IdentityHardening;
using ALKAROS.Security.SecretRotation;
using ALKAROS.Support.DiagnosticBundle;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace ALKAROS.Host.Experience.SecurityAdministration;

/// <summary>
/// V1-RMD-266: the security administration surface. AccountRecoveryService
/// (V15-SEC-002) was built, tested and DI-registered but no caller existed, so
/// nobody could sign a user out everywhere or clear a lockout. Every route here
/// requires a MANAGER session (never supervisor) holding security.manage, the
/// manager-exclusive grant added by migration 142. Later administration
/// operations mount on this same group and filter.
/// </summary>
public static class SecurityAdministrationEndpoints
{
    public const string ManagerCookieName = "alkaros.manager";
    public const string ManagePermission = ApplicationPermissions.SecurityManage;

    public static IServiceCollection AddSecurityAdministrationExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<IRoleRepository, PostgresRoleRepository>();
        services.TryAddScoped<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddScoped<IAuthorizationService, AuthorizationService>();
        services.TryAddSingleton<IAuditEventStore, PostgresAuditEventStore>();
        // Replaces the in-memory reference sink of the Security module: a
        // recovery action that vanishes on restart is not an audit trail.
        services.AddSingleton<ISuspiciousLoginAuditSink, AuditEventStoreSuspiciousLoginSink>();
        services.TryAddScoped<SecurityAdministrationAuthentication>();
        // The diagnostic bundle reads current health checks and redacts through the observability services.
        services.AddObservabilityExperience();

        // The Operations module defaults the restore drill to NpgsqlDataSource.ConnectionString,
        // which has no password; use the host's full connection string (or the explicit override).
        services.AddSingleton<IIsolatedRestoreDatabaseFactory>(provider => new NpgsqlIsolatedRestoreDatabaseFactory(
            Environment.GetEnvironmentVariable("ALKAROS_RESTORE_MAINTENANCE_CONNECTION")
                ?? provider.GetRequiredService<HostDatabaseConnection>().ConnectionString));

        // Scheduled and manager-triggerable operational jobs (V1-RMD-268 onward).
        services.TryAddSingleton<MaintenanceJobRunner>();
        services.TryAddSingleton<ALKAROS.Host.Experience.Orders.OrderSettlementService>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMaintenanceJob, RetentionSweepMaintenanceJob>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMaintenanceJob, OffsiteBackupMaintenanceJob>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMaintenanceJob, RestoreVerificationMaintenanceJob>());
        services.AddHostedService<MaintenanceJobHostedService>();
        return services;
    }

    public static RouteGroupBuilder MapSecurityAdministrationApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup("/api/v1/management/security");
        group.AddEndpointFilter<SecurityAdministrationEndpointFilter>();

        // V1-RMD-331 (independent 2026-09-26 audit, finding K10): revoke-sessions/force-unlock
        // above already existed but had no way for a manager to turn a username into the
        // userId they require — AccountRecoveryService's own doc comment deliberately keeps
        // username lookup out of ITS scope ("never a username lookup by a stranger"), but an
        // authorized manager already past this group's security.manage gate is not a
        // stranger. Read-only, reuses IUserStore.GetByUsernameAsync as-is.
        group.MapGet("/users/lookup", async (
            string username,
            IUserStore users,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var user = await users.GetByUsernameAsync(username, cancellationToken);
            return user is null
                ? Results.Json(
                    new SecurityAdministrationApiErrorEnvelopeV1(new SecurityAdministrationApiErrorV1(
                        "NOT_FOUND", "İstenen kullanıcı bulunamadı.", StatusCodes.Status404NotFound, context.TraceIdentifier)),
                    statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(new UserLookupResultV1(
                    user.UserId, user.DisplayName, user.Active, user.LockedUntil.HasValue && user.LockedUntil.Value > DateTimeOffset.UtcNow));
        });

        group.MapPost("/users/{userId:guid}/revoke-sessions", async (
            Guid userId,
            AccountRecoveryService recovery,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = SecurityAdministrationEndpointFilter.RequireActorId(context);
            var revoked = await recovery.RevokeAllSessionsAsync(userId, actorId.ToString("D"), cancellationToken);
            return Results.Ok(new RevokeSessionsResultV1(userId, revoked));
        });

        group.MapPost("/users/{userId:guid}/force-unlock", async (
            Guid userId,
            AccountRecoveryService recovery,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = SecurityAdministrationEndpointFilter.RequireActorId(context);
            var unlocked = await recovery.ForceUnlockAsync(userId, actorId.ToString("D"), cancellationToken);
            return unlocked
                ? Results.Ok(new ForceUnlockResultV1(userId, true))
                : Results.Json(
                    new SecurityAdministrationApiErrorEnvelopeV1(new SecurityAdministrationApiErrorV1(
                        "NOT_FOUND", "İstenen kullanıcı bulunamadı.", StatusCodes.Status404NotFound, context.TraceIdentifier)),
                    statusCode: StatusCodes.Status404NotFound);
        });

        // V1-RMD-405 (V1-RMD-399 N-2): offboarding. revoke-sessions alone let a leaver sign straight back in.
        group.MapPost("/users/{userId:guid}/deactivate", async (
            Guid userId,
            AccountRecoveryService recovery,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = SecurityAdministrationEndpointFilter.RequireActorId(context);
            if (actorId == userId)
            {
                return Results.Json(
                    new SecurityAdministrationApiErrorEnvelopeV1(new SecurityAdministrationApiErrorV1(
                        "SELF_DEACTIVATION", "Kendi hesabınızı pasifleştiremezsiniz.", StatusCodes.Status409Conflict, context.TraceIdentifier)),
                    statusCode: StatusCodes.Status409Conflict);
            }

            return await recovery.DeactivateAsync(userId, actorId.ToString("D"), cancellationToken)
                ? Results.Ok(new AccountActiveResultV1(userId, false))
                : Results.Json(
                    new SecurityAdministrationApiErrorEnvelopeV1(new SecurityAdministrationApiErrorV1(
                        "NOT_FOUND", "İstenen kullanıcı bulunamadı.", StatusCodes.Status404NotFound, context.TraceIdentifier)),
                    statusCode: StatusCodes.Status404NotFound);
        });

        group.MapPost("/users/{userId:guid}/reactivate", async (
            Guid userId,
            AccountRecoveryService recovery,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = SecurityAdministrationEndpointFilter.RequireActorId(context);
            return await recovery.ReactivateAsync(userId, actorId.ToString("D"), cancellationToken)
                ? Results.Ok(new AccountActiveResultV1(userId, true))
                : Results.Json(
                    new SecurityAdministrationApiErrorEnvelopeV1(new SecurityAdministrationApiErrorV1(
                        "NOT_FOUND", "İstenen kullanıcı bulunamadı.", StatusCodes.Status404NotFound, context.TraceIdentifier)),
                    statusCode: StatusCodes.Status404NotFound);
        });

        group.MapGet("/maintenance/jobs", (MaintenanceJobRunner runner) => Results.Ok(runner.GetStatuses()));

        group.MapPost("/maintenance/jobs/{name}/run", async (
            string name,
            MaintenanceJobRunner runner,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var status = await runner.RunAsync(name, cancellationToken);
            return status is null
                ? Results.Json(
                    new SecurityAdministrationApiErrorEnvelopeV1(new SecurityAdministrationApiErrorV1(
                        "NOT_FOUND", "İstenen bakım işi bulunamadı.", StatusCodes.Status404NotFound, context.TraceIdentifier)),
                    statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(status);
        });

        // What the off-site receipts prove per data class. A class with no receipt, or
        // whose newest receipt is older than its target, reports MeetsTarget=false: the
        // RPO shortfall is shown, never hidden.
        group.MapGet("/backup/rpo", async (IOffsiteBackupReceiptStore receipts, CancellationToken cancellationToken) =>
        {
            var all = await receipts.GetAllAsync(cancellationToken: cancellationToken);
            var now = DateTimeOffset.UtcNow;
            return Results.Ok(Enum.GetValues<DataClass>()
                .Select(dataClass => RpoCoverageChecker.Evaluate(dataClass, all, now))
                .Select(result => new BackupRpoStatusV1(
                    result.DataClass.ToString(),
                    (long)result.Target.TotalSeconds,
                    result.MeasuredGap is null ? null : (long)result.MeasuredGap.Value.TotalSeconds,
                    result.MeetsTarget)));
        });

        // The recorded restore drills (newest first): what proves the off-site copies can be restored.
        group.MapGet("/backup/restore-attempts", async (IRestoreAttemptStore attempts, CancellationToken cancellationToken) =>
        {
            var recorded = await attempts.GetByDataClassAsync(DataClass.Settings, 20, cancellationToken);
            return Results.Ok(recorded
                .OrderByDescending(attempt => attempt.StartedAtUtc)
                .Select(attempt => new RestoreAttemptV1(
                    attempt.ArtifactId, attempt.DataClass.ToString(), attempt.StartedAtUtc,
                    attempt.Duration.TotalSeconds, attempt.Succeeded, attempt.WithinRtoTarget,
                    attempt.IntegrityChecksPassed, attempt.IntegrityChecksTotal, attempt.FailureReason)));
        });

        group.MapPost("/diagnostic-bundle", async (
            DiagnosticBundleRequestV1 request,
            IDiagnosticBundleService bundles,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = SecurityAdministrationEndpointFilter.RequireActorId(context);
            try
            {
                var bundle = await bundles.GenerateAsync(
                    new DiagnosticBundleRequest(
                        actorId.ToString("D"),
                        request.CorrelationIds ?? [],
                        request.WindowStart,
                        request.WindowEnd,
                        request.Reason ?? string.Empty),
                    cancellationToken);
                return Results.Ok(bundle);
            }
            catch (DiagnosticBundleException exception)
            {
                var (status, code, message) = exception.Reason switch
                {
                    DiagnosticBundleFailureReason.NoCorrelationIdsProvided =>
                        (StatusCodes.Status400BadRequest, "NO_CORRELATION_IDS", "En az bir korelasyon kimliği seçilmelidir."),
                    DiagnosticBundleFailureReason.TimeWindowTooLarge =>
                        (StatusCodes.Status400BadRequest, "TIME_WINDOW_TOO_LARGE", "Zaman aralığı en fazla 30 gün olabilir."),
                    DiagnosticBundleFailureReason.SizeLimitExceeded =>
                        (StatusCodes.Status413PayloadTooLarge, "BUNDLE_TOO_LARGE", "Paket çok büyük; korelasyon kimliklerini veya zaman aralığını daraltın."),
                    _ => (StatusCodes.Status500InternalServerError, "INTERNAL_ERROR", "İşlem tamamlanamadı."),
                };
                return Results.Json(
                    new SecurityAdministrationApiErrorEnvelopeV1(new SecurityAdministrationApiErrorV1(code, message, status, context.TraceIdentifier)),
                    statusCode: status);
            }
        });

        group.MapSecretRotation();
        group.MapOrderBacklog();
        group.MapOutbox();

        return group;
    }
}

public sealed record RestoreAttemptV1(
    string ArtifactId, string DataClass, DateTimeOffset StartedAtUtc, double DurationSeconds,
    bool Succeeded, bool WithinRtoTarget, int IntegrityChecksPassed, int IntegrityChecksTotal, string? FailureReason);

public sealed record BackupRpoStatusV1(string DataClass, long TargetSeconds, long? MeasuredGapSeconds, bool MeetsTarget);

public sealed record DiagnosticBundleRequestV1(
    IReadOnlyList<string>? CorrelationIds,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    string? Reason);

public sealed record UserLookupResultV1(Guid UserId, string DisplayName, bool Active, bool IsLocked);

public sealed record RevokeSessionsResultV1(Guid UserId, int RevokedSessions);

public sealed record ForceUnlockResultV1(Guid UserId, bool Unlocked);

/// <summary>V1-RMD-405: the account's active flag after a deactivate/reactivate call.</summary>
public sealed record AccountActiveResultV1(Guid UserId, bool Active);

public sealed record SecurityAdministrationApiErrorV1(string Code, string Message, int Status, string TraceId);

public sealed record SecurityAdministrationApiErrorEnvelopeV1(SecurityAdministrationApiErrorV1 Error);

public sealed class SecurityAdministrationUnauthorizedException : Exception
{
    public SecurityAdministrationUnauthorizedException() : base("A valid manager session is required.")
    {
    }
}

public sealed class SecurityAdministrationAuthentication
{
    private readonly NpgsqlDataSource _dataSource;

    public SecurityAdministrationAuthentication(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<Guid> AuthenticateAsync(HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rawToken = context.Request.Cookies[SecurityAdministrationEndpoints.ManagerCookieName];
        var actorId = await ManagementSessionLookup.ResolveActorAsync(_dataSource, rawToken, allowSupervisor: false, cancellationToken);
        return actorId ?? throw new SecurityAdministrationUnauthorizedException();
    }
}

public sealed class SecurityAdministrationEndpointFilter : IEndpointFilter
{
    private const string ActorIdItemKey = "SecurityAdministrationActorId";

    private readonly SecurityAdministrationAuthentication _authentication;
    private readonly IAuthorizationService _authorization;

    public SecurityAdministrationEndpointFilter(
        SecurityAdministrationAuthentication authentication, IAuthorizationService authorization)
    {
        _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
    }

    public static Guid RequireActorId(HttpContext context)
        => context.Items[ActorIdItemKey] as Guid?
            ?? throw new InvalidOperationException($"{nameof(SecurityAdministrationEndpointFilter)} did not run before this endpoint.");

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            var actorId = await _authentication.AuthenticateAsync(context.HttpContext, context.HttpContext.RequestAborted);
            await _authorization.AuthorizeAsync(actorId, SecurityAdministrationEndpoints.ManagePermission, context.HttpContext.RequestAborted);
            context.HttpContext.Items[ActorIdItemKey] = actorId;
            return await next(context);
        }
        catch (SecurityAdministrationUnauthorizedException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (AuthorizationDeniedException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (SecretRotationConflictException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (SecretRotationConcurrencyException exception)
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
            SecurityAdministrationUnauthorizedException =>
                (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
            AuthorizationDeniedException =>
                (StatusCodes.Status403Forbidden, "FORBIDDEN", "Güvenlik yönetimi için yeterli izin yok."),
            SecretRotationConflictException =>
                (StatusCodes.Status409Conflict, "INVALID_OPERATION", "Bu işlem sürümün mevcut durumuyla uyumlu değil."),
            SecretRotationConcurrencyException =>
                (StatusCodes.Status409Conflict, "CONCURRENCY_CONFLICT", "Sürüm kaydı başka bir işlem tarafından değiştirildi."),
            ArgumentException or BadHttpRequestException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            NpgsqlException =>
                (StatusCodes.Status503ServiceUnavailable, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
            _ => throw exception,
        };
        return Results.Json(
            new SecurityAdministrationApiErrorEnvelopeV1(new SecurityAdministrationApiErrorV1(code, message, status, context.TraceIdentifier)),
            statusCode: status);
    }
}
