using System.Reflection;
using ALKAROS.Host.Composition;
using ALKAROS.Host.Composition.Modules;
using ALKAROS.ModuleComposition;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Tests.Composition;

public sealed class HostModuleReachabilityTests
{
    [Fact]
    public void DefaultCatalogContainsStandardProductionModules()
    {
        var catalog = ModuleRegistry.DefaultCatalog;

        Assert.NotEmpty(catalog);
        Assert.Equal(4, catalog.Count);
        Assert.All(catalog, type =>
        {
            Assert.True(typeof(IModule).IsAssignableFrom(type));
            Assert.False(type.IsAbstract);
            Assert.NotNull(type.GetConstructor(Type.EmptyTypes));
        });
    }

    [Fact]
    public void DefaultDiscoveryWithDataSourceBuildsValidProvider()
    {
        using var output = new StringWriter();
        using var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=alkaros_test");
        using var provider = HostComposition.ComposeModules(output, moduleTypes: null, dataSource: dataSource);

        Assert.NotNull(provider);
        Assert.Contains("Modules composed: 11 service(s) registered.", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultDiscoveryWithoutDataSourceFailsClosed()
    {
        using var output = new StringWriter();
        var provider = HostComposition.ComposeModules(output, moduleTypes: null, dataSource: null);

        Assert.Null(provider);
        Assert.Contains("Module composition failed:", output.ToString(), StringComparison.Ordinal);
    }
}
