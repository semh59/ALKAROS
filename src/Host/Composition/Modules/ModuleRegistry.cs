using System.Reflection;
using ALKAROS.ModuleComposition;

namespace ALKAROS.Host.Composition.Modules;

/// <summary>
/// Loads modules from the given assemblies and composes them through
/// <see cref="ModuleCompositionRoot"/>. Only concrete <see cref="IModule"/>
/// implementations with a public parameterless constructor that are actually
/// present in the scanned ALKAROS assemblies get registered and loaded;
/// every other assembly is ignored.
/// </summary>
public static class ModuleRegistry
{
    /// <summary>
    /// Returns every candidate <see cref="IModule"/> type declared in the
    /// scanned ALKAROS assemblies, in deterministic assembly order.
    /// </summary>
    public static IReadOnlyList<Type> Discover(IEnumerable<Assembly> assemblies)
    {
        var discovered = new List<Type>();

        foreach (var assembly in assemblies)
        {
            var assemblyName = assembly.GetName().Name;
            if (assemblyName is null || !assemblyName.StartsWith("ALKAROS.", StringComparison.Ordinal))
                continue;

            foreach (var type in assembly.GetExportedTypes())
            {
                if (type.IsAbstract || !typeof(IModule).IsAssignableFrom(type))
                    continue;

                if (type.GetConstructor(Type.EmptyTypes) is not null)
                    discovered.Add(type);
            }
        }

        return discovered;
    }

    /// <summary>
    /// Registers the given module types and returns the module ids in the
    /// validated topological composition order. Throws when the dependency
    /// graph is cyclic, references an unknown module, or declares a duplicate
    /// module id.
    /// </summary>
    public static IReadOnlyList<string> Compose(IEnumerable<Type> moduleTypes)
    {
        var root = BuildRoot(moduleTypes);
        return root.Compose().Select(module => module.Id).ToList();
    }

    /// <summary>
    /// Registers the given module types, runs the validated composition, and
    /// returns the composition root so callers can read the concrete service
    /// registrations produced by the modules.
    /// </summary>
    public static ModuleCompositionRoot ComposeRoot(IEnumerable<Type> moduleTypes)
    {
        var root = BuildRoot(moduleTypes);
        root.Compose();
        return root;
    }

    private static ModuleCompositionRoot BuildRoot(IEnumerable<Type> moduleTypes)
    {
        ArgumentNullException.ThrowIfNull(moduleTypes);

        var root = new ModuleCompositionRoot();

        foreach (var type in moduleTypes)
        {
            var constructor = type.GetConstructor(Type.EmptyTypes)
                ?? throw new InvalidOperationException(
                    $"Module '{type.FullName}' has no public parameterless constructor.");

            root.AddModule((IModule)constructor.Invoke(null));
        }

        return root;
    }
}
