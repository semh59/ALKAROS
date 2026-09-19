using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Reconciliation.CaseFoundation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace ALKAROS.Host.Experience.Reconciliation;

/// <summary>
/// V1-RMD-250: found by an independent audit (2026-09-18) —
/// IReconciliationService (V1-REC-001: discrepancy case lifecycle,
/// deduplication, resolution) was domain-complete, DI-registered,
/// unit-tested, and had zero HTTP surface. Same "base filter authenticates +
/// reports.view, mutating handler escalates to a stronger permission" shape
/// as EndOfDayEndpoints (V1-RMD-249) — reading a case is reports.view
/// (Supervisor+), but opening/transitioning/annotating one requires
/// reconciliation.manage (also Supervisor+, but a distinct grant so a
/// read-only reporting role cannot mutate a case).
/// </summary>
public static class ReconciliationCaseEndpoints
{
    public const string ManagerCookieName = "alkaros.manager";
    public const string ViewPermission = ApplicationPermissions.ReportsView;
    public const string ManagePermission = ApplicationPermissions.ReconciliationManage;

    public static IServiceCollection AddReconciliationCaseExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<System.Data.Common.DbDataSource>(
            serviceProvider => serviceProvider.GetRequiredService<NpgsqlDataSource>());
        services.TryAddScoped<IReconciliationRepository, PostgresReconciliationRepository>();
        services.TryAddScoped<IReconciliationService, ReconciliationService>();

        services.TryAddScoped<IRoleRepository, PostgresRoleRepository>();
        services.TryAddScoped<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddScoped<IAuthorizationService, AuthorizationService>();
        services.TryAddScoped<ReconciliationCaseAuthentication>();
        return services;
    }

    public static RouteGroupBuilder MapReconciliationCaseApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup("/api/v1/management/reconciliation/cases");
        group.AddEndpointFilter<ReconciliationCaseEndpointFilter>();

        group.MapPost("/", async (
            CreateReconciliationCaseV1 request,
            IReconciliationService reconciliation,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = ReconciliationCaseEndpointFilter.RequireActorId(context);
            await authorization.AuthorizeAsync(actorId, ManagePermission, cancellationToken);
            var record = await reconciliation.CreateOrDeduplicateCaseAsync(
                new CreateCaseRequest(
                    request.DeduplicationKey, request.CaseType, request.SourceARef, request.SourceBRef,
                    request.DiscrepancyAmount, request.Severity, actorId, request.DetailsJson),
                cancellationToken);
            // CreateOrDeduplicateCaseAsync returns the existing case row
            // untouched when DeduplicationKey already matches an active
            // case, so this is deliberately 200 (an idempotent read-or-create),
            // never 201 — a second call is not a new resource.
            return Results.Ok(ReconciliationCaseV1.From(record));
        });

        group.MapPost("/{caseId:guid}/transition", async (
            Guid caseId,
            TransitionReconciliationCaseV1 request,
            IReconciliationService reconciliation,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = ReconciliationCaseEndpointFilter.RequireActorId(context);
            await authorization.AuthorizeAsync(actorId, ManagePermission, cancellationToken);
            var record = await reconciliation.TransitionCaseStatusAsync(
                new TransitionCaseStatusRequest(caseId, request.NewStatus, request.ExpectedVersion, actorId, request.ReasonOrNote),
                cancellationToken);
            return Results.Ok(ReconciliationCaseV1.From(record));
        });

        group.MapPost("/{caseId:guid}/notes", async (
            Guid caseId,
            AddReconciliationCaseNoteV1 request,
            IReconciliationService reconciliation,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = ReconciliationCaseEndpointFilter.RequireActorId(context);
            await authorization.AuthorizeAsync(actorId, ManagePermission, cancellationToken);
            await reconciliation.AddCaseNoteAsync(new AddCaseNoteRequest(caseId, request.Note, actorId), cancellationToken);
            return Results.Ok();
        });

        group.MapGet("/{caseId:guid}", async (
            Guid caseId,
            IReconciliationService reconciliation,
            CancellationToken cancellationToken) =>
        {
            var record = await reconciliation.GetCaseByIdAsync(caseId, cancellationToken)
                ?? throw new CaseNotFoundException(caseId);
            return Results.Ok(ReconciliationCaseV1.From(record));
        });

        group.MapGet("/{caseId:guid}/actions", async (
            Guid caseId,
            IReconciliationService reconciliation,
            CancellationToken cancellationToken) =>
        {
            var actions = await reconciliation.GetCaseActionsAsync(caseId, cancellationToken);
            return Results.Ok(actions.Select(CaseActionV1.From).ToArray());
        });

        group.MapGet("/", async (
            CaseStatus status,
            int? limit,
            IReconciliationService reconciliation,
            CancellationToken cancellationToken) =>
        {
            var cases = await reconciliation.GetCasesByStatusAsync(status, limit ?? 50, cancellationToken);
            return Results.Ok(cases.Select(ReconciliationCaseV1.From).ToArray());
        });

        return group;
    }
}

