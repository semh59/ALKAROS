namespace ALKAROS.Tables.TableLifecycle;

using ALKAROS.ModuleComposition;
using ALKAROS.Tables.FloorPlan;

public sealed class TablesModule : IModule
{
    public string Id => "Tables";

    public string DisplayName => "Table Management";

    // Table merge/transfer/unmerge reparent active orders and bills to the new
    // table inside the same transaction, through the Order and Bill module
    // contracts (V0-ARC-001: Tables -> Order, Bill same-transaction reparent).
    public IReadOnlyCollection<string> DependsOn => ["Orders", "Billing"];

    public void Register(ModuleContext context)
    {
        context
            .RegisterTransient<ITableRepository, PostgresTableRepository>()
            .RegisterTransient<IZoneRepository, PostgresZoneRepository>()
            .RegisterTransient<ITableFloorPlanRepository, PostgresTableFloorPlanRepository>();
    }
}
