namespace ALKAROS.Operations;

using ALKAROS.ModuleComposition;
using ALKAROS.Operations.BackupHealth;

public sealed class OperationsModule : IModule
{
    public string Id => "Operations";
    public string DisplayName => "System Operations and Backup";
    public IReadOnlyCollection<string> DependsOn => ["Observability"];

    public void Register(ModuleContext context)
    {
        context.RegisterTransient<IBackupHealthRepository, PostgresBackupHealthRepository>();
        context.RegisterTransient<IBackupEngine, LocalBackupEngine>();
        context.RegisterTransient<IBackupHealthService, BackupHealthService>();
    }
}
