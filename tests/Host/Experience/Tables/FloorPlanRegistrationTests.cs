using ALKAROS.Host.Experience.Tables;
using ALKAROS.Tables.FloorPlan;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Tests.Experience.Tables;

public sealed class FloorPlanRegistrationTests
{
    [Fact]
    public void RegistrationUsesPostgresqlAndPublishesVersionedFloorPlanRoutes()
    {
        using var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=not_opened");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.Services.AddSingleton(dataSource);
        builder.Services.AddTableManagementExperience();
        using var app = builder.Build();
        app.MapTableManagementApi();

        Assert.IsType<PostgresTableFloorPlanRepository>(
            app.Services.GetRequiredService<ITableFloorPlanRepository>());

        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => new
            {
                Route = endpoint.RoutePattern.RawText,
                Methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [],
            })
            .ToList();

        AssertRoute(routes, "GET", "/api/v1/terminals/{terminalId:guid}/table-management/floor-plans/{zoneId:guid}");
        AssertRoute(routes, "PUT", "/api/v1/terminals/{terminalId:guid}/table-management/floor-plans/{zoneId:guid}");
    }

    private static void AssertRoute(IEnumerable<dynamic> routes, string method, string pattern)
    {
        Assert.Contains(routes, route =>
            string.Equals((string?)route.Route, pattern, StringComparison.Ordinal)
            && ((IReadOnlyList<string>)route.Methods).Contains(method, StringComparer.Ordinal));
    }
}
