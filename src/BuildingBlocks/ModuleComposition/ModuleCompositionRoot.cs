namespace ALKAROS.ModuleComposition;

/// <summary>
/// Discovers <see cref="IModule"/> implementations, validates that the
/// dependency graph declared through <see cref="IModule.DependsOn"/> is acyclic
/// and that every referenced module id is registered, then invokes
/// <see cref="IModule.Register"/> for each module in topological order.
/// </summary>
public sealed class ModuleCompositionRoot
{
    private readonly Dictionary<string, IModule> _modulesById = new(StringComparer.Ordinal);
    private IReadOnlyList<ModuleContext.ServiceDescriptor> _services =
        Array.Empty<ModuleContext.ServiceDescriptor>();

    /// <summary>
    /// Registrations produced by the latest successful composition.
    /// The host composition adapter consumes these descriptors to build its
    /// concrete service provider.
    /// </summary>
    public IReadOnlyList<ModuleContext.ServiceDescriptor> Services => _services;

    public ModuleCompositionRoot()
    {
    }

    public ModuleCompositionRoot AddModule(IModule module)
    {
        ArgumentNullException.ThrowIfNull(module);

        if (string.IsNullOrWhiteSpace(module.Id))
            throw new ArgumentException("Module Id must be a non-empty value.", nameof(module));

        if (_modulesById.ContainsKey(module.Id))
            throw new InvalidOperationException(
                $"Module '{module.Id}' is already registered.");

        _modulesById.Add(module.Id, module);
        return this;
    }

    /// <summary>
    /// Validate dependencies and register every module in topological order.
    /// Throws if the dependency graph is cyclic or references an unknown module.
    /// </summary>
    public IReadOnlyList<IModule> Compose()
    {
        _services = Array.Empty<ModuleContext.ServiceDescriptor>();
        ValidateReferences();
        var ordered = TopologicalSort();
        var context = new ModuleContext();
        foreach (var module in ordered)
            module.Register(context);

        _services = context.Services.ToArray();
        return ordered;
    }

    private void ValidateReferences()
    {
        foreach (var module in _modulesById.Values)
        {
            foreach (var dependency in module.DependsOn)
            {
                if (!_modulesById.ContainsKey(dependency))
                    throw new InvalidOperationException(
                        $"Module '{module.Id}' depends on unknown module '{dependency}'.");
            }
        }
    }

    private List<IModule> TopologicalSort()
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<IModule>(_modulesById.Count);

        void Visit(string moduleId)
        {
            if (visited.Contains(moduleId))
                return;

            if (!visiting.Add(moduleId))
                throw new InvalidOperationException(
                    $"Cyclic module dependency detected involving '{moduleId}'.");

            var module = _modulesById[moduleId];
            foreach (var dep in module.DependsOn)
                Visit(dep);

            visiting.Remove(moduleId);
            visited.Add(moduleId);
            result.Add(module);
        }

        foreach (var id in _modulesById.Keys)
            Visit(id);

        return result;
    }
}
