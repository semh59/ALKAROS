using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Measurements;
using ALKAROS.Production.BatchLifecycle;
using ALKAROS.Production.StockEffects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace ALKAROS.Host.Experience.Production;

/// <summary>
/// V1-RMD-133: found by an independent audit (2026-09-09) — Production
/// (batch lifecycle + its stock effects, both real, Postgres-backed) had
/// zero HTTP surface at all. A second, narrower gap surfaced while wiring
/// it: IProductionBatchService.CompleteBatchAsync and
/// IProductionStockEffectService.ExecuteBatchStockEffectsAsync are two
/// independent, pre-existing ways to "complete" a batch that were never
/// meant to both run — the first only flips ProductionBatch's own status
/// through its domain aggregate (no stock movement at all); the second
/// does the real work (consumes recipe ingredients, posts finished-portion
/// output, verifies stock availability) and separately flips the same
/// status column by raw SQL, with a weaker precondition (only rejects
/// Cancelled, not "must have been Started first") and an idempotency guard
/// that only catches replay if the batch already has consumption/output
/// rows. Calling both — even sequentially, never mind concurrently — would
/// double-apply the status write with no shared optimistic-concurrency
/// check between them. This surface exposes ExecuteBatchStockEffectsAsync
/// as the one true "complete" operation (it is the only one that actually
/// moves inventory, which is the entire point of a production batch), and
/// restores the missing "must be InProgress" precondition here at the
/// orchestration layer instead of teaching either module about the other.
/// </summary>
public static class ProductionManagementEndpoints
{
    public const string ManagerCookieName = "alkaros.manager";
    public const string ManagePermission = "production.manage";

    public static IServiceCollection AddProductionManagementExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<IProductionBatchRepository, PostgresProductionBatchRepository>();
        services.TryAddScoped<IProductionBatchService, ProductionBatchService>();
        services.TryAddScoped<IProductionStockEffectService, ProductionStockEffectService>();
        // ProductionStockEffectService posts consumption/output stock movements
        // through Inventory's own contract in the same transaction
        // (V0-ARC-001 row 11) — these must resolve standalone too.
        services.TryAddScoped<IStockBalanceRepository, PostgresStockBalanceRepository>();
        services.TryAddScoped<IStockMovementRepository, PostgresStockMovementRepository>();
        // V1-RMD-344 made ProductionStockEffectService depend on the shared IUnitConverter; same
        // TryAddSingleton registration every other experience that converts units makes.
        services.TryAddSingleton<IUnitConverter, UnitConverter>();

