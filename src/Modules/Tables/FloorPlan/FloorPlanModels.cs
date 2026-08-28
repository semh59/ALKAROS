using System.Globalization;
using ALKAROS.Tables.TableLifecycle;

namespace ALKAROS.Tables.FloorPlan;

public enum FloorTableShape
{
    Rectangle,
    Round,
    Square,
}

public sealed record FloorSeat(
    Guid SeatId,
    int Number,
    string Label,
    int X,
    int Y,
    long RowVersion);

public sealed record FloorTableLayout(
    Guid TableId,
    string TableNumber,
    int Capacity,
    bool Active,
    TableState State,
    Guid? CurrentOrderId,
    Guid? CurrentBillId,
    long TableRowVersion,
    int? X,
    int? Y,
    int? Width,
    int? Height,
    FloorTableShape? Shape,
    int? RotationDegrees,
    long LayoutRowVersion,
    Guid? ActiveReservationId,
    int? ReservationPartySize,
    DateTimeOffset? ReservedAt,
    DateTimeOffset? ReservationExpiresAt,
    Guid? MergeGroupId,
    bool IsMergePrimary,
    IReadOnlyList<FloorSeat> Seats);

public sealed record FloorPlanSnapshot(
    Guid ZoneId,
    string ZoneCode,
    string ZoneName,
    int CanvasWidth,
    int CanvasHeight,
    long RowVersion,
    IReadOnlyList<FloorTableLayout> Tables);

public sealed record SaveFloorSeat(
    Guid SeatId,
    long ExpectedRowVersion,
    int Number,
    string Label,
    int X,
    int Y);

public sealed record SaveFloorTable(
    Guid TableId,
    long ExpectedTableRowVersion,
    long ExpectedLayoutRowVersion,
    int X,
    int Y,
    int Width,
    int Height,
    FloorTableShape Shape,
    int RotationDegrees,
    IReadOnlyList<SaveFloorSeat> Seats);

public sealed record SaveFloorPlan(
    Guid ZoneId,
    long ExpectedRowVersion,
    int CanvasWidth,
    int CanvasHeight,
    IReadOnlyList<SaveFloorTable> Tables);

public sealed record FloorPlanWarning(string Code, Guid TableId, string Message);

public sealed record FloorPlanSaveResult(
    FloorPlanSnapshot FloorPlan,
    IReadOnlyList<FloorPlanWarning> Warnings);

public interface ITableFloorPlanRepository
{
    Task<FloorPlanSnapshot?> GetAsync(Guid zoneId, CancellationToken cancellationToken = default);

    Task<FloorPlanSaveResult> SaveAsync(SaveFloorPlan request, CancellationToken cancellationToken = default);
}

public sealed class FloorPlanValidationException : ArgumentException
{
    public FloorPlanValidationException(string message) : base(message) { }
}

public sealed class FloorPlanNotFoundException : KeyNotFoundException
{
    public FloorPlanNotFoundException(string message) : base(message) { }
}

public sealed class FloorPlanConcurrencyException : Exception
{
    public FloorPlanConcurrencyException(string resource, Guid id, long expected, long? actual)
        : base($"Concurrency conflict on {resource} {id}: expected {expected}, actual {actual?.ToString(CultureInfo.InvariantCulture) ?? "missing"}.")
    {
        Resource = resource;
        Id = id;
        Expected = expected;
        Actual = actual;
    }

    public string Resource { get; }

    public Guid Id { get; }

    public long Expected { get; }

    public long? Actual { get; }
}
