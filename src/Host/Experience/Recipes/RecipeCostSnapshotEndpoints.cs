using ALKAROS.Identity.Authorization;
using ALKAROS.Measurements;
using ALKAROS.Recipes.CostSnapshots;
using ALKAROS.Recipes.Versioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace ALKAROS.Host.Experience.Recipes;

/// <summary>
/// V1-RMD-247: found by an independent audit (2026-09-18) —
/// IRecipeCostSnapshotService (V11-RCP-002: waste-factor + moving-average
/// recipe costing) was domain-complete, DI-registered, unit-tested, and had
/// zero HTTP surface — a recipe's cost could never actually be calculated
/// through the running application. Own endpoint filter (same reasoning as
/// RecipeCatalogMappingEndpoints's own doc comment: keeps the approved
/// Host orchestration edge to Identity + Recipes only, no Inventory).
/// </summary>
public static class RecipeCostSnapshotEndpoints
{
    public const string ManagerCookieName = "alkaros.manager";
    public const string ManagePermission = "inventory.manage";

    public static IServiceCollection AddRecipeCostSnapshotExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<IRecipeCostSnapshotRepository, PostgresRecipeCostSnapshotRepository>();
        services.TryAddScoped<IRecipeVersionRepository, PostgresRecipeVersionRepository>();
        services.TryAddScoped<IStockCostResolver, PostgresStockCostResolver>();
        services.TryAddSingleton<IUnitConverter, UnitConverter>();
        services.TryAddScoped<IRecipeCostSnapshotService, RecipeCostSnapshotService>();

        services.TryAddScoped<IRoleRepository, PostgresRoleRepository>();
        services.TryAddScoped<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddScoped<IAuthorizationService, AuthorizationService>();
        services.TryAddScoped<RecipeCostSnapshotAuthentication>();
        return services;
    }

    public static RouteGroupBuilder MapRecipeCostSnapshotApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup("/api/v1/management/recipes/{recipeVersionId:guid}/cost-snapshots");
        group.AddEndpointFilter<RecipeCostSnapshotEndpointFilter>();

        group.MapPost("/", async (
            Guid recipeVersionId,
            CreateRecipeCostSnapshotV1 request,
            IRecipeCostSnapshotService snapshots,
            IRecipeVersionRepository versions,
            CancellationToken cancellationToken) =>
        {
            var snapshot = await snapshots.CreateSnapshotAsync(
                new CreateSnapshotCommand(
                    recipeVersionId, request.CostBasisDate, request.StockItemUnits,
                    request.FallbackItemCosts, request.Currency ?? "TRY"),
                cancellationToken);
            var version = await versions.GetByIdAsync(recipeVersionId, cancellationToken)
                ?? throw new RecipeVersionNotFoundException(recipeVersionId);
            return Results.Created(
                $"/api/v1/management/recipes/{recipeVersionId:D}/cost-snapshots/{snapshot.Id:D}",
                RecipeCostSnapshotV1.From(snapshot, version.YieldQuantity));
        });

        group.MapGet("/effective", async (
            Guid recipeVersionId,
            DateOnly asOfDate,
            IRecipeCostSnapshotService snapshots,
            IRecipeVersionRepository versions,
            CancellationToken cancellationToken) =>
        {
            var snapshot = await snapshots.GetEffectiveSnapshotAsync(recipeVersionId, asOfDate, cancellationToken);
            if (snapshot is null)
                return Results.NotFound();
            var version = await versions.GetByIdAsync(recipeVersionId, cancellationToken)
                ?? throw new RecipeVersionNotFoundException(recipeVersionId);
            return Results.Ok(RecipeCostSnapshotV1.From(snapshot, version.YieldQuantity));
        });

        return group;
    }
}

public sealed record CreateRecipeCostSnapshotV1(
    DateOnly CostBasisDate,
    string? Currency = null,
    IReadOnlyDictionary<Guid, string>? StockItemUnits = null,
    IReadOnlyDictionary<Guid, decimal>? FallbackItemCosts = null);

public sealed record RecipeCostSnapshotItemV1(
    Guid StockItemId, decimal RawQuantity, decimal WasteFactor, decimal EffectiveNativeQuantity,
    string NativeUnitCode, decimal StockQuantity, string StockUnitCode, decimal UnitCost, decimal LineCost)
{
    public static RecipeCostSnapshotItemV1 From(RecipeCostSnapshotItem item)
        => new(item.StockItemId, item.RawQuantity, item.WasteFactor, item.EffectiveNativeQuantity,
            item.NativeUnitCode, item.StockQuantity, item.StockUnitCode, item.UnitCost, item.LineCost);
}

