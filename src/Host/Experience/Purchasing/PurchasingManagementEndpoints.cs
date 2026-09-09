using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Purchasing.OrdersAndReceipts;
using ALKAROS.Purchasing.Suppliers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace ALKAROS.Host.Experience.Purchasing;

/// <summary>
/// V1-RMD-132: found by an independent audit (2026-09-09) — Purchasing
/// (supplier master data, purchase orders, goods receipt with variance
/// policy, all real and Postgres-backed) had zero HTTP surface at all.
/// Follows the same manager-gated pattern as Catalog/Menu management.
/// </summary>
public static class PurchasingManagementEndpoints
{
    public const string ManagerCookieName = "alkaros.manager";
    public const string ManagePermission = "purchasing.manage";

    public static IServiceCollection AddPurchasingManagementExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<ISupplierRepository, PostgresSupplierRepository>();
        services.TryAddScoped<ISupplierService, SupplierService>();
        services.TryAddScoped<IPurchaseOrderRepository, PostgresPurchaseOrderRepository>();
        services.TryAddScoped<IGoodsReceiptRepository, PostgresGoodsReceiptRepository>();
        services.TryAddScoped<IPurchasingService, PurchasingService>();
        // PurchasingService.ReceiveGoodsAsync posts the stock movement/balance
        // effect through Inventory's own contract in the same transaction
        // (V0-ARC-001 row 27) — these must resolve standalone too.
        services.TryAddScoped<IStockBalanceRepository, PostgresStockBalanceRepository>();
        services.TryAddScoped<IStockMovementRepository, PostgresStockMovementRepository>();

