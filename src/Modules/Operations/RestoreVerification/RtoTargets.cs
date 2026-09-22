using ALKAROS.Operations.OffsiteBackup;

namespace ALKAROS.Operations.RestoreVerification;

/// <summary>
/// The approved RTO ceiling per <see cref="DataClass"/>
/// (<c>docs/recovery/rpo-rto-targets.md</c> section 2, V0-BKP-002 decision
/// record). A restore drill's measured duration is compared against these,
/// not asserted.
/// </summary>
public static class RtoTargets
{
    public static TimeSpan For(DataClass dataClass) => dataClass switch
    {
        DataClass.Fiscal => TimeSpan.FromHours(2),
        DataClass.OrdersInventory => TimeSpan.FromHours(4),
        DataClass.Settings => TimeSpan.FromHours(8),
        _ => throw new ArgumentOutOfRangeException(nameof(dataClass), dataClass, "Unknown data class."),
    };
}
