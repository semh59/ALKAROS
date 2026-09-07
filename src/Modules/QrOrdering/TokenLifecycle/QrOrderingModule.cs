using ALKAROS.ModuleComposition;

namespace ALKAROS.QrOrdering.TokenLifecycle;

public sealed class QrOrderingModule : IModule
{
    public string Id => "QrOrdering";

    public string DisplayName => "QR Ordering";

    // Table binding is a read of table_mgmt.tables' existence, not a write —
    // no direct-call edge is declared (V0-ARC-001 row 1: reads across
    // schemas are always allowed without a dependency declaration).
    public IReadOnlyCollection<string> DependsOn => [];

    public void Register(ModuleContext context)
        => context
            .RegisterTransient<ITableTokenRepository, PostgresTableTokenRepository>()
            .RegisterTransient<TableTokenService, TableTokenService>();
}
