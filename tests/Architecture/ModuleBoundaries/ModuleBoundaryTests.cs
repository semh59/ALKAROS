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
        "ALKAROS.Security",
        "ALKAROS.Support",
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
            // V12-QRO-002: module-dependency-rules.md row 19, approved
            // 2026-08-03, exercised in code for the first time now (same
            // situation as rows 11/27's own notes describe).
            ["QrOrdering"] = ["Tables"],
            // Table Management has no direct-call edge: it reparents orders and
            // bills after a merge/transfer/unmerge by publishing a table event
            // to the outbox, which Order and Bill consume (V0-ARC-001 row 3).
            // V1-RMD-248: found by an independent audit (2026-09-18) — these
            // four real, already-Done (V13-CSH-001/002/003) sub-module edges
            // were never added here, so this whole dictionary construction
            // has been throwing on the very first `dotnet test` run of this
            // suite since V13-CSH-004 registered them into DefaultCatalog.
            // Row 7 of the table below already documents "Cash -> Payment";
            // these are that same edge, split across the row's four separate
            // ALKAROS.Cash/ALKAROS.Payments sub-modules.
            ["Cash.SessionLifecycle"] = ["Cash"],
            ["Cash.TransactionLedger"] = ["Cash"],
            ["Cash.TenderHandler"] = ["Cash", "Cash.TransactionLedger", "Payments", "Payments.Allocations.Persistence", "Billing"],
            ["Payments.Allocations.Persistence"] = ["Payments", "Billing"],
            // V13-PAY-004: same coarse edge module-dependency-rules.md row 6
            // ("Payment -> Bill, Identity") already documents, split into its
            // own IModule for the same reason as Cash.TenderHandler/
            // Payments.Allocations.Persistence above — no new doc row needed.
            ["Payments.CardSettlement"] = ["Payments", "Payments.Allocations.Persistence", "Billing"],
            // V13-PAY-003: same module-dependency-rules.md row 6 edge as
            // Cash.TenderHandler above (this module only bridges into it),
            // plus Cash.TenderHandler itself to reach ICashTenderHandler —
            // no new doc row needed.
            ["Payments.TenderComposition"] = ["Payments", "Cash.TenderHandler"],
            // V15-BKP-001: module-dependency-rules.md row 24 ("Backup"),
            // structured alert logging on upload/RPO failure.
            // V15-BKP-001: OffsiteBackup's envelope encryption is keyed
            // through Security's versioned secret rotation.
            ["Operations"] = ["Observability", "Security"],
            // V15-SUP-001: module-dependency-rules.md row 28 ("Support") -
            // system status summary reuses Observability's health-check
            // query, selected correlation logs and the bundle's own
            // provenance record reuse Audit's existing event store.
            ["Support"] = ["Observability", "Audit"],
            // V15-SEC-001/002/003: module-dependency-rules.md row 29
            // ("Security") - Identity for session/lockout hardening, Audit
            // for the disposal/purge/re-encryption trail.
            ["Security"] = ["Identity", "Audit"],
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
        // V1-RMD-248: found by an independent audit (2026-09-18) — several
        // real assemblies (ALKAROS.Cash, ALKAROS.Payments) deliberately host
        // more than one IModule (e.g. CashModule/CashSessionLifecycleModule/
        // CashTransactionLedgerModule/CashTenderHandlerModule all compile
        // into one ALKAROS.Cash.csproj, same reasoning as
        // CashTenderHandlerModule's own doc comment: a later task never has
        // to write to an earlier task's shared module file). Compile
        // references are a property of the ASSEMBLY, not of any one module
        // inside it — CashTenderHandlerModule's own reference to Payments
        // shows up on every module compiled into ALKAROS.Cash.csproj,
        // including CashModule itself, which has nothing to do with it. So
        // this check is done per assembly: an assembly's declared dependency
        // set is the UNION of every module it hosts' DependsOn, and a
        // reference is satisfied if ANY hosted module declares it.
        var groups = CatalogModules()
            .GroupBy(m => m.Assembly.GetName().Name!, StringComparer.Ordinal)
            .Select(g => new
            {
                AssemblyName = g.Key,
                Assembly = g.First().Assembly,
                HostedIds = g.Select(m => m.Module.Id).ToArray(),
                Declared = g.SelectMany(m => m.Module.DependsOn).ToArray(),
            })
            .ToDictionary(g => g.AssemblyName, StringComparer.Ordinal);

        foreach (var group in groups.Values)
        {
            var referencedModuleIdGroups = group.Assembly.GetReferencedAssemblies()
                .Select(a => a.Name)
                .Where(name => name is not null && groups.ContainsKey(name))
                .Where(name => name != group.AssemblyName)
                .Select(name => groups[name!].HostedIds)
                .ToArray();

            var undeclared = referencedModuleIdGroups
                .Where(ids => !ids.Any(id => group.Declared.Contains(id, StringComparer.Ordinal)))
                .Select(ids => string.Join("/", ids))
                .ToArray();

            Assert.True(
                undeclared.Length == 0,
                $"Assembly '{group.AssemblyName}' (hosting module(s) [{string.Join(", ", group.HostedIds)}]) has " +
                $"a compile dependency on [{string.Join(", ", undeclared)}] (project reference) that is not " +
                "declared in any hosted module's IModule.DependsOn. Declare it so the composition order and " +
                "the boundary rules stay honest.");
        }
    }

    // V1-RMD-159: found by the 2026-09-10 Garson audit — the two checks
    // above only ever see modules registered in ModuleRegistry.DefaultCatalog,
    // so a new cross-module edge written under src/Host/Experience/** (a
    // Host orchestrator calling straight into several modules' public
    // contracts for one same-transaction flow, same shape docs/architecture/
    // module-dependency-rules.md's Enforcement section already describes for
    // Production's own edges) was never checked by anything. The writes
    // still go through each module's own repository contract, so this was
    // never a correctness bug — but an edge could be added or widened here
    // completely silently. Each entry is one deliberate orchestrator
    // namespace and the module assemblies its own doc comment says it
    // calls (docs/architecture/module-dependency-rules.md, Enforcement);
    // add a new orchestrator here in the same diff that introduces it.
    // V1-RMD-215: found by an independent audit (2026-09-16) — the two
    // entries below only ever matched their own narrow sub-namespace, so
    // every other file directly under ALKAROS.Host.Experience.Orders (the
    // whole Orders composition root, OrderManagementEndpoints.cs included)
    // and everything under ALKAROS.Host.Experience.KitchenOperations was
    // never checked by this test at all — a new, unapproved module
    // reference there would compile clean and pass CI silently. The two
    // new root entries are deliberately broader (the union of every module
    // actually referenced anywhere in that namespace tree, `git grep -h
    // "^using ALKAROS\." src/Host/Experience/Orders|KitchenOperations`) so
    // they double-check, never loosen, the two existing narrower entries
    // above (whose own approved lists are each a subset of their area's
    // root entry) while finally giving the root-level files real coverage.
    private static readonly Dictionary<string, string[]> ApprovedHostOrchestrationEdges =
        new(StringComparer.Ordinal)
        {
            ["ALKAROS.Host.Experience.Orders.OrderStockConsumption"] =
                ["ALKAROS.Inventory", "ALKAROS.Orders", "ALKAROS.Recipes"],
            ["ALKAROS.Host.Experience.Orders.SentItemVoid"] =
                ["ALKAROS.Billing", "ALKAROS.Inventory", "ALKAROS.Kitchen", "ALKAROS.Orders"],
            ["ALKAROS.Host.Experience.Orders"] =
                ["ALKAROS.Audit", "ALKAROS.Billing", "ALKAROS.Identity", "ALKAROS.Inventory", "ALKAROS.Kitchen", "ALKAROS.Orders", "ALKAROS.Recipes", "ALKAROS.Settings"],
            ["ALKAROS.Host.Experience.KitchenOperations"] =
                ["ALKAROS.Audit", "ALKAROS.Identity", "ALKAROS.Kitchen", "ALKAROS.Operations", "ALKAROS.Orders", "ALKAROS.Settings"],
            // V11-RCP-003: a manager-only surface saying which recipe a
            // catalog product corresponds to. Orders itself still can't
            // reach Recipe directly (see V0-ARC-001 row 4) — this is a
            // self-contained Host area, not a Orders-module dependency.
            ["ALKAROS.Host.Experience.Recipes"] =
                ["ALKAROS.Identity", "ALKAROS.Recipes"],
        };

    [Fact]
    public static void HostOrchestrationEdgesStayWithinTheApprovedList()
    {
        var hostAssembly = typeof(ALKAROS.Host.Experience.Orders.OrderStockConsumption.OrderStockConsumptionService).Assembly;
        var failures = new List<string>();

        foreach (var (ns, approved) in ApprovedHostOrchestrationEdges)
        {
            var disallowed = ModuleAssemblies.Except(approved, StringComparer.Ordinal).ToArray();
            var result = Types.InAssembly(hostAssembly)
                .That().ResideInNamespaceStartingWith(ns)
                .Should().NotHaveDependencyOnAny(disallowed)
                .GetResult();

            if (!result.IsSuccessful)
            {
                failures.Add(
                    $"'{ns}' has an undeclared dependency. Failing types: " +
                    string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
            }
        }

        Assert.True(
            failures.Count == 0,
            "A Host/Experience orchestrator references a module assembly outside its approved edge list. " +
            "Add the edge to ApprovedHostOrchestrationEdges and docs/architecture/module-dependency-rules.md " +
            "together, or remove the dependency. Failures: " + string.Join("; ", failures));
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
