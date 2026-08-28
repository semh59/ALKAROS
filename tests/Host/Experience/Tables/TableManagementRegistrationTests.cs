using ALKAROS.Host.Experience.Tables;
using ALKAROS.Tables.CurrentPointers;
using ALKAROS.Tables.Reservations;
using ALKAROS.Tables.TableMerge;
using ALKAROS.Tables.TableTransfer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Tests.Experience.Tables;

public sealed class TableManagementRegistrationTests
{
    [Fact]
    public void RegistrationBuildsTheRealOperationalServiceGraph()
    {
        using var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=not_opened");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(dataSource);
        services.AddTableManagementExperience();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        Assert.IsType<TableTransferService>(provider.GetRequiredService<ITableTransferService>());
        Assert.IsType<TableMergeService>(provider.GetRequiredService<ITableMergeService>());
        Assert.IsType<TableReservationService>(provider.GetRequiredService<ITableReservationService>());
        Assert.IsType<PostgresTablePointerProjector>(provider.GetRequiredService<ITablePointerProjector>());
        Assert.NotNull(provider.GetRequiredService<ZoneConcurrencyStore>());
        Assert.NotNull(provider.GetRequiredService<TableManagementStore>());
    }

    [Fact]
    public void MappingPublishesEveryVersionedTableManagementContract()
    {
        using var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=not_opened");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.Services.AddSingleton(dataSource);
        builder.Services.AddTableManagementExperience();
        using var app = builder.Build();
        app.MapTableManagementApi();

        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => new
            {
                Route = endpoint.RoutePattern.RawText,
                Methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [],
            })
            .ToList();

        AssertRoute(routes, "GET", "/api/v1/terminals/{terminalId:guid}/table-management/zones");
        AssertRoute(routes, "POST", "/api/v1/terminals/{terminalId:guid}/table-management/zones");
        AssertRoute(routes, "PUT", "/api/v1/terminals/{terminalId:guid}/table-management/zones/{zoneId:guid}");
        AssertRoute(routes, "DELETE", "/api/v1/terminals/{terminalId:guid}/table-management/zones/{zoneId:guid}");
        AssertRoute(routes, "GET", "/api/v1/terminals/{terminalId:guid}/table-management/tables");
        AssertRoute(routes, "GET", "/api/v1/terminals/{terminalId:guid}/table-management/tables/{tableId:guid}");
        AssertRoute(routes, "POST", "/api/v1/terminals/{terminalId:guid}/table-management/tables");
        AssertRoute(routes, "PUT", "/api/v1/terminals/{terminalId:guid}/table-management/tables/{tableId:guid}");
        AssertRoute(routes, "POST", "/api/v1/terminals/{terminalId:guid}/table-management/tables/{tableId:guid}/status");
        AssertRoute(routes, "GET", "/api/v1/terminals/{terminalId:guid}/table-management/tables/{tableId:guid}/current-pointer");
        AssertRoute(routes, "POST", "/api/v1/terminals/{terminalId:guid}/table-management/reservations");
        AssertRoute(routes, "POST", "/api/v1/terminals/{terminalId:guid}/table-management/reservations/{reservationId:guid}/claim");
        AssertRoute(routes, "POST", "/api/v1/terminals/{terminalId:guid}/table-management/reservations/{reservationId:guid}/cancel");
        AssertRoute(routes, "POST", "/api/v1/terminals/{terminalId:guid}/table-management/reservations/{reservationId:guid}/expire");
        AssertRoute(routes, "POST", "/api/v1/terminals/{terminalId:guid}/table-management/transfers");
        AssertRoute(routes, "POST", "/api/v1/terminals/{terminalId:guid}/table-management/merges");
        AssertRoute(routes, "POST", "/api/v1/terminals/{terminalId:guid}/table-management/merges/{mergeGroupId:guid}/unmerge");
    }

    private static void AssertRoute(
        IEnumerable<dynamic> routes,
        string method,
        string pattern)
    {
        Assert.Contains(routes, route =>
            string.Equals((string?)route.Route, pattern, StringComparison.Ordinal)
            && ((IReadOnlyList<string>)route.Methods).Contains(method, StringComparer.Ordinal));
    }
}