public sealed record RecipeCostSnapshotV1(
    Guid Id, Guid RecipeVersionId, DateOnly CostBasisDate, decimal CalculatedCost, decimal CostPerPortion,
    string Currency, DateTimeOffset CreatedAt, IReadOnlyList<RecipeCostSnapshotItemV1> Items)
{
    // V1-RMD-247: CalculatedCost is the whole batch's cost; CostPerPortion
    // (divided by the recipe version's own YieldQuantity) is what the
    // codebase's independent audit found was never computed anywhere —
    // this is the first real consumer, so it is computed here rather than
    // changing what RecipeCostSnapshot.CalculatedCost itself means.
    public static RecipeCostSnapshotV1 From(RecipeCostSnapshot snapshot, decimal yieldQuantity)
        => new(snapshot.Id, snapshot.RecipeVersionId, snapshot.CostBasisDate, snapshot.CalculatedCost,
            yieldQuantity > 0 ? Math.Round(snapshot.CalculatedCost / yieldQuantity, 2, MidpointRounding.AwayFromZero) : snapshot.CalculatedCost,
            snapshot.Currency, snapshot.CreatedAt, snapshot.Items.Select(RecipeCostSnapshotItemV1.From).ToArray());
}

public sealed class RecipeCostSnapshotAuthentication
{
    private readonly NpgsqlDataSource _dataSource;

    public RecipeCostSnapshotAuthentication(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<Guid> AuthenticateAsync(HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rawToken = context.Request.Cookies[RecipeCostSnapshotEndpoints.ManagerCookieName];
        var actorId = await ManagementSessionLookup.ResolveActorAsync(_dataSource, rawToken, allowSupervisor: false, cancellationToken);
        return actorId ?? throw new RecipeCostSnapshotUnauthorizedException();
    }
}

public sealed class RecipeCostSnapshotEndpointFilter : IEndpointFilter
{
    private readonly RecipeCostSnapshotAuthentication _authentication;
    private readonly IAuthorizationService _authorization;

    public RecipeCostSnapshotEndpointFilter(
        RecipeCostSnapshotAuthentication authentication,
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
                RecipeCostSnapshotEndpoints.ManagePermission,
                context.HttpContext.RequestAborted);
            return await next(context);
        }
        catch (RecipeCostSnapshotUnauthorizedException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (AuthorizationDeniedException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (RecipeCostSnapshotException exception)
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
    }

    private static IResult MapError(HttpContext context, Exception exception)
    {
        var (status, code, message) = exception switch
        {
            RecipeCostSnapshotUnauthorizedException =>
                (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
            AuthorizationDeniedException =>
                (StatusCodes.Status403Forbidden, "FORBIDDEN", "Reçete maliyeti yönetimi izni gerekiyor."),
            RecipeVersionNotFoundException =>
                (StatusCodes.Status404NotFound, "NOT_FOUND", "İstenen reçete sürümü bulunamadı."),
            DuplicateCostSnapshotException =>
                (StatusCodes.Status409Conflict, "DUPLICATE_RESOURCE", "Bu tarihte bir maliyet anlık görüntüsü zaten var."),
            MissingCostBasisException =>
                (StatusCodes.Status400BadRequest, "MISSING_COST_BASIS", "Bir malzeme için maliyet verisi bulunamadı."),
            MissingStockUnitMappingException =>
                (StatusCodes.Status400BadRequest, "MISSING_STOCK_UNIT_MAPPING", "Bir malzeme için stok takip birimi belirtilmedi."),
            InvalidCostSnapshotException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } =>
                (StatusCodes.Status409Conflict, "DUPLICATE_RESOURCE", "Aynı kimlikte bir kayıt zaten var."),
            PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } =>
                (StatusCodes.Status400BadRequest, "REFERENCE_NOT_FOUND", "Başvurulan bir kayıt mevcut değil."),
            ArgumentException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            NpgsqlException =>
                (StatusCodes.Status503ServiceUnavailable, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
            _ => throw exception,
        };
        return Results.Json(
            new RecipeCostSnapshotApiErrorEnvelopeV1(new RecipeCostSnapshotApiErrorV1(code, message, status, context.TraceIdentifier)),
            statusCode: status);
    }
}

public sealed record RecipeCostSnapshotApiErrorV1(string Code, string Message, int Status, string TraceId);

public sealed record RecipeCostSnapshotApiErrorEnvelopeV1(RecipeCostSnapshotApiErrorV1 Error);

public sealed class RecipeCostSnapshotUnauthorizedException : Exception
{
    public RecipeCostSnapshotUnauthorizedException() : base("A valid recipe cost snapshot manager session is required.")
    {
    }
}