public sealed record CreateReconciliationCaseV1(
    string DeduplicationKey,
    CaseType CaseType,
    string SourceARef,
    string SourceBRef,
    decimal DiscrepancyAmount,
    CaseSeverity Severity,
    string? DetailsJson = null);

public sealed record TransitionReconciliationCaseV1(CaseStatus NewStatus, int ExpectedVersion, string? ReasonOrNote = null);

public sealed record AddReconciliationCaseNoteV1(string Note);

public sealed record ReconciliationCaseV1(
    Guid CaseId, string DeduplicationKey, CaseType CaseType, string SourceARef, string SourceBRef,
    decimal DiscrepancyAmount, CaseSeverity Severity, CaseStatus Status, DateTimeOffset OpenedAt,
    DateTimeOffset? ResolvedAt, int RowVersion, string? DetailsJson)
{
    public static ReconciliationCaseV1 From(ReconciliationCaseRecord record)
        => new(record.CaseId, record.DeduplicationKey, record.CaseType, record.SourceARef, record.SourceBRef,
            record.DiscrepancyAmount, record.Severity, record.Status, record.OpenedAt, record.ResolvedAt,
            record.RowVersion, record.DetailsJson);
}

public sealed record CaseActionV1(Guid ActionId, Guid CaseId, ActionType ActionType, Guid PerformedBy, DateTimeOffset PerformedAt, string? DetailsJson)
{
    public static CaseActionV1 From(CaseActionRecord record)
        => new(record.ActionId, record.CaseId, record.ActionType, record.PerformedBy, record.PerformedAt, record.DetailsJson);
}

public sealed class ReconciliationCaseAuthentication
{
    private readonly NpgsqlDataSource _dataSource;

    public ReconciliationCaseAuthentication(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<Guid> AuthenticateAsync(HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rawToken = context.Request.Cookies[ReconciliationCaseEndpoints.ManagerCookieName];
        var actorId = await ManagementSessionLookup.ResolveActorAsync(_dataSource, rawToken, allowSupervisor: true, cancellationToken);
        return actorId ?? throw new ReconciliationCaseUnauthorizedException();
    }
}

public sealed class ReconciliationCaseEndpointFilter : IEndpointFilter
{
    private const string ActorIdItemKey = "ReconciliationCaseActorId";

    private readonly ReconciliationCaseAuthentication _authentication;
    private readonly IAuthorizationService _authorization;

    public ReconciliationCaseEndpointFilter(ReconciliationCaseAuthentication authentication, IAuthorizationService authorization)
    {
        _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
    }

    public static Guid RequireActorId(HttpContext context)
        => context.Items[ActorIdItemKey] as Guid?
            ?? throw new InvalidOperationException($"{nameof(ReconciliationCaseEndpointFilter)} did not run before this endpoint.");

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            var actorId = await _authentication.AuthenticateAsync(context.HttpContext, context.HttpContext.RequestAborted);
            await _authorization.AuthorizeAsync(actorId, ReconciliationCaseEndpoints.ViewPermission, context.HttpContext.RequestAborted);
            context.HttpContext.Items[ActorIdItemKey] = actorId;
            return await next(context);
        }
        catch (ReconciliationCaseUnauthorizedException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (AuthorizationDeniedException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (ReconciliationException exception)
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
            ReconciliationCaseUnauthorizedException =>
                (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
            AuthorizationDeniedException =>
                (StatusCodes.Status403Forbidden, "FORBIDDEN", "Mutabakat vakası yönetimi için yeterli izin yok."),
            CaseNotFoundException =>
                (StatusCodes.Status404NotFound, "NOT_FOUND", "İstenen mutabakat vakası bulunamadı."),
            InvalidCaseStatusTransitionException =>
                (StatusCodes.Status409Conflict, "INVALID_OPERATION", "Bu durum geçişi şu anki vaka durumuyla uyumlu değil."),
            ReconciliationConcurrencyException =>
                (StatusCodes.Status409Conflict, "CONCURRENCY_CONFLICT", "Vaka başka bir işlem tarafından değiştirildi."),
            PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } =>
                (StatusCodes.Status409Conflict, "DUPLICATE_RESOURCE", "Aynı kimlikte bir kayıt zaten var."),
            ArgumentException or BadHttpRequestException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            NpgsqlException =>
                (StatusCodes.Status503ServiceUnavailable, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
            _ => throw exception,
        };
        return Results.Json(
            new ReconciliationCaseApiErrorEnvelopeV1(new ReconciliationCaseApiErrorV1(code, message, status, context.TraceIdentifier)),
            statusCode: status);
    }
}

public sealed record ReconciliationCaseApiErrorV1(string Code, string Message, int Status, string TraceId);

public sealed record ReconciliationCaseApiErrorEnvelopeV1(ReconciliationCaseApiErrorV1 Error);

public sealed class ReconciliationCaseUnauthorizedException : Exception
{
    public ReconciliationCaseUnauthorizedException() : base("A valid reconciliation case session is required.")
    {
    }
}
