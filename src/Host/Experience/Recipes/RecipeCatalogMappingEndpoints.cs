using ALKAROS.Identity.Authorization;
using ALKAROS.Measurements;
using ALKAROS.Recipes.CatalogMapping;
using ALKAROS.Recipes.Units;
using ALKAROS.Recipes.Versioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ALKAROS.Host.Experience.Recipes;

/// <summary>
/// V11-RCP-003: the AvT (actual vs theoretical) variance report chain's
/// first step — a manager-only surface to say which recipe a catalog
/// product corresponds to. Own endpoint filter rather than reusing
/// StockMasterEndpointFilter: this Host namespace's own approved
/// orchestration edge (ApprovedHostOrchestrationEdges,
/// ModuleBoundaryTests.cs) only needs to name Identity + Recipes, not also
/// Inventory, by keeping the auth plumbing self-contained instead of
/// cross-referencing another Host area's filter type.
/// </summary>
public static class RecipeCatalogMappingEndpoints
{
    public const string ManagerCookieName = "alkaros.manager";
    public const string ManagePermission = "inventory.manage";

    public static IServiceCollection AddRecipeCatalogMappingExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<IProductRecipeMappingRepository, PostgresProductRecipeMappingRepository>();
        // V1-RMD-275: recipe catalog management (V11-RCP-001) and custom unit conversions (V11-UNT-001).
        services.TryAddTransient<IUnitConverter, UnitConverter>();
        services.TryAddScoped<IUnitConversionRepository, PostgresUnitConversionRepository>();
        services.TryAddScoped<IRecipeRepository, PostgresRecipeRepository>();
        services.TryAddScoped<IRecipeVersionRepository, PostgresRecipeVersionRepository>();
        services.TryAddScoped<IRecipeLifecycleService, RecipeLifecycleService>();

        services.TryAddScoped<IRoleRepository, PostgresRoleRepository>();
        services.TryAddScoped<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddScoped<IAuthorizationService, AuthorizationService>();
        services.TryAddScoped<RecipeCatalogMappingAuthentication>();
        return services;
    }

    public static RouteGroupBuilder MapRecipeCatalogMappingApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup("/api/v1/management/recipes");
        group.AddEndpointFilter<RecipeCatalogMappingEndpointFilter>();

        group.MapPost("/products/{productId:guid}/mapping", async (
            Guid productId,
            AssignProductRecipeMappingV1 request,
            IProductRecipeMappingRepository mappings,
            CancellationToken cancellationToken) =>
        {
            var mapping = new ProductRecipeMapping(productId, request.RecipeId, request.IsActive, request.Notes);
            await mappings.AddOrUpdateAsync(mapping, cancellationToken);
            return Results.Ok(ProductRecipeMappingV1.From(mapping));
        });

        group.MapGet("/products/{productId:guid}/mapping", async (
            Guid productId,
            IProductRecipeMappingRepository mappings,
            CancellationToken cancellationToken) =>
        {
            var mapping = await mappings.GetByProductIdAsync(productId, cancellationToken);
            return mapping is null
                ? Results.NotFound(new { error = new { code = "NOT_FOUND", message = "Bu ürün için reçete eşlemesi bulunamadı." } })
                : Results.Ok(ProductRecipeMappingV1.From(mapping));
        });

        group.MapDelete("/products/{productId:guid}/mapping", async (
            Guid productId,
            IProductRecipeMappingRepository mappings,
            CancellationToken cancellationToken) =>
        {
            if (await mappings.GetByProductIdAsync(productId, cancellationToken) is null)
                throw new ProductRecipeMappingNotFoundException(productId);

            await mappings.RemoveAsync(productId, cancellationToken);
            return Results.NoContent();
        });

        group.MapRecipeManagement();

        return group;
    }
}

public sealed record AssignProductRecipeMappingV1(Guid RecipeId, bool IsActive, string? Notes);

