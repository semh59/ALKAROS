using ALKAROS.CustomerAccounts.CreditTerms;
using ALKAROS.Identity.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace ALKAROS.Host.Experience.CustomerCredit;

/// <summary>
/// V1-RMD-440 (PO 2026-09-29): a manager sets how much each customer may owe on their account and, optionally, the
/// payment term after which unpaid charges block new ones. Same manager session and permission as settings
/// management (<c>settings.manage</c>: manager-only, no escalation path) - a credit limit is a business setting
/// held per customer.
/// </summary>
public static class CustomerCreditTermsEndpoints
{
    public const string ManagerCookieName = "alkaros.manager";
    public const string ManagePermission = "settings.manage";

    public static IServiceCollection AddCustomerCreditTermsExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<ICustomerCreditTermsStore, PostgresCustomerCreditTermsStore>();
        services.TryAddScoped<IRoleRepository, PostgresRoleRepository>();
        services.TryAddScoped<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddScoped<IAuthorizationService, AuthorizationService>();
        services.TryAddScoped<CustomerCreditManagerEndpointFilter>();
        return services;
    }

    public static RouteGroupBuilder MapCustomerCreditTerms(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup("/api/v1/management/customers/{customerId:guid}/credit-terms");
        group.AddEndpointFilter<CustomerCreditManagerEndpointFilter>();

        group.MapGet("/", async (Guid customerId, ICustomerCreditTermsStore store, CancellationToken cancellationToken) =>
        {
            var terms = await store.GetAsync(customerId, cancellationToken)
                ?? throw new CustomerCreditCustomerNotFoundException();
            return Results.Ok(CustomerCreditTermsV1.From(terms));
        });

        group.MapPut("/", async (
            Guid customerId,
            UpdateCustomerCreditTermsV1 request,
            ICustomerCreditTermsStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = CustomerCreditManagerEndpointFilter.RequireActorId(context);
            var terms = await store.SetAsync(customerId, request.CreditLimit, request.PaymentTermDays, actorId, cancellationToken)
                ?? throw new CustomerCreditCustomerNotFoundException();
            return Results.Ok(CustomerCreditTermsV1.From(terms));
        });

        return group;
    }
}

public sealed record UpdateCustomerCreditTermsV1(decimal CreditLimit, int? PaymentTermDays);

public sealed record CustomerCreditTermsV1(Guid CustomerId, decimal CreditLimit, int? PaymentTermDays, DateTimeOffset? UpdatedAt)
{
    public static CustomerCreditTermsV1 From(CustomerCreditTerms value)
        => new(value.CustomerId, value.CreditLimit, value.PaymentTermDays, value.UpdatedAt);
}

public sealed record CustomerCreditApiErrorV1(string Code, string Message, int Status, string TraceId);

public sealed record CustomerCreditApiErrorEnvelopeV1(CustomerCreditApiErrorV1 Error);

public sealed class CustomerCreditCustomerNotFoundException : Exception
{
    public CustomerCreditCustomerNotFoundException() : base("The customer does not exist or is anonymized.")
    {
    }
}

public sealed class CustomerCreditUnauthorizedException : Exception
{
    public CustomerCreditUnauthorizedException() : base("A valid manager session is required.")
    {
    }
}

public sealed class CustomerCreditManagerEndpointFilter : IEndpointFilter
{
    private const string ActorIdItemKey = "CustomerCreditManagerActorId";

    private readonly NpgsqlDataSource _dataSource;
    private readonly IAuthorizationService _authorization;

    public CustomerCreditManagerEndpointFilter(NpgsqlDataSource dataSource, IAuthorizationService authorization)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
    }

    public static Guid RequireActorId(HttpContext context)
        => context.Items[ActorIdItemKey] as Guid?
            ?? throw new InvalidOperationException($"{nameof(CustomerCreditManagerEndpointFilter)} did not run before this endpoint.");

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        try
        {
            var rawToken = httpContext.Request.Cookies[CustomerCreditTermsEndpoints.ManagerCookieName];
            var actorId = await ManagementSessionLookup.ResolveActorAsync(
                    _dataSource, rawToken, allowSupervisor: false, httpContext.RequestAborted)
                ?? throw new CustomerCreditUnauthorizedException();
            await _authorization.AuthorizeAsync(actorId, CustomerCreditTermsEndpoints.ManagePermission, httpContext.RequestAborted);
            httpContext.Items[ActorIdItemKey] = actorId;
            return await next(context);
        }
        catch (Exception exception) when (exception is CustomerCreditUnauthorizedException
            or AuthorizationDeniedException
            or CustomerCreditCustomerNotFoundException
            or ArgumentException
            or BadHttpRequestException
            or NpgsqlException)
        {
            return MapError(httpContext, exception);
        }
    }

    private static IResult MapError(HttpContext context, Exception exception)
    {
        var (status, code, message) = exception switch
        {
            CustomerCreditUnauthorizedException =>
                (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
            AuthorizationDeniedException =>
                (StatusCodes.Status403Forbidden, "FORBIDDEN", "Kredi limitini yalnız yönetici değiştirebilir."),
            CustomerCreditCustomerNotFoundException =>
                (StatusCodes.Status404NotFound, "NOT_FOUND", "Müşteri bulunamadı veya anonimleştirilmiş."),
            ArgumentException or BadHttpRequestException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED",
                    "Kredi limiti 0 veya üstü (en çok iki ondalık), vade 1-365 gün olmalı."),
            _ =>
                (StatusCodes.Status503ServiceUnavailable, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
        };
        return Results.Json(
            new CustomerCreditApiErrorEnvelopeV1(new CustomerCreditApiErrorV1(code, message, status, context.TraceIdentifier)),
            statusCode: status);
    }
}
