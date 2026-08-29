using ALKAROS.Billing.BillFoundation;
using ALKAROS.Billing.SplitDesign;
using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ALKAROS.Host.Experience.Billing;

public static class BillingSplitApplication
{
    public const string MutationPermission = DualScreenApplication.CashierMutationPermission;
    public const string RoutePrefix = "/api/v1/terminals/{terminalId:guid}/billing/bills/{billId:guid}/split-design";

    public static IServiceCollection AddBillingSplitExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<DualScreenStore>();
        services.TryAddSingleton<IRoleRepository, PostgresRoleRepository>();
        services.TryAddSingleton<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddSingleton<IAuthorizationService, AuthorizationService>();
        services.TryAddSingleton<IBillRepository, PostgresBillRepository>();
        services.TryAddSingleton<ISplitDesignRepository, PostgresSplitDesignRepository>();
        services.TryAddSingleton<BillingSplitStore>();
        services.TryAddSingleton<IBillingSplitSessionAuthorizer, BillingSplitSessionAuthorizer>();
        services.TryAddTransient<BillingSplitExceptionFilter>();
        return services;
    }

    public static RouteGroupBuilder MapBillingSplitApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup(RoutePrefix)
            .WithTags("BillingSplitDesign")
            .AddEndpointFilter<BillingSplitExceptionFilter>();

        group.MapGet("", async (
            Guid terminalId,
            Guid billId,
            IBillingSplitSessionAuthorizer authorizer,
            BillingSplitStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            return Results.Ok(await store.GetAsync(billId, principal.CanMutate, cancellationToken));
        });

        group.MapPost("/from-order/{orderId:guid}", async (
            Guid terminalId,
            Guid billId,
            Guid orderId,
            IBillingSplitSessionAuthorizer authorizer,
            BillingSplitStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireMutationAsync(context, terminalId, cancellationToken);
            return Results.Ok(await store.CreateBillFromOrderAsync(orderId, principal.CanMutate, cancellationToken));
        });

        group.MapPut("/equal", async (
            Guid terminalId,
            Guid billId,
            SaveEqualSplitRequest request,
            IBillingSplitSessionAuthorizer authorizer,
            BillingSplitStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireMutationAsync(context, terminalId, cancellationToken);
            return Results.Ok(await store.SaveEqualAsync(billId, request, principal.UserId, cancellationToken));
        });

        group.MapPut("/items", async (
            Guid terminalId,
            Guid billId,
            SaveItemSplitRequest request,
            IBillingSplitSessionAuthorizer authorizer,
            BillingSplitStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireMutationAsync(context, terminalId, cancellationToken);
            return Results.Ok(await store.SaveItemsAsync(billId, request, principal.UserId, cancellationToken));
        });

        group.MapPut("/amounts", async (
            Guid terminalId,
            Guid billId,
            SaveAmountSplitRequest request,
            IBillingSplitSessionAuthorizer authorizer,
            BillingSplitStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireMutationAsync(context, terminalId, cancellationToken);
            return Results.Ok(await store.SaveAmountsAsync(billId, request, principal.UserId, cancellationToken));
        });

        group.MapPut("/custom", async (
            Guid terminalId,
            Guid billId,
            SaveCustomSplitRequest request,
            IBillingSplitSessionAuthorizer authorizer,
            BillingSplitStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireMutationAsync(context, terminalId, cancellationToken);
            return Results.Ok(await store.SaveCustomAsync(billId, request, principal.UserId, cancellationToken));
        });

        group.MapPost("/clear", async (
            Guid terminalId,
            Guid billId,
            ClearSplitDesignRequest request,
            IBillingSplitSessionAuthorizer authorizer,
            BillingSplitStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await authorizer.RequireMutationAsync(context, terminalId, cancellationToken);
            return Results.Ok(await store.ClearAsync(billId, request, cancellationToken));
        });

        return group;
    }
}

internal sealed record BillingSplitPrincipal(Guid UserId, bool CanMutate);

internal interface IBillingSplitSessionAuthorizer
{
    Task<BillingSplitPrincipal> RequireReadAsync(HttpContext context, Guid terminalId, CancellationToken cancellationToken);

    Task<BillingSplitPrincipal> RequireMutationAsync(HttpContext context, Guid terminalId, CancellationToken cancellationToken);
}

