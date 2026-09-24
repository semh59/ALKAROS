using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.ModifierStock;
using ALKAROS.Inventory.PhysicalCounts;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Inventory.Transactions;
using ALKAROS.Inventory.WasteRecording;
using ALKAROS.Measurements;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ALKAROS.Host.Experience.Inventory;

/// <summary>
/// V1-RMD-143: if accepting an order is going to consume real stock,
/// someone first needs to be able to define which product corresponds to
/// which stock item. `inventory.product_stock_mappings` and
/// `StockMasterService` have existed since V1.1 but never had any HTTP
/// surface — the same "built but never called" pattern as the
/// Menu/Purchasing/Production remediations. Manager-only, exactly like the
/// Menu/Purchasing/Production management surfaces.
/// </summary>
public static class StockMasterEndpoints
{
    public const string ManagerCookieName = "alkaros.manager";
    public const string ManagePermission = "inventory.manage";

    public static IServiceCollection AddStockMasterExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<IStockLocationRepository, PostgresStockLocationRepository>();
        services.TryAddScoped<IStockItemRepository, PostgresStockItemRepository>();
        services.TryAddScoped<IProductStockMappingRepository, PostgresProductStockMappingRepository>();
        services.TryAddScoped<IModifierStockMappingRepository, PostgresModifierStockMappingRepository>();
        // StockMasterService's own constructor needs a unit converter for
        // cross-unit BOM validation — the full production host gets this
        // from InventoryModule/RecipesModule, but this self-contained
        // registration set must resolve it standalone too (same reasoning
        // as ProductionManagementExperience's own repository registrations).
        services.TryAddTransient<IUnitConverter, UnitConverter>();
        services.TryAddScoped<IStockMasterService, StockMasterService>();
        // Read-only here (available-quantity display on a product's own
        // mappings) — the same contract OrderStockConsumptionService writes
        // through on Accept.
        services.TryAddScoped<IStockBalanceRepository, PostgresStockBalanceRepository>();
        // V11-INV-008: physical/cycle counts, same TryAdd-defers-to-
        // InventoryModule shape as the registrations above.
        services.TryAddScoped<IStockMovementRepository, PostgresStockMovementRepository>();
        services.TryAddScoped<IInventoryTransactionRunner, PostgresInventoryTransactionRunner>();
        services.TryAddScoped<IPhysicalCountRepository, PostgresPhysicalCountRepository>();
        services.TryAddScoped<IPhysicalCountService, PhysicalCountService>();
        // V1-RMD-274: waste recording (V11-INV-006) - same defer-to-InventoryModule shape.
        services.TryAddScoped<IWasteRecordRepository, PostgresWasteRecordRepository>();
        services.TryAddScoped<IWasteRecordingService, WasteRecordingService>();

