using ALKAROS.Host.Experience.WaiterNotifications;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.WaiterNotifications.Tests;

public sealed class WaiterOrderStatusHubRegistrationTests
{
    [Fact]
    public void RegistrationPublishesTheHubRoute()
    {
        using var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=not_opened");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.Services.AddSingleton(dataSource);
        builder.Services.AddSingleton<Host.DualScreen.DualScreenStore>();
        builder.Services.AddWaiterNotificationsExperience();
        using var app = builder.Build();
        app.MapWaiterNotificationsApi();

        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToList();

        Assert.Contains(routes, route => route == WaiterOrderStatusHub.Route);
    }
}
