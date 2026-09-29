using ALKAROS.Host.Composition.Errors;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Menu.CounterProjection;
using ALKAROS.Menu.DailyMenuLifecycle;
using ALKAROS.Menu.StaticMenu;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace ALKAROS.Host.Experience.Menu;

/// <summary>
/// V1-RMD-131: found by an independent audit (2026-09-09) — the entire Menu
/// module (a persistent named-menu catalog and the daily-specials
/// lifecycle, both real, Postgres-backed, and tested) had zero HTTP surface
/// at all. Nothing in Host ever registered its services or mapped a route;
/// no client could reach it. This is that surface, following the same
/// manager-gated pattern as Catalog management (menu composition is the
/// same class of back-of-house configuration work).
/// </summary>
public static class MenuManagementEndpoints
{
    public const string ManagerCookieName = "alkaros.manager";
    public const string ManagePermission = "menu.manage";

    public static IServiceCollection AddMenuManagementExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<ICatalogProductReader, PostgresCatalogProductReader>();
        services.TryAddScoped<IMenuRepository, PostgresMenuRepository>();
        services.TryAddScoped<IMenuItemRepository, PostgresMenuItemRepository>();
        services.TryAddScoped<IStaticMenuService, StaticMenuService>();

        services.TryAddScoped<ICatalogProductPriceReader, PostgresCatalogProductPriceReader>();
        services.TryAddScoped<IRecipeVersionValidator, PostgresRecipeVersionValidator>();
        services.TryAddScoped<IBusinessDateProvider, BusinessDateProvider>();
        services.TryAddScoped<IDailyMenuRepository, PostgresDailyMenuRepository>();
        services.TryAddScoped<IDailyMenuItemRepository, PostgresDailyMenuItemRepository>();
        services.TryAddScoped<IDailyMenuItemHistoryRepository, PostgresDailyMenuItemHistoryRepository>();
        services.TryAddScoped<IDailyMenuService, DailyMenuService>();
        services.TryAddScoped<IDailyMenuCounterProjector, PostgresDailyMenuCounterProjector>();