        services.TryAddScoped<IRoleRepository, PostgresRoleRepository>();
        services.TryAddScoped<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddScoped<IAuthorizationService, AuthorizationService>();
        services.TryAddScoped<PurchasingManagerAuthentication>();
        return services;
    }

    public static RouteGroupBuilder MapPurchasingManagement(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup("/api/v1/management/purchasing");
        group.AddEndpointFilter<PurchasingManagerEndpointFilter>();

        // ---- Suppliers ----

        group.MapGet("/suppliers", async (
            bool? activeOnly,
            ISupplierService service,
            CancellationToken cancellationToken) =>
        {
            var suppliers = await service.ListSuppliersAsync(activeOnly, cancellationToken);
            return Results.Ok(suppliers.Select(SupplierSummaryV1.From).ToArray());
        });

        group.MapPost("/suppliers", async (
            CreateSupplierV1 request,
            ISupplierService service,
            CancellationToken cancellationToken) =>
        {
            var created = await service.CreateSupplierAsync(
                new CreateSupplierCommand(request.Code, request.Name, request.TaxNumber, request.TaxOffice,
                    request.Phone, request.Email, request.Active),
                cancellationToken);
            return Results.Created(
                $"/api/v1/management/purchasing/suppliers/{created.Id:D}",
                SupplierViewV1.From(SupplierAccessPolicy.ProjectToView(created, "Manager")));
        });

        group.MapGet("/suppliers/{supplierId:guid}", async (
            Guid supplierId,
            HttpContext context,
            ISupplierService service,
            CancellationToken cancellationToken) =>
        {
            var role = PurchasingManagerEndpointFilter.RequireActorRole(context);
            var view = await service.GetSupplierViewAsync(supplierId, role, cancellationToken);
            return Results.Ok(SupplierViewV1.From(view));
        });

        group.MapPut("/suppliers/{supplierId:guid}", async (
            Guid supplierId,
            UpdateSupplierV1 request,
            HttpContext context,
            ISupplierService service,
            CancellationToken cancellationToken) =>
        {
            var updated = await service.UpdateSupplierAsync(
                new UpdateSupplierCommand(supplierId, request.Name, request.TaxNumber, request.TaxOffice,
                    request.Phone, request.Email),
                cancellationToken);
            var role = PurchasingManagerEndpointFilter.RequireActorRole(context);
            return Results.Ok(SupplierViewV1.From(SupplierAccessPolicy.ProjectToView(updated, role)));
        });

        group.MapPost("/suppliers/{supplierId:guid}/activate", async (
            Guid supplierId,
            ISupplierService service,
            CancellationToken cancellationToken) =>
        {
            await service.ActivateSupplierAsync(supplierId, cancellationToken);
            return Results.NoContent();
        });

        group.MapPost("/suppliers/{supplierId:guid}/deactivate", async (
            Guid supplierId,
            ISupplierService service,
            CancellationToken cancellationToken) =>
        {
            await service.DeactivateSupplierAsync(supplierId, cancellationToken);
            return Results.NoContent();
        });

        // ---- Purchase orders ----

        group.MapGet("/purchase-orders", async (
            Guid? supplierId,
            string? status,
            IPurchaseOrderRepository repository,
            CancellationToken cancellationToken) =>
        {
            var parsedStatus = string.IsNullOrWhiteSpace(status)
                ? (PurchaseOrderStatus?)null
                : Enum.Parse<PurchaseOrderStatus>(status, ignoreCase: true);
            var orders = await repository.ListAsync(supplierId, parsedStatus, cancellationToken);
            return Results.Ok(orders.Select(PurchaseOrderV1.From).ToArray());
        });

        group.MapPost("/purchase-orders", async (
            CreatePurchaseOrderV1 request,
            IPurchasingService service,
            CancellationToken cancellationToken) =>
        {
            var created = await service.CreatePurchaseOrderAsync(
                new CreatePOCommand(
                    request.OrderNumber, request.SupplierId, request.DestinationLocationId,
                    request.Lines.Select(l => new CreatePOLineDto(l.StockItemId, l.OrderedQuantity, l.UnitCode, l.UnitPrice)).ToArray(),
                    request.Notes, request.Currency),
                cancellationToken);
            return Results.Created(
                $"/api/v1/management/purchasing/purchase-orders/{created.Id:D}", PurchaseOrderV1.From(created));
        });

        group.MapGet("/purchase-orders/{orderId:guid}", async (
            Guid orderId,
            IPurchaseOrderRepository repository,
            CancellationToken cancellationToken) =>
        {
            var order = await repository.GetByIdAsync(orderId, cancellationToken)
                ?? throw new PurchaseOrderNotFoundException(orderId);
            return Results.Ok(PurchaseOrderV1.From(order));
        });

        group.MapPost("/purchase-orders/{orderId:guid}/submit", async (
            Guid orderId,
            IPurchasingService service,
            IPurchaseOrderRepository repository,
            CancellationToken cancellationToken) =>
        {
            await service.SubmitPurchaseOrderAsync(orderId, cancellationToken);
            var order = await repository.GetByIdAsync(orderId, cancellationToken)
                ?? throw new PurchaseOrderNotFoundException(orderId);
            return Results.Ok(PurchaseOrderV1.From(order));
        });

        group.MapPost("/purchase-orders/{orderId:guid}/cancel", async (
            Guid orderId,
            IPurchasingService service,
            IPurchaseOrderRepository repository,
            CancellationToken cancellationToken) =>
        {
            await service.CancelPurchaseOrderAsync(orderId, cancellationToken);
            var order = await repository.GetByIdAsync(orderId, cancellationToken)
                ?? throw new PurchaseOrderNotFoundException(orderId);
            return Results.Ok(PurchaseOrderV1.From(order));
        });

        // ---- Goods receipts ----

        group.MapPost("/purchase-orders/{orderId:guid}/receipts", async (
            Guid orderId,
            ReceiveGoodsV1 request,
            HttpContext context,
            IPurchasingService service,
            CancellationToken cancellationToken) =>
        {
            var actorName = PurchasingManagerEndpointFilter.RequireActorDisplayName(context);
            var receipt = await service.ReceiveGoodsAsync(
                new ReceiveGoodsCommand(
                    request.ReceiptNumber, orderId, actorName,
                    request.DeliveredItems.Select(i => new ReceiveLineItemDto(i.OrderLineId, i.DeliveredQuantity, i.VarianceReason)).ToArray(),
                    request.IsManagerApproved,
                    ApprovedBy: request.IsManagerApproved ? actorName : null,
                    request.Notes),
                cancellationToken);
            return Results.Created(
                $"/api/v1/management/purchasing/receipts/{receipt.Id:D}", GoodsReceiptV1.From(receipt));
        });

        group.MapGet("/purchase-orders/{orderId:guid}/receipts", async (
            Guid orderId,
            IGoodsReceiptRepository repository,
            CancellationToken cancellationToken) =>
        {
            var receipts = await repository.ListByOrderAsync(orderId, cancellationToken);
            return Results.Ok(receipts.Select(GoodsReceiptV1.From).ToArray());
        });

        group.MapGet("/receipts/{receiptId:guid}", async (
            Guid receiptId,
            IGoodsReceiptRepository repository,
            CancellationToken cancellationToken) =>
        {
            var receipt = await repository.GetByIdAsync(receiptId, cancellationToken)
                ?? throw new GoodsReceiptNotFoundException(receiptId);
            return Results.Ok(GoodsReceiptV1.From(receipt));
        });

        return group;
    }
}

public sealed class PurchasingManagerAuthentication
{
    private readonly NpgsqlDataSource _dataSource;

