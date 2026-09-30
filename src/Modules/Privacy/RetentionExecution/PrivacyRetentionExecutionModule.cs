namespace ALKAROS.Privacy.RetentionExecution;

using ALKAROS.ModuleComposition;

/// <summary>
/// Registers KVKK retention execution. It reads the records it judges (identity, orders, table reservations, customer
/// data and account, invoicing, purchasing) by plain SQL and writes only the privacy schema; scrubbing the fields of a
/// due record belongs to whoever consumes its work item.
/// </summary>
public sealed class PrivacyRetentionExecutionModule : IModule
{
    public string Id => "Privacy.RetentionExecution";
    public string DisplayName => "Privacy - Retention Execution";

    public IReadOnlyCollection<string> DependsOn => [];

    public void Register(ModuleContext context)
        => context.RegisterTransient<IRetentionExecutionService, PostgresRetentionExecutionService>();
}
