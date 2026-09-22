namespace ALKAROS.Operations.OffsiteBackup;

/// <summary>
/// The approved RPO ceiling per <see cref="DataClass"/>
/// (<c>docs/recovery/rpo-rto-targets.md</c> section 2, V0-BKP-002 decision
/// record). Off-site upload freshness is measured against these, not
/// asserted.
/// </summary>
public static class RpoTargets
{
    public static TimeSpan For(DataClass dataClass) => dataClass switch
    {
        DataClass.Fiscal => TimeSpan.FromMinutes(5),
        DataClass.OrdersInventory => TimeSpan.FromHours(1),
        DataClass.Settings => TimeSpan.FromHours(24),
        _ => throw new ArgumentOutOfRangeException(nameof(dataClass), dataClass, "Unknown data class."),
    };
}