        services.TryAddScoped<IRoleRepository, PostgresRoleRepository>();
        services.TryAddScoped<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddScoped<IAuthorizationService, AuthorizationService>();
        services.TryAddScoped<ProductionManagerAuthentication>();
        return services;
    }

    public static RouteGroupBuilder MapProductionManagement(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup("/api/v1/management/production");
        group.AddEndpointFilter<ProductionManagerEndpointFilter>();

        group.MapGet("/batches", async (
            string? status,
            Guid? recipeVersionId,
            Guid? dailyMenuItemId,
            IProductionBatchService service,
            CancellationToken cancellationToken) =>
        {
            var filter = new ProductionBatchFilter(
                Status: string.IsNullOrWhiteSpace(status) ? null : Enum.Parse<ProductionBatchStatus>(status, ignoreCase: true),
                RecipeVersionId: recipeVersionId,
                DailyMenuItemId: dailyMenuItemId);
            var batches = await service.ListBatchesAsync(filter, cancellationToken);
            return Results.Ok(batches.Select(ProductionBatchV1.From).ToArray());
        });

        group.MapPost("/batches", async (
            CreateProductionBatchV1 request,
            HttpContext context,
            IProductionBatchService service,
            CancellationToken cancellationToken) =>
        {
            var actorId = ProductionManagerEndpointFilter.RequireActorId(context);
            var created = await service.CreateBatchAsync(
                new CreateProductionBatchCommand(
                    request.BatchNumber, request.RecipeVersionId, request.PlannedQuantity, request.PortionUnitCode,
                    request.DailyMenuItemId, request.DestinationLocationId, request.Notes, actorId),
                cancellationToken);
            return Results.Created(
                $"/api/v1/management/production/batches/{created.Id:D}", ProductionBatchV1.From(created));
        });

        group.MapGet("/batches/{batchId:guid}", async (
            Guid batchId,
            IProductionBatchService service,
            CancellationToken cancellationToken) =>
        {
            var batch = await service.GetBatchAsync(batchId, cancellationToken)
                ?? throw new ProductionBatchNotFoundException(batchId);
            return Results.Ok(ProductionBatchV1.From(batch));
        });

        group.MapGet("/batches/by-number/{batchNumber}", async (
            string batchNumber,
            IProductionBatchService service,
            CancellationToken cancellationToken) =>
        {
            var batch = await service.GetBatchByNumberAsync(batchNumber, cancellationToken);
            return batch is null ? Results.NotFound() : Results.Ok(ProductionBatchV1.From(batch));
        });

        group.MapPost("/batches/{batchId:guid}/start", async (
            Guid batchId,
            StartProductionBatchV1 request,
            IProductionBatchService service,
            CancellationToken cancellationToken) =>
        {
            var started = await service.StartBatchAsync(
                new StartProductionBatchCommand(batchId, request.StartedAt), cancellationToken);
            return Results.Ok(ProductionBatchV1.From(started));
        });

        group.MapPost("/batches/{batchId:guid}/complete", async (
            Guid batchId,
            CompleteProductionBatchV1 request,
            HttpContext context,
            IProductionBatchService batchService,
            IProductionStockEffectService stockEffectService,
            CancellationToken cancellationToken) =>
        {
            var actorId = ProductionManagerEndpointFilter.RequireActorId(context);

            // See this file's own doc comment: ExecuteBatchStockEffectsAsync's
            // own precondition only rejects Cancelled, not "must have been
            // Started first" — restored here since ProductionBatch.Complete()
            // (which does enforce it) is deliberately not called on this path.
            var batch = await batchService.GetBatchAsync(batchId, cancellationToken)
                ?? throw new ProductionBatchNotFoundException(batchId);
            if (batch.Status != ProductionBatchStatus.InProgress)
            {
                throw new InvalidProductionBatchTransitionException(batch.Status, ProductionBatchStatus.Completed);
            }

            var result = await stockEffectService.ExecuteBatchStockEffectsAsync(
                new ExecuteBatchStockEffectsCommand(
                    batchId, request.ActualQuantity, request.SourceLocationId,
                    request.DestinationLocationId, request.OutputStockItemId, ExecutedBy: actorId),
                cancellationToken);

            var completed = await batchService.GetBatchAsync(batchId, cancellationToken)
                ?? throw new ProductionBatchNotFoundException(batchId);
            return Results.Ok(new
            {
                Batch = ProductionBatchV1.From(completed),
                Consumptions = result.Consumptions.Select(ProductionConsumptionV1.From).ToArray(),
                Output = result.Output is null ? null : ProductionOutputV1.From(result.Output),
                result.WasAlreadyExecuted,
            });
        });

        group.MapPost("/batches/{batchId:guid}/cancel", async (
            Guid batchId,
            CancelProductionBatchV1 request,
            IProductionBatchService service,
            CancellationToken cancellationToken) =>
        {
            var cancelled = await service.CancelBatchAsync(
                new CancelProductionBatchCommand(batchId, request.Reason), cancellationToken);
            return Results.Ok(ProductionBatchV1.From(cancelled));
        });

        group.MapPut("/batches/{batchId:guid}/recipe-version", async (
            Guid batchId,
            ReassignRecipeVersionV1 request,
            IProductionBatchService service,
            CancellationToken cancellationToken) =>
        {
            await service.ReassignRecipeVersionAsync(
                new ReassignRecipeVersionCommand(batchId, request.NewRecipeVersionId), cancellationToken);
            var updated = await service.GetBatchAsync(batchId, cancellationToken)
                ?? throw new ProductionBatchNotFoundException(batchId);
            return Results.Ok(ProductionBatchV1.From(updated));
        });

        group.MapGet("/batches/{batchId:guid}/consumptions", async (
            Guid batchId,
            IProductionStockEffectService service,
            CancellationToken cancellationToken) =>
        {
            var consumptions = await service.GetConsumptionsByBatchIdAsync(batchId, cancellationToken);
            return Results.Ok(consumptions.Select(ProductionConsumptionV1.From).ToArray());
        });

        group.MapGet("/batches/{batchId:guid}/outputs", async (
            Guid batchId,
            IProductionStockEffectService service,
            CancellationToken cancellationToken) =>
        {
            var outputs = await service.GetOutputsByBatchIdAsync(batchId, cancellationToken);
            return Results.Ok(outputs.Select(ProductionOutputV1.From).ToArray());
        });

        return group;
    }
}

