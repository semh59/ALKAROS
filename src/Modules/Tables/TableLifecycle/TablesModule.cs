namespace ALKAROS.Tables.TableLifecycle;

using ALKAROS.ModuleComposition;
using ALKAROS.Tables.FloorPlan;

public sealed class TablesModule : IModule
{
    public string Id => "Tables";

    public string DisplayName => "Table Management";

    public IReadOnlyCollection<string> DependsOn => Array.Empty<string>();

    public void Register(ModuleContext context)
    {
        context
            .RegisterTransient<ITableRepository, PostgresTableRepository>()
            .RegisterTransient<IZoneRepository, PostgresZoneRepository>()
            .RegisterTransient<ITableFloorPlanRepository, PostgresTableFloorPlanRepository>();
    }
}
