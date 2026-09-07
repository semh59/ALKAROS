using ALKAROS.Catalog.Pricing;
using ALKAROS.Catalog.ProductCatalog;
using ALKAROS.Identity.Authorization;
using ALKAROS.Host.Experience;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace ALKAROS.Host.Experience.Catalog;

public static class CatalogManagementEndpoints
{
    public const string ManagerCookieName = "alkaros.manager";
    public const string ManagePermission = "catalog.manage";
    public const int DefaultPageSize = 50;
    public const int MaximumPageSize = 100;

    public static IServiceCollection AddCatalogManagement(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<ICategoryRepository, PostgresCategoryRepository>();
        services.TryAddScoped<ITaxProfileRepository, PostgresTaxProfileRepository>();
        services.TryAddScoped<IProductRepository, PostgresProductRepository>();
        services.TryAddScoped<IModifierGroupRepository, PostgresModifierGroupRepository>();
        services.TryAddScoped<IModifierRepository, PostgresModifierRepository>();
        services.TryAddScoped<IProductModifierGroupRepository, PostgresProductModifierGroupRepository>();
        services.TryAddScoped<IPricingRepository, PostgresPricingRepository>();
        services.TryAddScoped<CatalogManagerAuthentication>();
        services.TryAddScoped<CatalogManagementStore>();
        // Found by an independent audit (2026-09-05): CatalogManagerEndpointFilter
        // requires IAuthorizationService in its constructor, but this method never
        // registered it (or the IRoleRepository/IDenialEventSink it needs) — it
        // only ever worked because the full Host composition happened to register
        // these from another module first. A standalone host for this module alone
        // could never resolve the filter. Every other module's Add*Experience
        // registers its own filter dependencies; Catalog was the one exception.
        services.TryAddScoped<IRoleRepository, PostgresRoleRepository>();
        services.TryAddScoped<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddScoped<IAuthorizationService, AuthorizationService>();
        // Found by an independent audit (2026-09-07): current_price never
        // advanced or retreated on its own — see
        // CatalogPriceRecomputeHostedService's own doc comment.
        services.AddHostedService<CatalogPriceRecomputeHostedService>();
        return services;
    }

    public static RouteGroupBuilder MapCatalogManagement(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var catalog = endpoints.MapGroup("/api/v1/management/catalog");
        catalog.AddEndpointFilter<CatalogManagerEndpointFilter>();

        catalog.MapGet("/categories", async (
            string? limit,
            string? cursor,
            CatalogManagementStore store,
            CancellationToken cancellationToken) =>
            Results.Ok(await store.ListCategoriesAsync(ParseLimit(limit), cursor, cancellationToken)));
        catalog.MapPost("/categories", async (
            CreateCategoryV1 request,
            CatalogManagementStore store,
            CancellationToken cancellationToken) =>
        {
            var created = await store.CreateCategoryAsync(request, cancellationToken);
            return Results.Created($"/api/v1/management/catalog/categories/{created.Id:D}", created);
        });

        catalog.MapGet("/tax-profiles", async (
            string? limit,
            string? cursor,
            CatalogManagementStore store,
            CancellationToken cancellationToken) =>
            Results.Ok(await store.ListTaxProfilesAsync(ParseLimit(limit), cursor, cancellationToken)));
        catalog.MapPost("/tax-profiles", async (
            CreateTaxProfileV1 request,
            CatalogManagementStore store,
            CancellationToken cancellationToken) =>
        {
            var created = await store.CreateTaxProfileAsync(request, cancellationToken);
            return Results.Created($"/api/v1/management/catalog/tax-profiles/{created.Id:D}", created);
        });

        catalog.MapGet("/products", async (
            string? limit,
            string? cursor,
            CatalogManagementStore store,
            CancellationToken cancellationToken) =>
            Results.Ok(await store.ListProductsAsync(ParseLimit(limit), cursor, cancellationToken)));
        catalog.MapPost("/products", async (
            CreateProductV1 request,
            CatalogManagementStore store,
            CancellationToken cancellationToken) =>
        {
            var created = await store.CreateProductAsync(request, cancellationToken);
            return Results.Created($"/api/v1/management/catalog/products/{created.Id:D}", created);
        });
        catalog.MapPost("/products/{productId:guid}/availability", async (
            Guid productId,
            SetProductAvailabilityV1 request,
            CatalogManagementStore store,
            CancellationToken cancellationToken) =>
        {
            var updated = await store.SetProductAvailabilityAsync(productId, request, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        });

        catalog.MapGet("/modifier-groups", async (
            string? limit,
            string? cursor,
            CatalogManagementStore store,
            CancellationToken cancellationToken) =>
            Results.Ok(await store.ListModifierGroupsAsync(ParseLimit(limit), cursor, cancellationToken)));
        catalog.MapPost("/modifier-groups", async (
            CreateModifierGroupV1 request,
            CatalogManagementStore store,
            CancellationToken cancellationToken) =>
        {
            var created = await store.CreateModifierGroupAsync(request, cancellationToken);
            return Results.Created($"/api/v1/management/catalog/modifier-groups/{created.Id:D}", created);
        });

        catalog.MapGet("/modifiers", async (
            string? limit,
            string? cursor,
            CatalogManagementStore store,
            CancellationToken cancellationToken) =>
            Results.Ok(await store.ListModifiersAsync(ParseLimit(limit), cursor, cancellationToken)));
        catalog.MapPost("/modifiers", async (
            CreateModifierV1 request,
            CatalogManagementStore store,
            CancellationToken cancellationToken) =>
        {
            var created = await store.CreateModifierAsync(request, cancellationToken);
            return Results.Created($"/api/v1/management/catalog/modifiers/{created.Id:D}", created);
        });

        catalog.MapGet("/product-modifier-assignments", async (
            string? limit,
            string? cursor,
            CatalogManagementStore store,
            CancellationToken cancellationToken) =>
            Results.Ok(await store.ListAssignmentsAsync(ParseLimit(limit), cursor, cancellationToken)));
        catalog.MapPost("/product-modifier-assignments", async (
            CreateProductModifierAssignmentV1 request,
            CatalogManagementStore store,
            CancellationToken cancellationToken) =>
        {
            var created = await store.CreateAssignmentAsync(request, cancellationToken);
            return Results.Created(
                $"/api/v1/management/catalog/product-modifier-assignments/{created.Id:D}",
                created);
        });

        catalog.MapGet("/prices", async (
            string? limit,
            string? cursor,
            CatalogManagementStore store,
            CancellationToken cancellationToken) =>
            Results.Ok(await store.ListPricesAsync(ParseLimit(limit), cursor, cancellationToken)));
        catalog.MapPost("/prices", async (
            CreateProductPriceV1 request,
            CatalogManagementStore store,
            CancellationToken cancellationToken) =>
        {
            var created = await store.CreatePriceAsync(request, cancellationToken);
            return Results.Created($"/api/v1/management/catalog/prices/{created.Id:D}", created);
        });
        catalog.MapGet("/effective-price", async (
            Guid productId,
            PriceType priceType,
            string currencyCode,
            DateTimeOffset at,
            CatalogManagementStore store,
            CancellationToken cancellationToken) =>
        {
            var price = await store.GetEffectivePriceAsync(
                productId,
                priceType,
                currencyCode,
                at,
                cancellationToken);
            return price is null ? Results.NotFound() : Results.Ok(price);
        });

        return catalog;
    }

    private static int ParseLimit(string? value)
    {
        if (value is null)
            return DefaultPageSize;
        if (!int.TryParse(value, out var limit) || limit is < 1 or > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"Page size must be between 1 and {MaximumPageSize}.");
        }

        return limit;
    }
}