public sealed class ProductionManagerAuthentication
{
    private readonly NpgsqlDataSource _dataSource;

    public ProductionManagerAuthentication(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<Guid> AuthenticateAsync(HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rawToken = context.Request.Cookies[ProductionManagementEndpoints.ManagerCookieName];
        var actorId = await ManagementSessionLookup.ResolveActorAsync(_dataSource, rawToken, allowSupervisor: false, cancellationToken);
        return actorId ?? throw new ProductionManagementUnauthorizedException();
    }
}

public sealed class ProductionManagerEndpointFilter : IEndpointFilter
{
    private const string ActorIdItemKey = "ProductionManagerActorId";

    private readonly ProductionManagerAuthentication _authentication;
    private readonly IAuthorizationService _authorization;

    public ProductionManagerEndpointFilter(
        ProductionManagerAuthentication authentication,
        IAuthorizationService authorization)
    {
        _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
    }

    public static Guid RequireActorId(HttpContext context)
        => context.Items[ActorIdItemKey] as Guid?
            ?? throw new InvalidOperationException($"{nameof(ProductionManagerEndpointFilter)} did not run before this endpoint.");

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
                ProductionManagementEndpoints.ManagePermission,
                context.HttpContext.RequestAborted);
            context.HttpContext.Items[ActorIdItemKey] = actorId;
            return await next(context);
        }
        catch (ProductionManagementUnauthorizedException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (AuthorizationDeniedException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (ProductionBatchException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (ProductionStockEffectException exception)
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
    }

    private static IResult MapError(HttpContext context, Exception exception)
    {
        var (status, code, message) = exception switch
        {
            ProductionManagementUnauthorizedException =>
                (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
            AuthorizationDeniedException =>
                (StatusCodes.Status403Forbidden, "FORBIDDEN", "Üretim yönetimi izni gerekiyor."),
            ProductionBatchNotFoundException =>
                (StatusCodes.Status404NotFound, "NOT_FOUND", "İstenen üretim partisi bulunamadı."),
            ProductionBatchDuplicateNumberException =>
                (StatusCodes.Status409Conflict, "DUPLICATE_BATCH_NUMBER", "Bu numarayla bir üretim partisi zaten var."),
            InvalidProductionBatchTransitionException =>
                (StatusCodes.Status409Conflict, "INVALID_TRANSITION", "Üretim partisi bu durumda bu işlemi kabul etmiyor."),
            RecipeVersionImmutableException =>
                (StatusCodes.Status409Conflict, "RECIPE_VERSION_IMMUTABLE", "Reçete sürümü bu partide değiştirilemez."),
            ProductionBatchConcurrencyException =>
                (StatusCodes.Status409Conflict, "CONCURRENCY_CONFLICT", "Üretim partisi başka bir işlem tarafından değiştirildi."),
            InvalidProductionBatchQuantityException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            InsufficientProductionStockException =>
                (StatusCodes.Status409Conflict, "INSUFFICIENT_STOCK", "Reçete bileşenleri için yeterli stok yok."),
            InvalidProductionStockEffectException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } =>
                (StatusCodes.Status409Conflict, "DUPLICATE_RESOURCE", "Aynı kimlikte bir kayıt zaten var."),
            PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } =>
                (StatusCodes.Status400BadRequest, "REFERENCE_NOT_FOUND", "Başvurulan bir kayıt mevcut değil."),
            PostgresException { SqlState: PostgresErrorCodes.CheckViolation or PostgresErrorCodes.NumericValueOutOfRange } =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek bir veri kısıtını ihlal ediyor."),
            ArgumentException or BadHttpRequestException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            NpgsqlException =>
                (StatusCodes.Status503ServiceUnavailable, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
            _ => throw exception,
        };
        return Results.Json(
            new ProductionApiErrorEnvelopeV1(new ProductionApiErrorV1(code, message, status, context.TraceIdentifier)),
            statusCode: status);
    }
}

public sealed class ProductionManagementUnauthorizedException : Exception
{
    public ProductionManagementUnauthorizedException() : base("A valid production manager session is required.")
    {
    }
}
