using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Tables.CurrentPointers;
using ALKAROS.Tables.FloorPlan;
using ALKAROS.Tables.TableLifecycle;
using ALKAROS.Tables.TableMerge;

namespace ALKAROS.Host.Experience.Tables;

public sealed record CreateZoneRequest(long? ExpectedRowVersion, string Code, string Name, int SortOrder = 0, bool Active = true);

public sealed record UpdateZoneRequest(long? ExpectedRowVersion, string Code, string Name, int SortOrder, bool Active);

public sealed record ZoneDto(Guid ZoneId, string Code, string Name, int SortOrder, bool Active, long RowVersion);

public sealed record CreateTableRequest(
    long? ExpectedRowVersion,
    string TableNumber,
    Guid? ZoneId,
    int Capacity = 0,
    bool Active = true);

public sealed record UpdateTableRequest(
    long? ExpectedRowVersion,
    string TableNumber,
    Guid? ZoneId,
    int Capacity,
    bool Active);

public sealed record ChangeTableStatusRequest(long? ExpectedRowVersion, string Status);

public sealed record SaveFloorPlanRequest(
    long? ExpectedRowVersion,
    int CanvasWidth,
    int CanvasHeight,
    IReadOnlyList<SaveFloorTableRequest> Tables);

public sealed record SaveFloorTableRequest(
    Guid TableId,
    long? ExpectedTableRowVersion,
    long? ExpectedLayoutRowVersion,
    int X,
    int Y,
    int Width,
    int Height,
    string Shape,
    int RotationDegrees,
    IReadOnlyList<SaveFloorSeatRequest> Seats);

public sealed record SaveFloorSeatRequest(
    Guid SeatId,
    long? ExpectedRowVersion,
    int Number,
    string Label,
    int X,
    int Y);

public sealed record FloorPlanSeatDto(
    Guid SeatId,
    int Number,
    string Label,
    int X,
    int Y,
    long RowVersion);

public sealed record FloorPlanTableDto(
    Guid TableId,
    string TableNumber,
    int Capacity,
    bool Active,
    string Status,
    Guid? CurrentOrderId,
    Guid? CurrentBillId,
    long TableRowVersion,
    int? X,
    int? Y,
    int? Width,
    int? Height,
    string? Shape,
    int? RotationDegrees,
    long LayoutRowVersion,
    Guid? ActiveReservationId,
    int? ReservationPartySize,
    DateTimeOffset? ReservedAt,
    DateTimeOffset? ReservationExpiresAt,
    Guid? MergeGroupId,
    bool IsMergePrimary,
    IReadOnlyList<FloorPlanSeatDto> Seats,
    IReadOnlyList<string> AllowedCommands);

public sealed record FloorPlanDto(
    Guid ZoneId,
    string ZoneCode,
    string ZoneName,
    int CanvasWidth,
    int CanvasHeight,
    long RowVersion,
    IReadOnlyList<FloorPlanTableDto> Tables);

public sealed record FloorPlanWarningDto(string Code, Guid TableId, string Message);

public sealed record SaveFloorPlanResponse(
    FloorPlanDto FloorPlan,
    IReadOnlyList<FloorPlanWarningDto> Warnings);

public sealed record TableDto(
    Guid TableId,
    string TableNumber,
    Guid? ZoneId,
    int Capacity,
    bool Active,
    string Status,
    Guid? CurrentOrderId,
    Guid? CurrentBillId,
    long RowVersion,
    IReadOnlyList<string> AllowedCommands);

public sealed record CreateTableReservationRequest(
    Guid TableId,
    long? ExpectedTableRowVersion,
    Guid? OrderId,
    string Reason,
    int PartySize = 1,
    DateTimeOffset? ReservedAt = null,
    DateTimeOffset? ExpiresAt = null);

public sealed record ClaimTableReservationRequest(
    long? ExpectedReservationRowVersion,
    long? ExpectedTableRowVersion,
    Guid? OrderId,
    DateTimeOffset? ClaimedAt = null);

public sealed record CancelTableReservationRequest(
    long? ExpectedReservationRowVersion,
    long? ExpectedTableRowVersion,
    string Reason,
    DateTimeOffset? CancelledAt = null);

public sealed record ExpireTableReservationRequest(
    long? ExpectedReservationRowVersion,
    long? ExpectedTableRowVersion,
    string Reason,
    DateTimeOffset? ExpiredAt = null);

public sealed record TransferTableRequest(
    Guid SourceTableId,
    long? ExpectedSourceRowVersion,
    Guid TargetTableId,
    long? ExpectedTargetRowVersion,
    string Reason,
    DateTimeOffset? TransferredAt = null);

