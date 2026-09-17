using ALKAROS.ModuleComposition;

namespace ALKAROS.Cash.SessionLifecycle;

/// <summary>
/// Registers CashSession lifecycle persistence and orchestration
/// (V13-CSH-001), separate from the module-root <c>CashModule</c> (owned
/// by V1-CSH-001) so this task never has to write to that shared file.
/// </summary>
public sealed class CashSessionLifecycleModule : IModule
{
    public string Id => "Cash.SessionLifecycle";
    public string DisplayName => "Cash Session Lifecycle";
    public IReadOnlyCollection<string> DependsOn => ["Cash"];

    public void Register(ModuleContext context)
    {
        context.RegisterTransient<ICashSessionRepository, PostgresCashSessionRepository>();
        context.RegisterTransient<ICashSessionLifecycleService, CashSessionLifecycleService>();
    }
}