public sealed class CatalogManagerAuthentication
{
    private readonly NpgsqlDataSource _dataSource;

    public CatalogManagerAuthentication(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<Guid> AuthenticateAsync(HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rawToken = context.Request.Cookies[CatalogManagementEndpoints.ManagerCookieName];
        var actorId = await ManagementSessionLookup.ResolveActorAsync(_dataSource, rawToken, allowSupervisor: false, cancellationToken);
        return actorId ?? throw new CatalogUnauthorizedException();
    }
}

public sealed class CatalogManagerEndpointFilter : IEndpointFilter
{
    private readonly CatalogManagerAuthentication _authentication;
    private readonly IAuthorizationService _authorization;

    public CatalogManagerEndpointFilter(
        CatalogManagerAuthentication authentication,
        IAuthorizationService authorization)
    {
        _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        try
        {
            var actorId = await _authentication.AuthenticateAsync(
                context.HttpContext,
                context.HttpContext.RequestAborted);
            await _authorization.AuthorizeAsync(
                actorId,
                CatalogManagementEndpoints.ManagePermission,
                context.HttpContext.RequestAborted);
            return await next(context);
        }
        catch (CatalogUnauthorizedException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (AuthorizationDeniedException exception)
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
        catch (BadHttpRequestException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (ArgumentException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (InvalidOperationException exception)
        {
            return MapError(context.HttpContext, exception);
        }
    }

    private static IResult MapError(HttpContext context, Exception exception)
    {
        var (status, code, message) = exception switch
        {
            CatalogUnauthorizedException =>
                (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Authentication is required."),
            AuthorizationDeniedException =>
                (StatusCodes.Status403Forbidden, "FORBIDDEN", "The catalog manager permission is required."),
            PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "products_sku_key" } =>
                (StatusCodes.Status409Conflict, "DUPLICATE_SKU", "The product SKU already exists."),
            PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } =>
                (StatusCodes.Status409Conflict, "DUPLICATE_RESOURCE", "The catalog record already exists."),
            PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation } =>
                (StatusCodes.Status409Conflict, "OVERLAPPING_EFFECTIVE_PRICE", "The effective price interval overlaps an existing interval."),
            PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } =>
                (StatusCodes.Status400BadRequest, "REFERENCE_NOT_FOUND", "A referenced catalog record does not exist."),
            PostgresException { SqlState: PostgresErrorCodes.CheckViolation or PostgresErrorCodes.NumericValueOutOfRange } =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "The catalog request violates a data constraint."),
            ArgumentException or BadHttpRequestException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "The catalog request is invalid."),
            InvalidOperationException =>
                (StatusCodes.Status409Conflict, "CONCURRENCY_CONFLICT", "The catalog record was concurrently modified."),
            NpgsqlException =>
                (StatusCodes.Status503ServiceUnavailable, "DATABASE_UNAVAILABLE", "The catalog operation could not be completed."),
            _ => throw exception,
        };
        return Results.Json(
            new CatalogApiErrorEnvelopeV1(
                new CatalogApiErrorV1(code, message, status, context.TraceIdentifier)),
            statusCode: status);
    }
}

public sealed class CatalogUnauthorizedException : Exception
{
    public CatalogUnauthorizedException() : base("A valid catalog manager session is required.")
    {
    }
}