public sealed record MergeTablesRequest(
    Guid PrimaryTableId,
    long? ExpectedPrimaryRowVersion,
    IReadOnlyList<TableMergeParticipantRequest> Participants,
    string Reason,
    DateTimeOffset? MergedAt = null);

public sealed record TableMergeParticipantRequest(Guid TableId, long? ExpectedRowVersion);

public sealed record UnmergeTablesRequest(
    long? ExpectedPrimaryRowVersion,
    IReadOnlyList<TableMergeParticipantRequest> Participants,
    string Reason,
    DateTimeOffset? UnmergedAt = null);

public sealed record TablePointerDto(
    Guid TableId,
    string TableNumber,
    string CurrentStatus,
    string ProjectedStatus,
    Guid? CurrentOrderId,
    Guid? AuthoritativeOrderId,
    Guid? CurrentBillId,
    Guid? AuthoritativeBillId,
    string DriftTypes,
    bool HasDrift)
{
    internal static TablePointerDto From(TablePointerDiscrepancy value) => new(
        value.TableId,
        value.TableNumber,
        value.CurrentStatus,
        value.ProjectedStatus,
        value.CurrentOrderId,
        value.AuthoritativeOrderId,
        value.CurrentBillId,
        value.AuthoritativeBillId,
        value.DriftTypes.ToString(),
        value.HasDrift);
}

public sealed record TableManagementErrorEnvelope(
    [property: JsonPropertyName("error")] TableManagementError Error);

public sealed record TableManagementError(string Code, string Message, int Status, string TraceId);

internal sealed record TableManagementPrincipal(Guid UserId, IReadOnlySet<string> Permissions);

internal static class TableContractMapper
{
    // Which granular permission (model §3) each table command needs. A command
    // absent from this map is always allowed. No table command is
    // grant-reachable, so AllowedCommands is "held permissions" only.
    private static readonly Dictionary<string, string> CommandPermission =
        new(StringComparer.Ordinal)
        {
            ["Update"] = ApplicationPermissions.FloorplanManage,
            ["SetOccupied"] = ApplicationPermissions.TablesStatus,
            ["SetAvailable"] = ApplicationPermissions.TablesStatus,
            ["SetCleaning"] = ApplicationPermissions.TablesStatus,
            ["SetOutOfService"] = ApplicationPermissions.TablesStatus,
            ["Reserve"] = ApplicationPermissions.TablesReserve,
            ["ClaimReservation"] = ApplicationPermissions.TablesReserve,
            ["CancelReservation"] = ApplicationPermissions.TablesReserve,
            ["Transfer"] = ApplicationPermissions.TablesTransfer,
            ["Merge"] = ApplicationPermissions.TablesMerge,
            ["Unmerge"] = ApplicationPermissions.TablesMerge,
        };

    public static TableDto ToDto(Table table, IReadOnlySet<string> permissions) => new(
        table.Id,
        table.TableNumber,
        table.ZoneId,
        table.Capacity,
        table.Active,
        table.State.ToString(),
        table.CurrentOrderId,
        table.CurrentBillId,
        table.RowVersion,
        AllowedCommands(table, permissions));

    public static IReadOnlyList<string> AllowedCommands(Table table, IReadOnlySet<string> permissions)
    {
        IReadOnlyList<string> candidates = !table.Active
            ? ["Update"]
            : table.State switch
            {
                TableState.Available => ["Update", "SetOccupied", "Reserve", "SetOutOfService"],
                TableState.Occupied => ["Update", "SetAvailable", "Reserve", "Transfer", "Merge"],
                TableState.Reserved => ["Update", "SetAvailable", "ClaimReservation", "CancelReservation"],
                TableState.Cleaning => ["Update", "SetAvailable"],
                TableState.OutOfService => ["Update", "SetAvailable", "SetCleaning"],
                _ => [],
            };

        return candidates
            .Where(command => !CommandPermission.TryGetValue(command, out var code)
                              || permissions.Contains(code))
            .ToArray();
    }