        services.TryAddScoped<IRoleRepository, PostgresRoleRepository>();
        services.TryAddScoped<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddScoped<IAuthorizationService, AuthorizationService>();
        services.TryAddScoped<StockMasterAuthentication>();
        return services;
    }

    public static RouteGroupBuilder MapStockMasterApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup("/api/v1/management/inventory");
        group.AddEndpointFilter<StockMasterEndpointFilter>();

        group.MapGet("/stock-locations", async (
            bool? activeOnly,
            IStockLocationRepository repository,
            CancellationToken cancellationToken) =>
        {
            var locations = await repository.GetAllAsync(activeOnly ?? false, cancellationToken);
            return Results.Ok(locations.Select(StockLocationV1.From).ToArray());
        });

        group.MapPost("/stock-locations", async (
            CreateStockLocationV1 request,
            IStockMasterService service,
            CancellationToken cancellationToken) =>
        {
            var locationType = Enum.Parse<StockLocationType>(request.LocationType, ignoreCase: true);
            var created = await service.CreateLocationAsync(
                request.Code, request.Name, locationType, request.IsActive, cancellationToken);
            return Results.Created(
                $"/api/v1/management/inventory/stock-locations/{created.Id:D}", StockLocationV1.From(created));
        });

        group.MapGet("/stock-items", async (
            bool? activeOnly,
            IStockItemRepository repository,
            CancellationToken cancellationToken) =>
        {
            var items = await repository.GetAllAsync(activeOnly ?? false, cancellationToken);
            return Results.Ok(items.Select(StockItemV1.From).ToArray());
        });

        group.MapPost("/stock-items", async (
            CreateStockItemV1 request,
            IStockMasterService service,
            CancellationToken cancellationToken) =>
        {
            var itemType = Enum.Parse<StockItemType>(request.ItemType, ignoreCase: true);
            var created = await service.CreateStockItemAsync(
                request.Code, request.Name, itemType, request.TrackingUnitCode,
                request.DefaultLocationId, request.IsActive, request.ReorderPoint, cancellationToken);
            return Results.Created(
                $"/api/v1/management/inventory/stock-items/{created.Id:D}", StockItemV1.From(created));
        });

        group.MapPost("/products/{productId:guid}/stock-mappings", async (
            Guid productId,
            AssignProductStockMappingV1 request,
            IStockMasterService service,
            CancellationToken cancellationToken) =>
        {
            var mapping = await service.AssignProductToStockItemAsync(
                productId, request.StockItemId, request.QuantityMultiplier, request.Notes, cancellationToken);
            return Results.Ok(mapping);
        });

        // V1-RMD-152: the same three routes for a modifier. Extras draw on
        // the store room too (an extra portion is real food), and a modifier
        // is not a catalog.products row so the product mapping above cannot
        // express it. A modifier with no mapping simply consumes nothing —
        // most modifiers are an instruction, not an ingredient.
        group.MapPost("/modifiers/{modifierId:guid}/stock-mappings", async (
            Guid modifierId,
            AssignModifierStockMappingV1 request,
            IModifierStockMappingRepository mappings,
            IStockItemRepository items,
            CancellationToken cancellationToken) =>
        {
            if (await items.GetByIdAsync(request.StockItemId, cancellationToken) is null)
                return Results.NotFound(new { error = new { code = "NOT_FOUND", message = "Stok kalemi bulunamadı." } });

            var mapping = new ModifierStockMapping(
                modifierId, request.StockItemId, request.QuantityMultiplier, request.Notes);
            await mappings.AddOrUpdateAsync(mapping, cancellationToken);
            return Results.Ok(new ModifierStockMappingV1(
                mapping.ModifierId, mapping.StockItemId, mapping.QuantityMultiplier, mapping.Notes, null));
        });

        group.MapGet("/modifiers/{modifierId:guid}/stock-mappings", async (
            Guid modifierId,
            IModifierStockMappingRepository mappings,
            IStockItemRepository items,
            IStockBalanceRepository balances,
            CancellationToken cancellationToken) =>
        {
            var modifierMappings = await mappings.GetByModifierIdAsync(modifierId, cancellationToken);
            var results = new List<ModifierStockMappingV1>(modifierMappings.Count);
            foreach (var mapping in modifierMappings)
            {
                var stockItem = await items.GetByIdAsync(mapping.StockItemId, cancellationToken);
                decimal? available = null;
                if (stockItem?.DefaultLocationId is { } locationId)
                {
                    var balance = await balances.GetByItemAndLocationAsync(mapping.StockItemId, locationId, cancellationToken);
                    if (balance is not null)
                        available = balance.AvailableQuantity / mapping.QuantityMultiplier;
                }

                results.Add(new ModifierStockMappingV1(
                    mapping.ModifierId, mapping.StockItemId, mapping.QuantityMultiplier, mapping.Notes, available));
            }

            return Results.Ok(results);
        });

        group.MapDelete("/modifiers/{modifierId:guid}/stock-mappings/{stockItemId:guid}", async (
            Guid modifierId,
            Guid stockItemId,
            IModifierStockMappingRepository mappings,
            CancellationToken cancellationToken) =>
        {
            var removed = await mappings.RemoveAsync(modifierId, stockItemId, cancellationToken);
            return removed
                ? Results.NoContent()
                : Results.NotFound(new { error = new { code = "NOT_FOUND", message = "Eşleme bulunamadı." } });
        });

        // Semih's own "kalan stok bilgisi ver garsona" (2026-09-09): the
        // per-item AvailableStockQuantity a staff member sees on a pending
        // order (OrderManagementContracts.OrderItemDto) is this exact same
        // query, run per order item instead of per product here — a manager
        // uses this route to check/configure it directly.
        group.MapGet("/products/{productId:guid}/stock-mappings", async (
            Guid productId,
            IProductStockMappingRepository mappings,
            IStockItemRepository items,
            IStockBalanceRepository balances,
            CancellationToken cancellationToken) =>
        {
            var productMappings = await mappings.GetByProductIdAsync(productId, cancellationToken);
            var results = new List<ProductStockMappingV1>(productMappings.Count);
            foreach (var mapping in productMappings)
            {
                var stockItem = await items.GetByIdAsync(mapping.StockItemId, cancellationToken);
                var stockItemName = stockItem?.Name ?? "?";
                decimal? availableQuantity = null;
                if (stockItem?.DefaultLocationId is { } locationId)
                {
                    var balance = await balances.GetByItemAndLocationAsync(mapping.StockItemId, locationId, cancellationToken);
                    if (balance is not null)
                        availableQuantity = balance.AvailableQuantity / mapping.QuantityMultiplier;
                }

                results.Add(new ProductStockMappingV1(
                    mapping.ProductId, mapping.StockItemId, stockItemName, mapping.QuantityMultiplier,
                    mapping.Notes, availableQuantity));
            }

            return Results.Ok(results);
        });

        // V1-RMD-143 follow-up (Semih, 2026-09-09 deep review): a manager
        // who mapped a product to the wrong stock item had no way to undo
        // it — AssignProductToStockItemAsync only ever upserts, and
        // IProductStockMappingRepository.RemoveAsync already existed but
        // had zero callers anywhere (same "built but never wired" pattern
        // this whole file exists to close). Checks the mapping actually
        // exists first so a caller gets a real 404 instead of a silent
        // no-op DELETE.
        group.MapDelete("/products/{productId:guid}/stock-mappings/{stockItemId:guid}", async (
            Guid productId,
            Guid stockItemId,
            IProductStockMappingRepository mappings,
            CancellationToken cancellationToken) =>
        {
            var productMappings = await mappings.GetByProductIdAsync(productId, cancellationToken);
            if (!productMappings.Any(m => m.StockItemId == stockItemId))
                throw new ProductStockMappingNotFoundException(productId, stockItemId);

            await mappings.RemoveAsync(productId, stockItemId, cancellationToken);
            return Results.NoContent();
        });

        // V11-INV-009: the persisted low-stock threshold read by
        // CriticalStockReport (Reporting) and the future low-stock alert
        // (V11-RPT-002).
        group.MapPut("/stock-items/{stockItemId:guid}/reorder-point", async (
            Guid stockItemId,
            SetReorderPointV1 request,
            IStockMasterService service,
            CancellationToken cancellationToken) =>
        {
            var updated = await service.SetReorderPointAsync(stockItemId, request.ReorderPoint, cancellationToken);
            return Results.Ok(StockItemV1.From(updated));
        });

        // V11-INV-008: a manager/staff member's real shelf count. Records
        // the count regardless of whether it matched the system balance
        // (a match is still evidence for the future AvT report,
        // V11-RPT-003), and applies the real delta through the same
        // guarded transaction ManualAdjustments uses.
        group.MapPost("/stock-items/{stockItemId:guid}/physical-counts", async (
            Guid stockItemId,
            RecordPhysicalCountV1 request,
            IPhysicalCountService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var actorId = StockMasterEndpointFilter.RequireActorId(httpContext);
            var result = await service.RecordPhysicalCountAsync(
                new PhysicalCountRequest(stockItemId, request.StockLocationId, request.CountedQuantity, actorId, request.Notes),
                cancellationToken);
            return Results.Ok(PhysicalCountResultV1.From(result));
        });

        // V1-RMD-274: fire / zayiat. Removes real stock through the same guarded ledger
        // transaction, idempotent on IdempotencyKey, refused (never negative) when the
        // shelf does not hold that much.
        group.MapPost("/stock-items/{stockItemId:guid}/waste", async (
            Guid stockItemId,
            RecordWasteV1 request,
            IWasteRecordingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var actorId = StockMasterEndpointFilter.RequireActorId(httpContext);
            var result = await service.RecordWasteAsync(
                new RecordWasteRequest(
                    stockItemId, request.StockLocationId, request.WasteSource ?? string.Empty, request.Quantity,
                    request.UnitCode ?? string.Empty, request.Reason ?? string.Empty, actorId,
                    request.SourceReferenceId, request.IdempotencyKey),
                cancellationToken);
            return Results.Ok(new RecordWasteResultV1(WasteRecordV1.From(result.Record), result.IsIdempotentReplay));
        });

        group.MapGet("/waste", async (
            string wasteSource,
            Guid sourceReferenceId,
            IWasteRecordingService service,
            CancellationToken cancellationToken) =>
        {
            var records = await service.GetWasteRecordsBySourceAsync(wasteSource, sourceReferenceId, cancellationToken);
            return Results.Ok(records.Select(WasteRecordV1.From).ToArray());
        });

        return group;
    }
}

