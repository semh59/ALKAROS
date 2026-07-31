namespace ALKAROS.ModuleComposition;

/// <summary>
/// Contract that every ALKAROS module implements so the host can discover,
/// register and enforce module boundaries.
/// </summary>
public interface IModule
{
    /// <summary>
    /// Stable, unique, case-sensitive module identifier (e.g. "Orders").
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Modules whose public contracts this module is allowed to depend on.
    /// The composition root verifies that every referenced id is registered
    /// and that the resulting dependency graph is acyclic.
    /// </summary>
    IReadOnlyCollection<string> DependsOn { get; }

    /// <summary>
    /// Register module services into the composition context.
    /// </summary>
    void Register(ModuleContext context);
}