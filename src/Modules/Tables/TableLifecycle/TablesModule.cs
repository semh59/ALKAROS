namespace ALKAROS.Tables.TableLifecycle;

using ALKAROS.ModuleComposition;
using ALKAROS.Tables.FloorPlan;

public sealed class TablesModule : IModule
{
    public string Id => "Tables";

    public string DisplayName => "Table Management";

    // Merge/transfer/unmerge write a table event to the outbox in their own
    // transaction; Order and Bill reparent their rows on delivery. No
    // direct-call edge — the coupling is an integration event (V0-ARC-001
    // row 3), so nothing is declared here.
    public IReadOnlyCollection<string> DependsOn => Array.Empty<string>();

    // Only the table, zone and floor-plan repositories are registered here. The merge, transfer
    // and reservation services and the pointer projector are registered by the Host's
    // AddTableManagementExperience, which every Host test of the table endpoints builds on its
    // own without loading modules; registering them in both places would create two lifetimes.
    public void Register(ModuleContext context)
    {
        context
            .RegisterTransient<ITableRepository, PostgresTableRepository>()
            .RegisterTransient<IZoneRepository, PostgresZoneRepository>()
            .RegisterTransient<ITableFloorPlanRepository, PostgresTableFloorPlanRepository>();
    }
}