        // Same rationale as CatalogManagementEndpoints/KitchenOperationsEndpoints's
        // own comment: every Add*Experience must resolve its own filter
        // dependencies standalone, not rely on another module registering them
        // first.
        services.TryAddScoped<IRoleRepository, PostgresRoleRepository>();
        services.TryAddScoped<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddScoped<IAuthorizationService, AuthorizationService>();
        services.TryAddScoped<MenuManagerAuthentication>();
        return services;
    }

    public static RouteGroupBuilder MapMenuManagement(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ApiErrorHandling.EnsureFor(endpoints);
        var group = endpoints.MapGroup("/api/v1/management/menus-and-specials");
        group.AddEndpointFilter<MenuManagerEndpointFilter>();

        // ---- Static menu ----

        group.MapGet("/menus", async (
            bool? activeOnly,
            IStaticMenuService service,
            CancellationToken cancellationToken) =>
        {
            var menus = await service.GetAllMenusAsync(activeOnly ?? false, cancellationToken);
            return Results.Ok(menus.Select(MenuV1.From).ToArray());
        });

        group.MapPost("/menus", async (
            CreateMenuV1 request,
            IStaticMenuService service,
            CancellationToken cancellationToken) =>
        {
            var created = await service.CreateMenuAsync(new CreateMenuCommand(request.Code, request.Name), cancellationToken);
            var dto = MenuV1.From(created);
            return Results.Created($"/api/v1/management/menus-and-specials/menus/{created.Id:D}", dto);
        });

        group.MapGet("/menus/{menuId:guid}", async (
            Guid menuId,
            bool? activeOnly,
            IStaticMenuService service,
            CancellationToken cancellationToken) =>
        {
            var composition = await service.GetMenuCompositionAsync(menuId, activeOnly ?? false, cancellationToken);
            return Results.Ok(MenuCompositionV1.From(composition));
        });

        group.MapPut("/menus/{menuId:guid}", async (
            Guid menuId,
            UpdateMenuV1 request,
            IStaticMenuService service,
            CancellationToken cancellationToken) =>
        {
            var updated = await service.UpdateMenuAsync(
                new UpdateMenuCommand(menuId, request.Name, request.IsActive), cancellationToken);
            return Results.Ok(MenuV1.From(updated));
        });

        group.MapPost("/menus/{menuId:guid}/items", async (
            Guid menuId,
            AddMenuItemV1 request,
            IStaticMenuService service,
            CancellationToken cancellationToken) =>
        {
            var created = await service.AddMenuItemAsync(
                new AddMenuItemCommand(menuId, request.ProductId, request.DisplayOrder), cancellationToken);
            return Results.Created(
                $"/api/v1/management/menus-and-specials/menus/{menuId:D}/items/{created.Id:D}",
                MenuItemV1.From(created));
        });

        group.MapPut("/menus/{menuId:guid}/items/{menuItemId:guid}", async (
            Guid menuId,
            Guid menuItemId,
            UpdateMenuItemV1 request,
            IStaticMenuService service,
            CancellationToken cancellationToken) =>
        {
            _ = menuId;
            var updated = await service.UpdateMenuItemAsync(
                new UpdateMenuItemCommand(menuItemId, request.DisplayOrder, request.IsActive), cancellationToken);
            return Results.Ok(MenuItemV1.From(updated));
        });

        group.MapPost("/menus/{menuId:guid}/items/reorder", async (
            Guid menuId,
            ReorderMenuItemsV1 request,
            IStaticMenuService service,
            CancellationToken cancellationToken) =>
        {
            await service.ReorderMenuItemsAsync(
                new ReorderMenuItemsCommand(menuId, request.OrderedMenuItemIds), cancellationToken);
            return Results.NoContent();
        });

        // ---- Daily menu (specials) ----

        group.MapGet("/daily-menus/{dailyMenuId:guid}", async (
            Guid dailyMenuId,
            bool? activeOnly,
            IDailyMenuService service,
            CancellationToken cancellationToken) =>
        {
            var details = await service.GetDailyMenuDetailsAsync(dailyMenuId, activeOnly ?? false, cancellationToken);
            return Results.Ok(DailyMenuDetailsV1.From(details));
        });

        group.MapGet("/daily-menus/by-date/{businessDate}", async (
            DateOnly businessDate,
            bool? activeOnly,
            IDailyMenuService service,
            CancellationToken cancellationToken) =>
        {
            var details = await service.GetDailyMenuByDateAsync(businessDate, activeOnly ?? false, cancellationToken);
            return details is null ? Results.NotFound() : Results.Ok(DailyMenuDetailsV1.From(details));
        });

        group.MapPost("/daily-menus", async (
            CreateDailyMenuV1 request,
            IDailyMenuService service,
            CancellationToken cancellationToken) =>
        {
            var created = await service.CreateDailyMenuAsync(
                new CreateDailyMenuCommand(request.BusinessDate, request.Note), cancellationToken);
            var dto = DailyMenuV1.From(created);
            return Results.Created($"/api/v1/management/menus-and-specials/daily-menus/{created.Id:D}", dto);
        });

        group.MapPost("/daily-menus/{dailyMenuId:guid}/open", async (
            Guid dailyMenuId,
            OpenDailyMenuV1 request,
            IDailyMenuService service,
            CancellationToken cancellationToken) =>
        {
            var opened = await service.OpenDailyMenuAsync(
                new OpenDailyMenuCommand(dailyMenuId, request.OpenedAt), cancellationToken);
            return Results.Ok(DailyMenuV1.From(opened));
        });

        group.MapPost("/daily-menus/{dailyMenuId:guid}/close", async (
            Guid dailyMenuId,
            CloseDailyMenuV1 request,
            HttpContext context,
            IDailyMenuService service,
            CancellationToken cancellationToken) =>
        {
            var actorId = MenuManagerEndpointFilter.RequireActorId(context);
            var closed = await service.CloseDailyMenuAsync(
                new CloseDailyMenuCommand(dailyMenuId, actorId, request.ClosedAt), cancellationToken);
            return Results.Ok(DailyMenuV1.From(closed));
        });

        group.MapPost("/daily-menus/{dailyMenuId:guid}/items", async (
            Guid dailyMenuId,
            AddDailyMenuItemV1 request,
            HttpContext context,
            IDailyMenuService service,
            CancellationToken cancellationToken) =>
        {
            var actorId = MenuManagerEndpointFilter.RequireActorId(context);
            var created = await service.AddDailyMenuItemAsync(
                new AddDailyMenuItemCommand(
                    dailyMenuId, request.ProductId, request.Price, request.RecipeVersionId,
                    request.PlannedPortions, request.PrinterRoutePolicy, actorId),
                cancellationToken);
            return Results.Created(
                $"/api/v1/management/menus-and-specials/daily-menus/items/{created.Id:D}",
                DailyMenuItemV1.From(created));
        });

        group.MapPut("/daily-menus/items/{dailyMenuItemId:guid}/price", async (
            Guid dailyMenuItemId,
            UpdateDailyMenuItemPriceV1 request,
            HttpContext context,
            IDailyMenuService service,
            CancellationToken cancellationToken) =>
        {
            var actorId = MenuManagerEndpointFilter.RequireActorId(context);
            var updated = await service.UpdateDailyMenuItemPriceAsync(
                new UpdateDailyMenuItemPriceCommand(dailyMenuItemId, request.NewPrice, actorId), cancellationToken);
            return Results.Ok(DailyMenuItemV1.From(updated));
        });

        group.MapPut("/daily-menus/items/{dailyMenuItemId:guid}/planned-portions", async (
            Guid dailyMenuItemId,
            UpdateDailyMenuItemPlannedPortionsV1 request,
            HttpContext context,
            IDailyMenuService service,
            CancellationToken cancellationToken) =>
        {
            var actorId = MenuManagerEndpointFilter.RequireActorId(context);
            var updated = await service.UpdateDailyMenuItemPlannedPortionsAsync(
                new UpdateDailyMenuItemPlannedPortionsCommand(dailyMenuItemId, request.NewPlannedPortions, actorId),
                cancellationToken);
            return Results.Ok(DailyMenuItemV1.From(updated));
        });

        group.MapPut("/daily-menus/items/{dailyMenuItemId:guid}/status", async (
            Guid dailyMenuItemId,
            UpdateDailyMenuItemStatusV1 request,
            HttpContext context,
            IDailyMenuService service,
            CancellationToken cancellationToken) =>
        {
            var actorId = MenuManagerEndpointFilter.RequireActorId(context);
            var updated = await service.UpdateDailyMenuItemStatusAsync(
                new UpdateDailyMenuItemStatusCommand(dailyMenuItemId, request.IsActive, actorId), cancellationToken);
            return Results.Ok(DailyMenuItemV1.From(updated));
        });

        return group;
    }
}

