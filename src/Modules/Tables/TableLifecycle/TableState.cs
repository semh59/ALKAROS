namespace ALKAROS.Tables.TableLifecycle;

/// <summary>
/// Canonical table status values (canonical-value-catalog.md section C,
/// PDF:II.5.15). Occupied is only reported by the billing/orders flow;
/// entering <see cref="Reserved"/> is only issued by the QR order flow
/// (table-reservation-policy.md). Table state is an application-layer
/// invariant and never drives financial coupling at DB level.
/// </summary>
public enum TableState
{
    Available = 1,
    Occupied = 2,
    Reserved = 3,
    Cleaning = 4,
    OutOfService = 5,
}