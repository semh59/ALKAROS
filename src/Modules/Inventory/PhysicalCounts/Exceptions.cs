namespace ALKAROS.Inventory.PhysicalCounts;

/// <summary>
/// A physical count's implied decrease would have driven on-hand negative
/// under a concurrent write — the same race the guarded transactional apply
/// (V1-RMD-125) already protects <c>ManualAdjustments</c> against.
/// </summary>
public sealed class PhysicalCountBalanceGuardFailedException : Exception
{
    public PhysicalCountBalanceGuardFailedException(string message) : base(message)
    {
    }
}
