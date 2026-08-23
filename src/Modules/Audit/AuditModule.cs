namespace ALKAROS.Audit;

using ALKAROS.Audit.EventStore;
using ALKAROS.ModuleComposition;

public sealed class AuditModule : IModule
{
    public string Id => "Audit";
    public string DisplayName => "Audit Logging";
    public IReadOnlyCollection<string> DependsOn => [];

    public void Register(ModuleContext context)
    {
        context.RegisterTransient<IAuditEventStore, PostgresAuditEventStore>();
        context.RegisterTransient<IAuditSanitizer, AuditSanitizer>();
    }
}
