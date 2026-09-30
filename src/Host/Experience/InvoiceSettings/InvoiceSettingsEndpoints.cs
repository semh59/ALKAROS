using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Invoicing.Generation.OrderInvoices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ALKAROS.Host.Experience.InvoiceSettings;

/// <summary>
/// The business as it appears on its invoices (legal name, VKN or TCKN, tax office, address): read and replaced by a
/// manager holding <c>integrations.manage</c>, on the same cashier session as the QNB credential settings. An
/// incomplete profile is refused with the Turkish names of the fields to fix, so no invoice is ever drafted without one.
/// </summary>
public static class InvoiceSettingsEndpoints
{
    public const string RoutePrefix = "/api/v1/terminals/{terminalId:guid}/invoice-settings";

    private static readonly Dictionary<string, (string Field, string Label)> Labels = new()
    {
        [nameof(SellerProfile.LegalName)] = ("legalName", "Ticari ünvan"),
        [nameof(SellerProfile.TaxIdNumber)] = ("taxIdNumber", "Vergi veya TC kimlik numarası"),
        [nameof(SellerProfile.TaxOffice)] = ("taxOffice", "Vergi dairesi"),
        [nameof(SellerProfile.Address)] = ("address", "Adres"),
        [nameof(SellerProfile.District)] = ("district", "İlçe"),
        [nameof(SellerProfile.City)] = ("city", "İl"),
        [nameof(SellerProfile.Email)] = ("email", "E-posta"),
    };

    public static IServiceCollection AddInvoiceSettingsExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<DualScreenStore>();
        services.TryAddSingleton<IRoleRepository, PostgresRoleRepository>();
        services.TryAddSingleton<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddSingleton<IAuthorizationService, AuthorizationService>();
        services.TryAddSingleton<ISellerProfileStore, PostgresSellerProfileStore>();
        services.TryAddTransient<InvoiceSettingsExceptionFilter>();
        return services;
    }

    public static RouteGroupBuilder MapInvoiceSettingsApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup(RoutePrefix)
            .WithTags("InvoiceSettings")
            .RequireRateLimiting("terminal-write")
            .AddEndpointFilter<InvoiceSettingsExceptionFilter>();

        group.MapGet("/seller-profile", async (
            Guid terminalId,
            ISellerProfileStore store,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireManagerAsync(context, terminalId, dualStore, authorization, cancellationToken);
            var profile = await store.GetAsync(cancellationToken);
            return Results.Ok(new SellerProfileResponse(profile is not null, profile));
        });

        group.MapPut("/seller-profile", async (
            Guid terminalId,
            SellerProfile request,
            ISellerProfileStore store,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = await RequireManagerAsync(context, terminalId, dualStore, authorization, cancellationToken);
            var problems = request.Problems();
            if (problems.Count > 0)
            {
                var fields = problems.Select(problem => Labels[problem]).ToList();
                return Results.Json(
                    new
                    {
                        error = new
                        {
                            code = "VALIDATION_FAILED",
                            message = "Şu alanlar eksik ya da hatalı: " + string.Join(", ", fields.Select(field => field.Label)) + ".",
                            fields = fields.Select(field => field.Field),
                        },
                    },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            await store.SaveAsync(request, userId, cancellationToken);
            return Results.NoContent();
        });

        return group;
    }

    private static async Task<Guid> RequireManagerAsync(
        HttpContext context,
        Guid terminalId,
        DualScreenStore store,
        IAuthorizationService authorization,
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
        await authorization.AuthorizeAsync(principal.UserId, ApplicationPermissions.IntegrationsManage, cancellationToken);
        return principal.UserId;
    }
}

public sealed record SellerProfileResponse(bool Configured, SellerProfile? Profile);

/// <summary>Turns the failures of the invoice settings requests into the Turkish error body; technical detail goes to the log only.</summary>
public sealed class InvoiceSettingsExceptionFilter(ILogger<InvoiceSettingsExceptionFilter> logger) : IEndpointFilter
{
    private static readonly Action<ILogger, string, string, Exception?> LogRequestFailure =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(5430, nameof(LogRequestFailure)),
            "Invoice settings request failed on {Path} ({TraceIdentifier}).");

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
            if (status >= StatusCodes.Status500InternalServerError)
                LogRequestFailure(logger, context.HttpContext.Request.Path, context.HttpContext.TraceIdentifier, exception);
            return Results.Json(new { error = new { code, message } }, statusCode: status);
        }
    }
}
