namespace ALKAROS.Tables.FloorPlan;

public static class FloorPlanValidator
{
    public const int MinimumCanvasSize = 320;
    public const int MaximumCanvasSize = 4096;
    public const int MinimumTableSize = 48;
    public const int MaximumTableSize = 640;
    public const int MaximumTables = 250;
    public const int MaximumSeatsPerTable = 64;

    private static readonly HashSet<int> SupportedRotations = [0, 90, 180, 270];

    public static IReadOnlyList<FloorPlanWarning> Validate(
        SaveFloorPlan request,
        IReadOnlyDictionary<Guid, int> tableCapacities,
        Func<Guid, Guid, bool> mayOverlap)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(tableCapacities);
        ArgumentNullException.ThrowIfNull(mayOverlap);

        if (request.ZoneId == Guid.Empty)
            throw new FloorPlanValidationException("Zone ID cannot be empty.");
        if (request.ExpectedRowVersion < 0)
            throw new FloorPlanValidationException("Expected floor-plan row version cannot be negative.");
        if (request.CanvasWidth is < MinimumCanvasSize or > MaximumCanvasSize
            || request.CanvasHeight is < MinimumCanvasSize or > MaximumCanvasSize)
        {
            throw new FloorPlanValidationException(
                $"Canvas dimensions must be between {MinimumCanvasSize} and {MaximumCanvasSize} layout units.");
        }

        var tables = request.Tables
            ?? throw new FloorPlanValidationException("Floor-plan tables are required.");
        if (tables.Count > MaximumTables)
            throw new FloorPlanValidationException($"A floor plan supports at most {MaximumTables} tables.");
        if (tables.Select(table => table.TableId).Distinct().Count() != tables.Count)
            throw new FloorPlanValidationException("A table can appear only once in a floor plan.");
        var seats = tables.SelectMany(table => table.Seats ?? []).ToList();
        if (seats.Select(seat => seat.SeatId).Distinct().Count() != seats.Count)
            throw new FloorPlanValidationException("A seat ID can appear only once in a floor plan.");

        var requestedIds = tables.Select(table => table.TableId).ToHashSet();
        if (!requestedIds.SetEquals(tableCapacities.Keys))
            throw new FloorPlanValidationException("The floor plan must contain every table currently assigned to the zone.");

        var warnings = new List<FloorPlanWarning>();
        foreach (var table in tables)
        {
            ValidateTable(request, table);
            var capacity = tableCapacities[table.TableId];
            if (capacity != table.Seats.Count)
            {
                warnings.Add(new FloorPlanWarning(
                    "CAPACITY_SEAT_MISMATCH",
                    table.TableId,
                    $"Table capacity is {capacity}, but {table.Seats.Count} active seats are configured."));
            }
        }

        // The request is bounded to 250 tables, so an O(n^2) rectangle check is deterministic and inexpensive.
        for (var leftIndex = 0; leftIndex < tables.Count; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < tables.Count; rightIndex++)
            {
                var left = tables[leftIndex];
                var right = tables[rightIndex];
                if (Overlaps(left, right) && !mayOverlap(left.TableId, right.TableId))
                {
                    throw new FloorPlanValidationException(
                        $"Tables {left.TableId} and {right.TableId} overlap without an active merge.");
                }
            }
        }

        return warnings;
    }

    private static void ValidateTable(SaveFloorPlan plan, SaveFloorTable table)
    {
        if (table.TableId == Guid.Empty)
            throw new FloorPlanValidationException("Table ID cannot be empty.");
        if (table.ExpectedTableRowVersion <= 0)
            throw new FloorPlanValidationException("Expected table row version must be positive.");
        if (table.ExpectedLayoutRowVersion < 0)
            throw new FloorPlanValidationException("Expected table-layout row version cannot be negative.");
        if (table.Width is < MinimumTableSize or > MaximumTableSize
            || table.Height is < MinimumTableSize or > MaximumTableSize)
        {
            throw new FloorPlanValidationException(
                $"Table dimensions must be between {MinimumTableSize} and {MaximumTableSize} layout units.");
        }
        if (table.X < 0 || table.Y < 0
            || table.X + table.Width > plan.CanvasWidth
            || table.Y + table.Height > plan.CanvasHeight)
        {
            throw new FloorPlanValidationException($"Table {table.TableId} is outside the zone canvas.");
        }
        if (!Enum.IsDefined(table.Shape))
            throw new FloorPlanValidationException($"Table {table.TableId} has an unsupported shape.");
        if (!SupportedRotations.Contains(table.RotationDegrees))
            throw new FloorPlanValidationException($"Table {table.TableId} has an unsupported rotation.");
        if (table.Shape == FloorTableShape.Square && table.Width != table.Height)
            throw new FloorPlanValidationException($"Square table {table.TableId} must have equal width and height.");

        var seats = table.Seats
            ?? throw new FloorPlanValidationException($"Seats are required for table {table.TableId}.");
        if (seats.Count > MaximumSeatsPerTable)
            throw new FloorPlanValidationException(
                $"Table {table.TableId} supports at most {MaximumSeatsPerTable} seats.");
        if (seats.Select(seat => seat.SeatId).Distinct().Count() != seats.Count)
            throw new FloorPlanValidationException($"Seat IDs must be unique for table {table.TableId}.");
        if (seats.Select(seat => seat.Number).Distinct().Count() != seats.Count)
            throw new FloorPlanValidationException($"Seat numbers must be unique for table {table.TableId}.");

        foreach (var seat in seats)
        {
            if (seat.SeatId == Guid.Empty)
                throw new FloorPlanValidationException("Seat ID cannot be empty.");
            if (seat.ExpectedRowVersion < 0)
                throw new FloorPlanValidationException("Expected seat row version cannot be negative.");
            if (seat.Number <= 0)
                throw new FloorPlanValidationException("Seat number must be positive.");
            if (string.IsNullOrWhiteSpace(seat.Label) || seat.Label.Trim().Length > 50)
                throw new FloorPlanValidationException("Seat label must contain between 1 and 50 characters.");
            if (seat.X < 0 || seat.Y < 0 || seat.X > plan.CanvasWidth || seat.Y > plan.CanvasHeight)
                throw new FloorPlanValidationException($"Seat {seat.SeatId} is outside the zone canvas.");
        }
    }

    private static bool Overlaps(SaveFloorTable left, SaveFloorTable right)
        => left.X < right.X + right.Width
           && left.X + left.Width > right.X
           && left.Y < right.Y + right.Height
           && left.Y + left.Height > right.Y;
}
