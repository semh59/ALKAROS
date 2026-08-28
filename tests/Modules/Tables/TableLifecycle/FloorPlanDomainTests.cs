using ALKAROS.Tables.FloorPlan;
using Xunit;

namespace ALKAROS.Tables.TableLifecycle.Tests;

public sealed class FloorPlanDomainTests
{
    [Fact]
    public void ValidLayoutReportsCapacitySeatMismatchWithoutChangingTheRequest()
    {
        var tableId = Guid.NewGuid();
        var request = Plan(
            Table(tableId, 40, 40, seats: [Seat(1), Seat(2)]));

        var warnings = FloorPlanValidator.Validate(
            request,
            new Dictionary<Guid, int> { [tableId] = 4 },
            (_, _) => false);

        var warning = Assert.Single(warnings);
        Assert.Equal("CAPACITY_SEAT_MISMATCH", warning.Code);
        Assert.Equal(tableId, warning.TableId);
        Assert.Equal(2, request.Tables[0].Seats.Count);
    }

    [Fact]
    public void OverlapIsRejectedUnlessBothTablesBelongToAnActiveMerge()
    {
        var first = Table(Guid.NewGuid(), 40, 40);
        var second = Table(Guid.NewGuid(), 80, 80);
        var request = Plan(first, second);
        var capacities = request.Tables.ToDictionary(table => table.TableId, _ => 0);

        Assert.Throws<FloorPlanValidationException>(() =>
            FloorPlanValidator.Validate(request, capacities, (_, _) => false));

        var warnings = FloorPlanValidator.Validate(
            request,
            capacities,
            (left, right) =>
                (left == first.TableId && right == second.TableId)
                || (left == second.TableId && right == first.TableId));
        Assert.Empty(warnings);
    }

    [Fact]
    public void BoundsShapeRotationAndSeatIdentityAreFailClosed()
    {
        var tableId = Guid.NewGuid();
        var duplicateSeat = Guid.NewGuid();
        var invalid = new SaveFloorTable(
            tableId,
            1,
            0,
            1_200,
            40,
            120,
            80,
            FloorTableShape.Rectangle,
            45,
            [
                new SaveFloorSeat(duplicateSeat, 0, 1, "Seat 1", 20, 20),
                new SaveFloorSeat(duplicateSeat, 0, 2, "Seat 2", 30, 20),
            ]);

        Assert.Throws<FloorPlanValidationException>(() =>
            FloorPlanValidator.Validate(
                Plan(invalid),
                new Dictionary<Guid, int> { [tableId] = 2 },
                (_, _) => false));
    }

    [Fact]
    public void SquareShapeRequiresEqualDimensions()
    {
        var tableId = Guid.NewGuid();
        var request = Plan(new SaveFloorTable(
            tableId,
            1,
            0,
            40,
            40,
            120,
            80,
            FloorTableShape.Square,
            0,
            []));

        Assert.Throws<FloorPlanValidationException>(() =>
            FloorPlanValidator.Validate(
                request,
                new Dictionary<Guid, int> { [tableId] = 0 },
                (_, _) => false));
    }

    private static SaveFloorPlan Plan(params SaveFloorTable[] tables)
        => new(Guid.NewGuid(), 0, 1_280, 800, tables);

    private static SaveFloorTable Table(
        Guid tableId,
        int x,
        int y,
        IReadOnlyList<SaveFloorSeat>? seats = null)
        => new(tableId, 1, 0, x, y, 120, 80, FloorTableShape.Rectangle, 0, seats ?? []);

    private static SaveFloorSeat Seat(int number)
        => new(Guid.NewGuid(), 0, number, $"Seat {number}", 40 + number * 20, 30);
}