    public static SaveFloorPlan ToCommand(Guid zoneId, SaveFloorPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tables = request.Tables ?? throw new ArgumentException("Floor-plan tables are required.", nameof(request));
        return new SaveFloorPlan(
            zoneId,
            RequiredVersionOrCreate(request.ExpectedRowVersion, nameof(request.ExpectedRowVersion)),
            request.CanvasWidth,
            request.CanvasHeight,
            tables.Select(table => new SaveFloorTable(
                table.TableId,
                RequiredVersion(table.ExpectedTableRowVersion, nameof(table.ExpectedTableRowVersion)),
                RequiredVersionOrCreate(table.ExpectedLayoutRowVersion, nameof(table.ExpectedLayoutRowVersion)),
                table.X,
                table.Y,
                table.Width,
                table.Height,
                ParseShape(table.Shape),
                table.RotationDegrees,
                (table.Seats ?? throw new ArgumentException("Table seats are required.", nameof(request)))
                    .Select(seat => new SaveFloorSeat(
                        seat.SeatId,
                        RequiredVersionOrCreate(seat.ExpectedRowVersion, nameof(seat.ExpectedRowVersion)),
                        seat.Number,
                        seat.Label,
                        seat.X,
                        seat.Y))
                    .ToList()))
                .ToList());
    }

    public static FloorPlanDto ToDto(FloorPlanSnapshot snapshot, IReadOnlySet<string> permissions) => new(
        snapshot.ZoneId,
        snapshot.ZoneCode,
        snapshot.ZoneName,
        snapshot.CanvasWidth,
        snapshot.CanvasHeight,
        snapshot.RowVersion,
        snapshot.Tables.Select(table => new FloorPlanTableDto(
            table.TableId,
            table.TableNumber,
            table.Capacity,
            table.Active,
            table.State.ToString(),
            table.CurrentOrderId,
            table.CurrentBillId,
            table.TableRowVersion,
            table.X,
            table.Y,
            table.Width,
            table.Height,
            table.Shape?.ToString(),
            table.RotationDegrees,
            table.LayoutRowVersion,
            table.ActiveReservationId,
            table.ReservationPartySize,
            table.ReservedAt,
            table.ReservationExpiresAt,
            table.MergeGroupId,
            table.IsMergePrimary,
            table.Seats.Select(seat => new FloorPlanSeatDto(
                seat.SeatId,
                seat.Number,
                seat.Label,
                seat.X,
                seat.Y,
                seat.RowVersion)).ToList(),
            AllowedCommands(new Table(
                table.TableId,
                table.TableNumber,
                snapshot.ZoneId,
                table.Capacity,
                table.Active,
                table.State,
                table.CurrentOrderId,
                table.CurrentBillId,
                table.TableRowVersion),
                permissions)))
            .ToList());

    public static SaveFloorPlanResponse ToDto(FloorPlanSaveResult result, IReadOnlySet<string> permissions) => new(
        ToDto(result.FloorPlan, permissions),
        result.Warnings.Select(warning => new FloorPlanWarningDto(
            warning.Code,
            warning.TableId,
            warning.Message)).ToList());

    public static IReadOnlyList<TableMergeParticipant> ToParticipants(
        IReadOnlyList<TableMergeParticipantRequest>? participants,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(participants, parameterName);
        return participants
            .Select(participant => new TableMergeParticipant(
                participant.TableId,
                RequiredVersion(participant.ExpectedRowVersion, nameof(participant.ExpectedRowVersion))))
            .ToList();
    }

    public static long RequiredVersion(long? value, string parameterName)
    {
        if (value is null or <= 0)
            throw new ArgumentOutOfRangeException(parameterName, "Expected row version must be positive.");
        return value.Value;
    }

    public static long RequiredVersionOrCreate(long? value, string parameterName)
    {
        if (value is null or < 0)
            throw new ArgumentOutOfRangeException(parameterName, "Expected row version cannot be negative or omitted.");
        return value.Value;
    }

    public static void RequireCreateVersion(long? value)
    {
        if (value != 0)
            throw new ArgumentException("Create requests must explicitly use expected row version 0.", nameof(value));
    }

    private static FloorTableShape ParseShape(string value)
    {
        if (!Enum.TryParse<FloorTableShape>(value, ignoreCase: true, out var shape) || !Enum.IsDefined(shape))
            throw new ArgumentException("Shape is not supported.", nameof(value));
        return shape;
    }
}

public sealed class TableManagementUnauthorizedException : Exception
{
    public TableManagementUnauthorizedException(string message) : base(message) { }
}

public sealed class TableManagementForbiddenException : Exception
{
    public TableManagementForbiddenException(string message) : base(message) { }
}

public sealed class TableManagementNotFoundException : Exception
{
    public TableManagementNotFoundException(string message) : base(message) { }
}

public sealed class TableManagementConcurrencyException : Exception
{
    public TableManagementConcurrencyException(string resource, Guid id, long expected, long? actual)
        : base($"Concurrency conflict on {resource} {id}: expected row version {expected}, actual {actual?.ToString(CultureInfo.InvariantCulture) ?? "missing"}.")
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

public sealed class TableManagementConflictException : Exception
{
    public TableManagementConflictException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
