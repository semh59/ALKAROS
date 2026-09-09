using System.Net;
using System.Net.Http.Json;
using ALKAROS.Host.Experience.Menu;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Menu.Tests;

/// <summary>
/// V1-RMD-131: found by an independent audit (2026-09-09) — the Menu module
/// (persistent named menus + the daily-specials lifecycle) had zero HTTP
/// surface at all, even though every service/repository behind it was real
/// and already tested. These are the first end-to-end tests proving a real
/// HTTP client can reach it.
/// </summary>
[Collection("Menu management PostgreSQL HTTP")]
public sealed class MenuManagementHttpTests : IAsyncLifetime
{
    private readonly MenuManagementTestDatabase _database = new();
    private WebApplication? _application;
    private Uri? _baseAddress;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddMenuManagementExperience();

        _application = builder.Build();
        _application.MapMenuManagement();
        await _application.StartAsync();
        var addresses = _application.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>();
        _baseAddress = new Uri(Assert.Single(addresses!.Addresses), UriKind.Absolute);
    }

    public async Task DisposeAsync()
    {
        if (_application is not null)
            await _application.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task MissingAndUnpermissionedSessionsAreRejectedWithoutMutation()
    {
        var request = new CreateMenuV1("AUTH-" + Guid.NewGuid().ToString("N")[..8], "Protected menu");

        using var anonymous = CreateClient(null);
        using var unauthorized = await anonymous.PostAsJsonAsync("/api/v1/management/menus-and-specials/menus", request);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal("UNAUTHORIZED", (await ReadErrorAsync(unauthorized)).Error.Code);

        using var denied = CreateClient(MenuManagementTestDatabase.DeniedToken);
        using var forbidden = await denied.PostAsJsonAsync("/api/v1/management/menus-and-specials/menus", request);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("FORBIDDEN", (await ReadErrorAsync(forbidden)).Error.Code);

        Assert.Equal(0L, await ScalarAsync<long>(
            "SELECT count(*) FROM menu.menus WHERE code = @value;", request.Code.ToUpperInvariant()));
    }

    [Fact]
    public async Task ManagerCanCreateComposeAndReorderAStaticMenuOverHttp()
    {
        using var client = CreateClient(MenuManagementTestDatabase.ManagerToken);
        var productId = await _database.SeedProductAsync("Latte");
        var secondProductId = await _database.SeedProductAsync("Espresso");
        var code = "MENU-" + Guid.NewGuid().ToString("N")[..8];

        using var createResponse = await client.PostAsJsonAsync(
            "/api/v1/management/menus-and-specials/menus", new CreateMenuV1(code, "Breakfast"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var menu = await createResponse.Content.ReadFromJsonAsync<MenuV1>();
        Assert.NotNull(menu);
        Assert.Equal(code.ToUpperInvariant(), menu!.Code); // Menu.Create normalizes the code to uppercase

        using var firstItem = await client.PostAsJsonAsync(
            $"/api/v1/management/menus-and-specials/menus/{menu.Id:D}/items",
            new AddMenuItemV1(productId, 0));
        Assert.Equal(HttpStatusCode.Created, firstItem.StatusCode);
        var firstItemDto = await firstItem.Content.ReadFromJsonAsync<MenuItemV1>();

        using var secondItem = await client.PostAsJsonAsync(
            $"/api/v1/management/menus-and-specials/menus/{menu.Id:D}/items",
            new AddMenuItemV1(secondProductId, 1));
        Assert.Equal(HttpStatusCode.Created, secondItem.StatusCode);
        var secondItemDto = await secondItem.Content.ReadFromJsonAsync<MenuItemV1>();

        using var composition = await client.GetAsync($"/api/v1/management/menus-and-specials/menus/{menu.Id:D}");
        Assert.Equal(HttpStatusCode.OK, composition.StatusCode);
        var compositionDto = await composition.Content.ReadFromJsonAsync<MenuCompositionV1>();
        Assert.Equal(2, compositionDto!.Items.Count);

        using var reorder = await client.PostAsJsonAsync(
            $"/api/v1/management/menus-and-specials/menus/{menu.Id:D}/items/reorder",
            new ReorderMenuItemsV1([secondItemDto!.Id, firstItemDto!.Id]));
        Assert.Equal(HttpStatusCode.NoContent, reorder.StatusCode);

        using var updateMenu = await client.PutAsJsonAsync(
            $"/api/v1/management/menus-and-specials/menus/{menu.Id:D}",
            new UpdateMenuV1("Weekend Breakfast", true));
        Assert.Equal(HttpStatusCode.OK, updateMenu.StatusCode);
        var updatedMenu = await updateMenu.Content.ReadFromJsonAsync<MenuV1>();
        Assert.Equal("Weekend Breakfast", updatedMenu!.Name);
    }

    [Fact]
    public async Task CreatingAMenuWithADuplicateCodeIsRejected()
    {
        using var client = CreateClient(MenuManagementTestDatabase.ManagerToken);
        var code = "DUP-" + Guid.NewGuid().ToString("N")[..8];
        var first = await client.PostAsJsonAsync("/api/v1/management/menus-and-specials/menus", new CreateMenuV1(code, "First"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var duplicate = await client.PostAsJsonAsync(
            "/api/v1/management/menus-and-specials/menus", new CreateMenuV1(code, "Second"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("DUPLICATE_CODE", (await ReadErrorAsync(duplicate)).Error.Code);
    }

    [Fact]
    public async Task ManagerCanRunTheFullDailyMenuLifecycleOverHttp()
    {
        using var client = CreateClient(MenuManagementTestDatabase.ManagerToken);
        var productId = await _database.SeedProductAsync("Soup of the day");
        // A random future business date avoids colliding with another test's
        // daily menu for "today" under the shared-collection, no-parallelism
        // convention this file already opts into.
        var businessDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(Random.Shared.Next(1, 3650)));

        using var createResponse = await client.PostAsJsonAsync(
            "/api/v1/management/menus-and-specials/daily-menus",
            new CreateDailyMenuV1(businessDate, "Test service day"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var dailyMenu = await createResponse.Content.ReadFromJsonAsync<DailyMenuV1>();
        Assert.Equal("Draft", dailyMenu!.Status);

        using var duplicateDate = await client.PostAsJsonAsync(
            "/api/v1/management/menus-and-specials/daily-menus",
            new CreateDailyMenuV1(businessDate, "Duplicate"));
        Assert.Equal(HttpStatusCode.Conflict, duplicateDate.StatusCode);
        Assert.Equal("DUPLICATE_BUSINESS_DATE", (await ReadErrorAsync(duplicateDate)).Error.Code);

        using var openResponse = await client.PostAsJsonAsync(
            $"/api/v1/management/menus-and-specials/daily-menus/{dailyMenu.Id:D}/open",
            new OpenDailyMenuV1(null));
        Assert.Equal(HttpStatusCode.OK, openResponse.StatusCode);
        var opened = await openResponse.Content.ReadFromJsonAsync<DailyMenuV1>();
        Assert.Equal("Open", opened!.Status);

        using var addItem = await client.PostAsJsonAsync(
            $"/api/v1/management/menus-and-specials/daily-menus/{dailyMenu.Id:D}/items",
            new AddDailyMenuItemV1(productId, 45.5m, null, 20m, null));
        Assert.Equal(HttpStatusCode.Created, addItem.StatusCode);
        var item = await addItem.Content.ReadFromJsonAsync<DailyMenuItemV1>();
        Assert.Equal(45.5m, item!.Price);

        using var updatePrice = await client.PutAsJsonAsync(
            $"/api/v1/management/menus-and-specials/daily-menus/items/{item.Id:D}/price",
            new UpdateDailyMenuItemPriceV1(50m));
        Assert.Equal(HttpStatusCode.OK, updatePrice.StatusCode);
        Assert.Equal(50m, (await updatePrice.Content.ReadFromJsonAsync<DailyMenuItemV1>())!.Price);

        using var updatePortions = await client.PutAsJsonAsync(
            $"/api/v1/management/menus-and-specials/daily-menus/items/{item.Id:D}/planned-portions",
            new UpdateDailyMenuItemPlannedPortionsV1(30m));
        Assert.Equal(HttpStatusCode.OK, updatePortions.StatusCode);
        Assert.Equal(30m, (await updatePortions.Content.ReadFromJsonAsync<DailyMenuItemV1>())!.PlannedPortions);

        using var updateStatus = await client.PutAsJsonAsync(
            $"/api/v1/management/menus-and-specials/daily-menus/items/{item.Id:D}/status",
            new UpdateDailyMenuItemStatusV1(false));
        Assert.Equal(HttpStatusCode.OK, updateStatus.StatusCode);
        Assert.False((await updateStatus.Content.ReadFromJsonAsync<DailyMenuItemV1>())!.IsActive);

        using var byDate = await client.GetAsync(
            $"/api/v1/management/menus-and-specials/daily-menus/by-date/{businessDate:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, byDate.StatusCode);
        var byDateDetails = await byDate.Content.ReadFromJsonAsync<DailyMenuDetailsV1>();
        Assert.Single(byDateDetails!.Items);

        using var closeResponse = await client.PostAsJsonAsync(
            $"/api/v1/management/menus-and-specials/daily-menus/{dailyMenu.Id:D}/close",
            new CloseDailyMenuV1(null));
        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);
        Assert.Equal("Closed", (await closeResponse.Content.ReadFromJsonAsync<DailyMenuV1>())!.Status);

        using var addAfterClose = await client.PostAsJsonAsync(
            $"/api/v1/management/menus-and-specials/daily-menus/{dailyMenu.Id:D}/items",
            new AddDailyMenuItemV1(productId, 10m, null, 5m, null));
        Assert.Equal(HttpStatusCode.Conflict, addAfterClose.StatusCode);
        Assert.Equal("DAILY_MENU_CLOSED", (await ReadErrorAsync(addAfterClose)).Error.Code);
    }

    [Fact]
    public async Task UnknownDailyMenuByDateReturnsNotFound()
    {
        using var client = CreateClient(MenuManagementTestDatabase.ManagerToken);
        var farFuture = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(50);

        using var response = await client.GetAsync(
            $"/api/v1/management/menus-and-specials/daily-menus/by-date/{farFuture:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpClient CreateClient(string? token)
    {
        var client = new HttpClient { BaseAddress = _baseAddress };
        if (token is not null)
            client.DefaultRequestHeaders.Add("Cookie", $"{MenuManagementEndpoints.ManagerCookieName}={token}");
        return client;
    }

    private static async Task<MenuApiErrorEnvelopeV1> ReadErrorAsync(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<MenuApiErrorEnvelopeV1>()
            ?? throw new InvalidOperationException("Expected a menu management error response.");

    private async Task<T> ScalarAsync<T>(string sql, object value)
    {
        await using var command = _database.DataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("value", value);
        return (T)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Scalar result was null."));
    }
}

[CollectionDefinition("Menu management PostgreSQL HTTP", DisableParallelization = true)]
public sealed class MenuManagementPostgresqlDefinition;