    public PurchasingManagerAuthentication(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<(Guid ActorId, string DisplayName)> AuthenticateAsync(HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rawToken = context.Request.Cookies[PurchasingManagementEndpoints.ManagerCookieName];
        var actorId = await ManagementSessionLookup.ResolveActorAsync(_dataSource, rawToken, allowSupervisor: false, cancellationToken)
            ?? throw new PurchasingManagementUnauthorizedException();

        await using var command = _dataSource.CreateCommand(
            "SELECT display_name FROM identity.users WHERE user_id = @user_id;");
        command.Parameters.AddWithValue("user_id", actorId);
        var displayName = await command.ExecuteScalarAsync(cancellationToken) as string;
        return (actorId, displayName ?? "Manager");
    }
}

public sealed class PurchasingManagerEndpointFilter : IEndpointFilter
{
    private const string ActorRoleItemKey = "PurchasingManagerActorRole";
    private const string ActorDisplayNameItemKey = "PurchasingManagerActorDisplayName";

    private readonly PurchasingManagerAuthentication _authentication;
    private readonly IAuthorizationService _authorization;

    public PurchasingManagerEndpointFilter(
        PurchasingManagerAuthentication authentication,
        IAuthorizationService authorization)
    {
        _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
    }

    /// <summary>The fixed role label passed to SupplierAccessPolicy — every
    /// caller reaching this filter already holds purchasing.manage, the
    /// same bar SupplierAccessPolicy's own "authorized roles" list guards,
    /// so unmasked supplier data is always appropriate here.</summary>
    public static string RequireActorRole(HttpContext context)
        => context.Items[ActorRoleItemKey] as string
            ?? throw new InvalidOperationException($"{nameof(PurchasingManagerEndpointFilter)} did not run before this endpoint.");

    public static string RequireActorDisplayName(HttpContext context)
        => context.Items[ActorDisplayNameItemKey] as string
            ?? throw new InvalidOperationException($"{nameof(PurchasingManagerEndpointFilter)} did not run before this endpoint.");

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        try
        {
            var (actorId, displayName) = await _authentication.AuthenticateAsync(
                context.HttpContext,
                context.HttpContext.RequestAborted);
            await _authorization.AuthorizeAsync(
                actorId,
                PurchasingManagementEndpoints.ManagePermission,
                context.HttpContext.RequestAborted);
            context.HttpContext.Items[ActorRoleItemKey] = "Manager";
            context.HttpContext.Items[ActorDisplayNameItemKey] = displayName;
            return await next(context);
        }
        catch (PurchasingManagementUnauthorizedException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (AuthorizationDeniedException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (SupplierException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (PurchasingException exception)
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
            PurchasingManagementUnauthorizedException =>
                (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
            AuthorizationDeniedException =>
                (StatusCodes.Status403Forbidden, "FORBIDDEN", "Satın alma yönetimi izni gerekiyor."),
            SupplierNotFoundException or PurchaseOrderNotFoundException or GoodsReceiptNotFoundException =>
                (StatusCodes.Status404NotFound, "NOT_FOUND", "İstenen kayıt bulunamadı."),
            DuplicateSupplierCodeException =>
                (StatusCodes.Status409Conflict, "DUPLICATE_CODE", "Bu kodla bir tedarikçi zaten var."),
            DuplicateSupplierTaxNumberException =>
                (StatusCodes.Status409Conflict, "DUPLICATE_TAX_NUMBER", "Bu vergi numarasıyla bir tedarikçi zaten var."),
            DuplicateGoodsReceiptException =>
                (StatusCodes.Status409Conflict, "DUPLICATE_RECEIPT_NUMBER", "Bu numarayla bir mal kabul fişi zaten var."),
            InactiveSupplierException =>
                (StatusCodes.Status409Conflict, "SUPPLIER_INACTIVE", "Tedarikçi pasif; sipariş kabul edemez."),
            PurchaseOrderStatusException =>
                (StatusCodes.Status409Conflict, "INVALID_STATUS", "Sipariş bu durumda bu işlemi kabul etmiyor."),
            VarianceReasonRequiredException =>
                (StatusCodes.Status400BadRequest, "VARIANCE_REASON_REQUIRED", "Sipariş edilenden farklı miktar için gerekçe zorunlu."),
            OverReceiptApprovalRequiredException =>
                (StatusCodes.Status409Conflict, "APPROVAL_REQUIRED", "Tolerans üstü fazla teslimat için yönetici onayı gerekiyor."),
            InvalidSupplierDataException or InvalidPurchaseOrderException or InvalidGoodsReceiptException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            SupplierAccessDeniedException =>
                (StatusCodes.Status403Forbidden, "FORBIDDEN", "Bu tedarikçi verisine erişim izniniz yok."),
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
            new PurchasingApiErrorEnvelopeV1(new PurchasingApiErrorV1(code, message, status, context.TraceIdentifier)),
            statusCode: status);
    }
}

public sealed class PurchasingManagementUnauthorizedException : Exception
{
    public PurchasingManagementUnauthorizedException() : base("A valid purchasing manager session is required.")
    {
    }
}
