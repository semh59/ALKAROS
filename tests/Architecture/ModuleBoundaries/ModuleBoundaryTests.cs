using ALKAROS.ModuleComposition;
using NetArchTest.Rules;
using Xunit;

namespace ALKAROS.Architecture.Tests;

/// <summary>
/// Architecture tests that enforce the module dependency rules locked by
/// V0-ARC-001 in docs/architecture/module-dependency-rules.md.
/// </summary>
public static class ModuleBoundaryTests
{
    private static readonly string[] ModuleAssemblies =
    {
        "ALKAROS.Orders",
        "ALKAROS.Billing",
        "ALKAROS.Payments",
        "ALKAROS.Kitchen",
        "ALKAROS.Catalog",
        "ALKAROS.Tables",
        "ALKAROS.Cash",
        "ALKAROS.Fiscal",
        "ALKAROS.Inventory",
        "ALKAROS.Accounts",
        "ALKAROS.Reporting",
        "ALKAROS.Reconciliation",
        "ALKAROS.Notifications",
        "ALKAROS.Settings",
        "ALKAROS.Identity",
    };

    private static readonly string[] EmptyDependencies = Array.Empty<string>();
    private static readonly string[] DependencyOnA = { "A" };
    private static readonly string[] DependencyOnAB = { "A", "B" };
    private static readonly string[] CyclicDependency = { "Cyclic" };
    private static readonly string[] UnknownDependency = { "NonExistent" };
    private static readonly string[] ExpectedCompositionOrder = { "A", "B", "C" };

    [Fact]
    public static void ModuleCompositionShouldNotDependOnAnyModule()
    {
        var result = Types.InAssembly(typeof(IModule).Assembly)
            .Should()
            .NotHaveDependencyOnAny(ModuleAssemblies)
            .GetResult();

        Assert.True(result.IsSuccessful,
            "ModuleComposition must not depend on any business module. Failures: " +
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public static void ModuleCompositionRootShouldDetectCyclicDependencies()
    {
        var cyclic = new CyclicModule();
        var root = new ModuleCompositionRoot();
        root.AddModule(cyclic);

        Assert.Throws<InvalidOperationException>(() => root.Compose());
    }

    [Fact]
    public static void ModuleCompositionRootShouldRejectUnknownDependency()
    {
        var dependent = new UnknownDependencyModule();
        var root = new ModuleCompositionRoot();
        root.AddModule(dependent);

        Assert.Throws<InvalidOperationException>(() => root.Compose());
    }

    [Fact]
    public static void ModuleCompositionRootShouldComposeInTopologicalOrder()
    {
        var composed = new List<string>();
        var a = new TestModule("A", "A", EmptyDependencies, _ => composed.Add("A"));
        var b = new TestModule("B", "B", DependencyOnA, _ => composed.Add("B"));
        var c = new TestModule("C", "C", DependencyOnAB, _ => composed.Add("C"));

        var root = new ModuleCompositionRoot();
        root.AddModule(c).AddModule(b).AddModule(a);
        root.Compose();

        Assert.Equal(ExpectedCompositionOrder, composed);
    }

    private sealed class TestModule : IModule
    {
        private readonly Action<ModuleContext> _onRegister;

        public TestModule(string id, string display, IReadOnlyCollection<string> dependsOn,
            Action<ModuleContext> onRegister)
        {
            Id = id;
            DisplayName = display;
            DependsOn = dependsOn;
            _onRegister = onRegister;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public IReadOnlyCollection<string> DependsOn { get; }
        public void Register(ModuleContext context) => _onRegister(context);
    }

    private sealed class CyclicModule : IModule
    {
        public string Id => "Cyclic";
        public string DisplayName => "Cyclic";
        public IReadOnlyCollection<string> DependsOn => CyclicDependency;
        public void Register(ModuleContext context) { }
    }

    private sealed class UnknownDependencyModule : IModule
    {
        public string Id => "Dependent";
        public string DisplayName => "Dependent";
        public IReadOnlyCollection<string> DependsOn => UnknownDependency;
        public void Register(ModuleContext context) { }
    }
}