public sealed class MenuManagerAuthentication
{
    private readonly NpgsqlDataSource _dataSource;

    public MenuManagerAuthentication(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<Guid> AuthenticateAsync(HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rawToken = context.Request.Cookies[MenuManagementEndpoints.ManagerCookieName];
        var actorId = await ManagementSessionLookup.ResolveActorAsync(_dataSource, rawToken, allowSupervisor: false, cancellationToken);
        return actorId ?? throw new MenuManagementUnauthorizedException();
    }
}

public sealed class MenuManagerEndpointFilter : IEndpointFilter
{
    private const string ActorIdItemKey = "MenuManagerActorId";

    private readonly MenuManagerAuthentication _authentication;
    private readonly IAuthorizationService _authorization;

    public MenuManagerEndpointFilter(
        MenuManagerAuthentication authentication,
        IAuthorizationService authorization)
    {
        _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
    }

    /// <summary>Retrieves the actor id this filter already authenticated for the current request.</summary>
    public static Guid RequireActorId(HttpContext context)
        => context.Items[ActorIdItemKey] as Guid?
            ?? throw new InvalidOperationException($"{nameof(MenuManagerEndpointFilter)} did not run before this endpoint.");

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        ApiErrorScope.Enter(context.HttpContext, ApiErrorCatalog.MenuManagement);
        var actorId = await _authentication.AuthenticateAsync(
            context.HttpContext,
            context.HttpContext.RequestAborted);
        await _authorization.AuthorizeAsync(
            actorId,
            MenuManagementEndpoints.ManagePermission,
            context.HttpContext.RequestAborted);
        context.HttpContext.Items[ActorIdItemKey] = actorId;
        return await next(context);
    }

}

public sealed class MenuManagementUnauthorizedException : Exception
{
    public MenuManagementUnauthorizedException() : base("A valid menu manager session is required.")
    {
    }
}
