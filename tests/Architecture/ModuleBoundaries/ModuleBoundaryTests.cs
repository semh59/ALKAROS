using System.Reflection;
using ALKAROS.Host.Composition.Modules;
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
    // Only the modules that carry code and are in ModuleRegistry.DefaultCatalog.
    // Modules from the domain model that are not implemented yet get their
    // project created together with their first source file (enforced by
    // NoEmptyModuleOrIntegrationProjects).
    private static readonly string[] ModuleAssemblies =
    {
        "ALKAROS.Orders",
        "ALKAROS.Billing",
        "ALKAROS.Kitchen",
        "ALKAROS.Catalog",
        "ALKAROS.Tables",
        "ALKAROS.Cash",
        "ALKAROS.Reporting",
        "ALKAROS.Reconciliation",
        "ALKAROS.Settings",
        "ALKAROS.Identity",
        "ALKAROS.Audit",
        "ALKAROS.Observability",
        "ALKAROS.Operations",
        "ALKAROS.Recipes",
        "ALKAROS.Inventory",
        "ALKAROS.Menu",
        "ALKAROS.Purchasing",
        "ALKAROS.Production",
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

    // Direct-call edges approved in docs/architecture/module-dependency-rules.md
    // (V0-ARC-001). A module's IModule.DependsOn must be a subset of this; a new
    // edge is added here and to the doc together, never silently in code.
    private static readonly Dictionary<string, string[]> ApprovedEdges =
        new(StringComparer.Ordinal)
        {
            ["Orders"] = ["Identity", "Catalog", "Tables"],
            ["Billing"] = ["Orders", "Identity"],
            ["Kitchen"] = ["Orders", "Identity"],
            ["Production"] = ["Inventory"],
            ["Purchasing"] = ["Inventory"],
            // Table Management has no direct-call edge: it reparents orders and
            // bills after a merge/transfer/unmerge by publishing a table event
            // to the outbox, which Order and Bill consume (V0-ARC-001 row 3).
        };

    private static List<(IModule Module, Assembly Assembly)> CatalogModules()
        => ModuleRegistry.DefaultCatalog
            .Select(t => ((IModule)Activator.CreateInstance(t)!, t.Assembly))
            .ToList();

    [Fact]
    public static void DeclaredDependenciesStayWithinTheApprovedEdgeList()
    {
        foreach (var (module, _) in CatalogModules())
        {
            var approved = ApprovedEdges.TryGetValue(module.Id, out var e) ? e : [];
            var extra = module.DependsOn.Except(approved, StringComparer.Ordinal).ToArray();
            Assert.True(
                extra.Length == 0,
                $"Module '{module.Id}' declares dependency on [{string.Join(", ", extra)}] which is not in " +
                "the approved edge list. Add the edge to docs/architecture/module-dependency-rules.md and " +
                "ApprovedEdges together, or remove the dependency.");
        }
    }

    [Fact]
    public static void ActualAssemblyDependenciesAreDeclaredInDependsOn()
    {
        var idByAssemblyName = CatalogModules()
            .ToDictionary(m => m.Assembly.GetName().Name!, m => m.Module.Id, StringComparer.Ordinal);

        foreach (var (module, assembly) in CatalogModules())
        {
            var referencedModuleIds = assembly.GetReferencedAssemblies()
                .Select(a => a.Name)
                .Where(name => name is not null && idByAssemblyName.ContainsKey(name))
                .Select(name => idByAssemblyName[name!])
                .Where(id => id != module.Id)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            var undeclared = referencedModuleIds.Except(module.DependsOn, StringComparer.Ordinal).ToArray();
            Assert.True(
                undeclared.Length == 0,
                $"Module '{module.Id}' has a compile dependency on [{string.Join(", ", undeclared)}] " +
                "(project reference) that is not declared in IModule.DependsOn. Declare it so the composition " +
                "order and the boundary rules stay honest.");
        }
    }

    [Fact]
    public static void NoModuleOrIntegrationProjectIsAnEmptyShell()
    {
        var repoRoot = FindRepoRoot();
        foreach (var area in new[] { "Modules", "Integrations" })
        {
            var areaDir = Path.Combine(repoRoot, "src", area);
            if (!Directory.Exists(areaDir))
                continue;

            foreach (var projectDir in Directory.GetDirectories(areaDir))
            {
                var hasProject = Directory.GetFiles(projectDir, "*.csproj").Length > 0;
                var hasSource = Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories)
                    .Any(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));
                Assert.False(
                    hasProject && !hasSource,
                    $"'{Path.GetRelativePath(repoRoot, projectDir)}' has a .csproj but no source. Create a module " +
                    "or integration project together with its first implementation file, not before.");
            }
        }
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "ALKAROS.slnx")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("Could not locate the repository root.");
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

    [Fact]
    public static void ModuleCompositionRootShouldRetainTypedServiceRegistrations()
    {
        var singleton = new RegistrationImplementation();
        var root = new ModuleCompositionRoot();
        root.AddModule(new TestModule(
            "Registrations",
            "Registrations",
            EmptyDependencies,
            context => context
                .RegisterSingleton<IRegistrationService>(singleton)
                .RegisterSingleton<IRegistrationService, RegistrationImplementation>()
                .RegisterTransient<IRegistrationService, RegistrationImplementation>()));

        root.Compose();

        Assert.Collection(
            root.Services,
            descriptor =>
            {
                Assert.Equal(typeof(IRegistrationService), descriptor.ServiceType);
                Assert.Equal(typeof(RegistrationImplementation), descriptor.ImplementationType);
                Assert.Equal(ModuleContext.ServiceLifetime.Singleton, descriptor.Lifetime);
                Assert.Same(singleton, descriptor.ImplementationInstance);
            },
            descriptor =>
            {
                Assert.Equal(typeof(IRegistrationService), descriptor.ServiceType);
                Assert.Equal(typeof(RegistrationImplementation), descriptor.ImplementationType);
                Assert.Equal(ModuleContext.ServiceLifetime.Singleton, descriptor.Lifetime);
                Assert.Null(descriptor.ImplementationInstance);
            },
            descriptor =>
            {
                Assert.Equal(typeof(IRegistrationService), descriptor.ServiceType);
                Assert.Equal(typeof(RegistrationImplementation), descriptor.ImplementationType);
                Assert.Equal(ModuleContext.ServiceLifetime.Transient, descriptor.Lifetime);
                Assert.Null(descriptor.ImplementationInstance);
            });
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

    private interface IRegistrationService
    {
    }

    private sealed class RegistrationImplementation : IRegistrationService
    {
    }
}
