using ALKAROS.Host.Composition;
using ALKAROS.ModuleComposition;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Tests.Composition;

/// <summary>
/// Verifies that host composition proves the constructability of every
/// registered service at build time (ValidateOnBuild/ValidateScopes) and
/// that a broken constructor graph fails the composition fail-closed before
/// any migration runs.
/// </summary>
public sealed class HostConstructabilityTests
{
    [Fact]
    public void ComposeModulesValidatesEveryRegisteredServiceIsConstructible()
    {
        using var output = new StringWriter();
        using var provider = HostComposition.ComposeModules(
            output,
            moduleTypes: [typeof(GraphModule)]);

        Assert.NotNull(provider);
        Assert.Contains(
            "Modules composed: 3 service(s) registered.",
            output.ToString(),
            StringComparison.Ordinal);

        Assert.IsType<GraphLeaf>(provider.GetRequiredService<IGraphLeaf>());
        Assert.IsType<GraphMid>(provider.GetRequiredService<IGraphMid>());
        Assert.IsType<GraphRoot>(provider.GetRequiredService<IGraphRoot>());
    }

    [Fact]
    public void ComposeModulesFailsClosedWhenRegisteredServiceCannotBeConstructed()
    {
        using var output = new StringWriter();
        var provider = HostComposition.ComposeModules(
            output,
            moduleTypes: [typeof(BrokenGraphModule)]);

        Assert.Null(provider);
        var message = output.ToString();
        Assert.Contains("Module composition failed:", message, StringComparison.Ordinal);
        Assert.Contains("BrokenGraphLeaf", message, StringComparison.Ordinal);
    }

    private interface IGraphLeaf
    {
    }

    private sealed class GraphLeaf : IGraphLeaf
    {
    }

    private interface IGraphMid
    {
    }

    private sealed class GraphMid : IGraphMid
    {
        public GraphMid(IGraphLeaf leaf)
        {
        }
    }

    private interface IGraphRoot
    {
    }

    private sealed class GraphRoot : IGraphRoot
    {
        public GraphRoot(IGraphMid mid)
        {
        }
    }

    private sealed class GraphModule : IModule
    {
        public string Id => "GraphModule";
        public string DisplayName => "GraphModule";
        public IReadOnlyCollection<string> DependsOn => Array.Empty<string>();

        public void Register(ModuleContext context) => context
            .RegisterSingleton<IGraphLeaf, GraphLeaf>()
            .RegisterSingleton<IGraphMid, GraphMid>()
            .RegisterTransient<IGraphRoot, GraphRoot>();
    }

    private interface IBrokenGraphDependency
    {
    }

    private interface IBrokenGraphService
    {
    }

    private sealed class BrokenGraphLeaf : IBrokenGraphService
    {
        public BrokenGraphLeaf(IBrokenGraphDependency dependency)
        {
        }
    }

    private sealed class BrokenGraphModule : IModule
    {
        public string Id => "BrokenGraphModule";
        public string DisplayName => "BrokenGraphModule";
        public IReadOnlyCollection<string> DependsOn => Array.Empty<string>();

        public void Register(ModuleContext context) => context
            .RegisterSingleton<IBrokenGraphService, BrokenGraphLeaf>();
    }
}