public sealed record ProductRecipeMappingV1(Guid ProductId, Guid RecipeId, bool IsActive, string? Notes)
{
    public static ProductRecipeMappingV1 From(ProductRecipeMapping mapping)
        => new(mapping.ProductId, mapping.RecipeId, mapping.IsActive, mapping.Notes);
}

public sealed class RecipeCatalogMappingAuthentication
{
    private readonly NpgsqlDataSource _dataSource;

    public RecipeCatalogMappingAuthentication(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<Guid> AuthenticateAsync(HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rawToken = context.Request.Cookies[RecipeCatalogMappingEndpoints.ManagerCookieName];
        var actorId = await ManagementSessionLookup.ResolveActorAsync(_dataSource, rawToken, allowSupervisor: false, cancellationToken);
        return actorId ?? throw new RecipeCatalogMappingUnauthorizedException();
    }
}

public sealed class RecipeCatalogMappingEndpointFilter : IEndpointFilter
{
    private static readonly Action<ILogger, string, string, Exception?> LogRequestFailure =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(5700, nameof(LogRequestFailure)),
            "Recipe catalog mapping request failed on {Path} ({TraceIdentifier}).");

    private readonly RecipeCatalogMappingAuthentication _authentication;
    private readonly IAuthorizationService _authorization;
    private readonly ILogger<RecipeCatalogMappingEndpointFilter> _logger;

    public RecipeCatalogMappingEndpointFilter(
        RecipeCatalogMappingAuthentication authentication, IAuthorizationService authorization, ILogger<RecipeCatalogMappingEndpointFilter> logger)
    {
        _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _logger = logger;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            var actorId = await _authentication.AuthenticateAsync(context.HttpContext, context.HttpContext.RequestAborted);
            await _authorization.AuthorizeAsync(actorId, RecipeCatalogMappingEndpoints.ManagePermission, context.HttpContext.RequestAborted);
            return await next(context);
        }
        catch (Exception exception)
        {
            var mapped = Map(exception);
            if (mapped.Status >= StatusCodes.Status500InternalServerError)
            {
                LogRequestFailure(_logger, context.HttpContext.Request.Path, context.HttpContext.TraceIdentifier, exception);
            }

            return Results.Json(
                new { error = new { code = mapped.Code, message = mapped.Message } },
                statusCode: mapped.Status);
        }
    }

    private static (int Status, string Code, string Message) Map(Exception exception) => exception switch
    {
        RecipeCatalogMappingUnauthorizedException => (401, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
        AuthorizationDeniedException => (403, "FORBIDDEN", "Bu işlem için yetki gerekiyor."),
        ProductRecipeMappingNotFoundException => (404, "NOT_FOUND", "Bu ürün için reçete eşlemesi bulunamadı."),
        RecipeNotFoundException => (404, "NOT_FOUND", "İstenen reçete ya da sürümü bulunamadı."),
        RecipeVersionImmutableException => (409, "VERSION_IMMUTABLE", "Etkinleştirilmiş ya da kilitlenmiş reçete sürümü değiştirilemez; yeni bir taslak sürüm oluşturun."),
        RecipeVersionConflictException => (409, "VERSION_CONFLICT", "Reçete sürümü mevcut durumuyla bu işleme uygun değil."),
        InvalidRecipeVersionException => (400, "VALIDATION_FAILED", "İstek doğrulanamadı: reçete sürümü bilgileri geçerli olmalı."),
        ArgumentException or BadHttpRequestException => (400, "VALIDATION_FAILED", "İstek doğrulanamadı."),
        PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } => (409, "DUPLICATE_RESOURCE", "Aynı kimlikte bir kayıt zaten var."),
        PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } => (400, "REFERENCE_NOT_FOUND", "Başvurulan bir kayıt mevcut değil."),
        PostgresException or NpgsqlException => (503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
        _ => (500, "INTERNAL_ERROR", "İşlem tamamlanamadı."),
    };
}

public sealed class RecipeCatalogMappingUnauthorizedException : Exception
{
    public RecipeCatalogMappingUnauthorizedException() : base("A valid manager session is required.")
    {
    }
}