public sealed class StockMasterAuthentication
{
    private readonly NpgsqlDataSource _dataSource;

    public StockMasterAuthentication(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<Guid> AuthenticateAsync(HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rawToken = context.Request.Cookies[StockMasterEndpoints.ManagerCookieName];
        var actorId = await ManagementSessionLookup.ResolveActorAsync(_dataSource, rawToken, allowSupervisor: false, cancellationToken);
        return actorId ?? throw new StockMasterUnauthorizedException();
    }
}

public sealed class StockMasterEndpointFilter : IEndpointFilter
{
    private const string ActorIdItemKey = "StockMasterActorId";

    private static readonly Action<ILogger, string, string, Exception?> LogRequestFailure =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(5600, nameof(LogRequestFailure)),
            "Stock master request failed on {Path} ({TraceIdentifier}).");

    private readonly StockMasterAuthentication _authentication;
    private readonly IAuthorizationService _authorization;
    private readonly ILogger<StockMasterEndpointFilter> _logger;

    public StockMasterEndpointFilter(
        StockMasterAuthentication authentication, IAuthorizationService authorization, ILogger<StockMasterEndpointFilter> logger)
    {
        _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _logger = logger;
    }

    /// <summary>V11-INV-008: the authenticated actor, for endpoints that record who did something (e.g. a physical count).</summary>
    public static Guid RequireActorId(HttpContext context)
        => context.Items[ActorIdItemKey] as Guid?
            ?? throw new InvalidOperationException($"{nameof(StockMasterEndpointFilter)} did not run before this endpoint.");

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            var actorId = await _authentication.AuthenticateAsync(context.HttpContext, context.HttpContext.RequestAborted);
            await _authorization.AuthorizeAsync(actorId, StockMasterEndpoints.ManagePermission, context.HttpContext.RequestAborted);
            context.HttpContext.Items[ActorIdItemKey] = actorId;
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
                new StockMasterApiErrorEnvelopeV1(new StockMasterApiErrorV1(mapped.Code, mapped.Message, mapped.Status, context.HttpContext.TraceIdentifier)),
                statusCode: mapped.Status);
        }
    }

    private static (int Status, string Code, string Message) Map(Exception exception) => exception switch
    {
        StockMasterUnauthorizedException => (401, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
        AuthorizationDeniedException => (403, "FORBIDDEN", "Stok yönetimi izni gerekiyor."),
        StockItemNotFoundException => (404, "NOT_FOUND", "İstenen stok kalemi bulunamadı."),
        StockLocationNotFoundException => (404, "NOT_FOUND", "İstenen stok konumu bulunamadı."),
        ProductStockMappingNotFoundException => (404, "NOT_FOUND", "Bu ürün için böyle bir stok eşlemesi bulunamadı."),
        DuplicateStockItemException => (409, "DUPLICATE_RESOURCE", "Bu kodla bir stok kalemi zaten var."),
        DuplicateStockLocationException => (409, "DUPLICATE_RESOURCE", "Bu kodla bir stok konumu zaten var."),
        InactiveStockItemException or InactiveStockLocationException => (409, "INACTIVE_RESOURCE", "Bu kayıt pasif durumda."),
        InvalidStockItemException or InvalidStockLocationException or InvalidProductStockMappingException =>
            (400, "VALIDATION_FAILED", "İstek doğrulanamadı."),
        StockMasterConcurrencyException => (409, "CONCURRENCY_CONFLICT", "Kayıt başka bir işlem tarafından değiştirildi."),
        InsufficientStockForWasteException => (409, "INSUFFICIENT_STOCK", "Rafta bu kadar stok yok; fire miktarı mevcut stoktan büyük olamaz."),
        WasteItemNotFoundException or WasteLocationNotFoundException => (404, "NOT_FOUND", "İstenen stok kalemi ya da konumu bulunamadı."),
        UnauthorizedWasteRecorderException => (403, "FORBIDDEN", "Fire kaydı için yetkili bir kullanıcı gerekiyor."),
        InvalidWasteQuantityException or InvalidWasteReasonException or IncompatibleWasteUnitException =>
            (400, "VALIDATION_FAILED", "İstek doğrulanamadı: miktar, birim ve gerekçe geçerli olmalı."),
        WasteRecordingException => (400, "VALIDATION_FAILED", "İstek doğrulanamadı: fire kaynağı geçerli değil."),
        PhysicalCountBalanceGuardFailedException => (409, "CONCURRENCY_CONFLICT", "Sayım uygulanırken bakiye başka bir işlemle çakıştı, tekrar deneyin."),
        PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } => (409, "DUPLICATE_RESOURCE", "Aynı kimlikte bir kayıt zaten var."),
        PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } => (400, "REFERENCE_NOT_FOUND", "Başvurulan bir kayıt mevcut değil."),
        ArgumentException or BadHttpRequestException => (400, "VALIDATION_FAILED", "İstek doğrulanamadı."),
        PostgresException or NpgsqlException => (503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
        _ => (500, "INTERNAL_ERROR", "İşlem tamamlanamadı."),
    };
}

public sealed class StockMasterUnauthorizedException : Exception
{
    public StockMasterUnauthorizedException() : base("A valid inventory manager session is required.")
    {
    }
}

/// <summary>No `inventory.product_stock_mappings` row exists for this exact (productId, stockItemId) pair — nothing to remove.</summary>
public sealed class ProductStockMappingNotFoundException : Exception
{
    public Guid ProductId { get; }
    public Guid StockItemId { get; }

    public ProductStockMappingNotFoundException(Guid productId, Guid stockItemId)
        : base($"Product '{productId}' has no stock mapping to stock item '{stockItemId}'.")
    {
        ProductId = productId;
        StockItemId = stockItemId;
    }
}