internal sealed class BillingSplitSessionAuthorizer : IBillingSplitSessionAuthorizer
{
    private readonly DualScreenStore _sessions;
    private readonly IRoleRepository _roles;
    private readonly IAuthorizationService _authorization;

    public BillingSplitSessionAuthorizer(
        DualScreenStore sessions,
        IRoleRepository roles,
        IAuthorizationService authorization)
    {
        _sessions = sessions;
        _roles = roles;
        _authorization = authorization;
    }

    public async Task<BillingSplitPrincipal> RequireReadAsync(
        HttpContext context,
        Guid terminalId,
        CancellationToken cancellationToken)
    {
        if (terminalId == Guid.Empty)
            throw new ArgumentException("Terminal ID cannot be empty.", nameof(terminalId));
        var token = context.Request.Cookies[DualScreenApplication.CashierCookieName];
        var cashier = await _sessions.AuthenticateCashierAsync(token, terminalId, cancellationToken)
            ?? throw new BillingSplitUnauthorizedException("A valid terminal-bound cashier session is required.");
        var permissions = await _roles.GetPermissionCodesForUserAsync(cashier.UserId, cancellationToken);
        return new BillingSplitPrincipal(
            cashier.UserId,
            permissions.Contains(BillingSplitApplication.MutationPermission, StringComparer.Ordinal));
    }

    public async Task<BillingSplitPrincipal> RequireMutationAsync(
        HttpContext context,
        Guid terminalId,
        CancellationToken cancellationToken)
    {
        var principal = await RequireReadAsync(context, terminalId, cancellationToken);
        await _authorization.AuthorizeAsync(principal.UserId, BillingSplitApplication.MutationPermission, cancellationToken);
        return principal with { CanMutate = true };
    }
}

internal sealed class BillingSplitExceptionFilter : IEndpointFilter
{
    private static readonly Action<ILogger, string, string, Exception?> LogRequestFailure =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(5200, nameof(LogRequestFailure)),
            "Billing split request failed on {Path} ({TraceIdentifier}).");

    private readonly ILogger<BillingSplitExceptionFilter> _logger;

    public BillingSplitExceptionFilter(ILogger<BillingSplitExceptionFilter> logger)
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
            var mapped = Map(exception);
            if (mapped.Status >= StatusCodes.Status500InternalServerError)
            {
                LogRequestFailure(
                    _logger,
                    invocationContext.HttpContext.Request.Path,
                    invocationContext.HttpContext.TraceIdentifier,
                    exception);
            }

            var conflict = exception is SplitDesignConcurrencyException concurrency
                ? new BillingSplitConflict(
                    concurrency.Resource,
                    concurrency.Id,
                    concurrency.Expected,
                    concurrency.Actual)
                : null;
            return Results.Json(
                new BillingSplitErrorEnvelope(new BillingSplitError(
                    mapped.Code,
                    mapped.Message,
                    mapped.Status,
                    invocationContext.HttpContext.TraceIdentifier,
                    conflict)),
                statusCode: mapped.Status);
        }
    }

    private static (int Status, string Code, string Message) Map(Exception exception) => exception switch
    {
        BillingSplitUnauthorizedException => (401, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
        AuthorizationDeniedException => (403, "FORBIDDEN", "Bu işlem için yetkiniz yok."),
        BillingSplitNotFoundException or KeyNotFoundException => (404, "NOT_FOUND", "İstenen hesap bulunamadı."),
        SplitDesignConcurrencyException => (409, "CONCURRENT_MODIFICATION", "Hesap bölme tasarımı başka bir işlem tarafından değiştirildi."),
        SplitDesignUnsupportedBillStateException => (409, "UNSUPPORTED_BILL_STATE", "Bu hesap durumunda bölme tasarımı değiştirilemez."),
        ArgumentException or InvalidOperationException => (400, "VALIDATION_FAILED", "Hesap bölme isteği doğrulanamadı."),
        PostgresException postgres when postgres.SqlState == PostgresErrorCodes.SerializationFailure =>
            (409, "CONCURRENT_MODIFICATION", "Hesap bölme tasarımı başka bir işlem tarafından değiştirildi."),
        PostgresException or NpgsqlException => (503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
        _ => (500, "INTERNAL_ERROR", "İşlem tamamlanamadı."),
    };
}

public sealed class BillingSplitUnauthorizedException : Exception
{
    public BillingSplitUnauthorizedException(string message) : base(message) { }
}
