using ALKAROS.Host.Composition.Modules;
using ALKAROS.ModuleComposition;
using Xunit;

namespace ALKAROS.Host.Tests.Registry;

public sealed class ModuleRegistryTests
{
    private static readonly string[] ExpectedTopologicalOrder = ["Alpha", "Beta", "Gamma"];

    [Fact]
    public void DiscoversOnlyModulesInAlkarosAssemblies()
    {
        var discovered = ModuleRegistry.Discover([typeof(FakeAlphaModule).Assembly]);

        Assert.Contains(typeof(FakeAlphaModule), discovered);
        Assert.DoesNotContain(discovered, t => !typeof(IModule).IsAssignableFrom(t));
    }

    [Fact]
    public void DiscoversNothingInNonAlkarosAssemblies()
    {
        Assert.Empty(ModuleRegistry.Discover([typeof(string).Assembly]));
    }

    [Fact]
    public void DiscoverSkipsAbstractTypesAndTypesWithoutPublicConstructor()
    {
        var discovered = ModuleRegistry.Discover([typeof(FakeAlphaModule).Assembly]);

        Assert.DoesNotContain(discovered, t => t == typeof(FakeAbstractModule));
        Assert.DoesNotContain(discovered, t => t == typeof(FakeNoPublicConstructorModule));
    }

    [Fact]
    public void ComposeReturnsTopologicalOrder()
    {
        var order = ModuleRegistry.Compose(
            [typeof(FakeGammaModule), typeof(FakeBetaModule), typeof(FakeAlphaModule)]);

        Assert.Equal(ExpectedTopologicalOrder, order);
    }

    [Fact]
    public void ComposeRejectsDuplicateModuleId()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ModuleRegistry.Compose([typeof(FakeAlphaModule), typeof(FakeDuplicateAlphaModule)]));
    }

    [Fact]
    public void ComposeRejectsCyclicDependencies()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ModuleRegistry.Compose([typeof(FakeCyclicAModule), typeof(FakeCyclicBModule)]));
    }

    [Fact]
    public void ComposeRejectsUnknownDependency()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ModuleRegistry.Compose([typeof(FakeUnknownDependencyModule)]));
    }

    public sealed class FakeAlphaModule : IModule
    {
        public string Id => "Alpha";
        public string DisplayName => "Alpha";
        public IReadOnlyCollection<string> DependsOn => [];
        public void Register(ModuleContext context) { }
    }

    public sealed class FakeBetaModule : IModule
    {
        public string Id => "Beta";
        public string DisplayName => "Beta";
        public IReadOnlyCollection<string> DependsOn => ["Alpha"];
        public void Register(ModuleContext context) { }
    }

    public sealed class FakeGammaModule : IModule
    {
        public string Id => "Gamma";
        public string DisplayName => "Gamma";
        public IReadOnlyCollection<string> DependsOn => ["Alpha", "Beta"];
        public void Register(ModuleContext context) { }
    }

    public sealed class FakeDuplicateAlphaModule : IModule
    {
        public string Id => "Alpha";
        public string DisplayName => "DuplicateAlpha";
        public IReadOnlyCollection<string> DependsOn => [];
        public void Register(ModuleContext context) { }
    }

    public sealed class FakeCyclicAModule : IModule
    {
        public string Id => "CyclicA";
        public string DisplayName => "CyclicA";
        public IReadOnlyCollection<string> DependsOn => ["CyclicB"];
        public void Register(ModuleContext context) { }
    }

    public sealed class FakeCyclicBModule : IModule
    {
        public string Id => "CyclicB";
        public string DisplayName => "CyclicB";
        public IReadOnlyCollection<string> DependsOn => ["CyclicA"];
        public void Register(ModuleContext context) { }
    }

    public sealed class FakeUnknownDependencyModule : IModule
    {
        public string Id => "UnknownDependency";
        public string DisplayName => "UnknownDependency";
        public IReadOnlyCollection<string> DependsOn => ["NonExistent"];
        public void Register(ModuleContext context) { }
    }

    public abstract class FakeAbstractModule : IModule
    {
        public string Id => "Abstract";
        public string DisplayName => "Abstract";
        public IReadOnlyCollection<string> DependsOn => [];
        public abstract void Register(ModuleContext context);
    }

    public sealed class FakeNoPublicConstructorModule : IModule
    {
        private FakeNoPublicConstructorModule()
        {
        }

        public string Id => "NoCtor";
        public string DisplayName => "NoCtor";
        public IReadOnlyCollection<string> DependsOn => [];
        public void Register(ModuleContext context) { }
    }
}
