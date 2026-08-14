namespace ALKAROS.Tables.TableLifecycle.Tests;

using ALKAROS.Tables.TableLifecycle;
using FluentAssertions;
using Xunit;

public class TableDomainTests
{
    private static Table NewTable(TableState state = TableState.Available, Guid? zoneId = null)
        => new(Guid.NewGuid(), "5", zoneId, state: state);

    [Theory]
    [InlineData(TableState.Occupied, true)]
    [InlineData(TableState.Reserved, true)]
    [InlineData(TableState.Cleaning, true)]
    [InlineData(TableState.OutOfService, true)]
    [InlineData(TableState.Available, false)]
    public void AvailableCanTransitionTo(TableState target, bool allowed)
        => NewTable(TableState.Available).CanTransitionTo(target).Should().Be(allowed);

    [Theory]
    [InlineData(TableState.Reserved, true)]
    [InlineData(TableState.Available, true)]
    [InlineData(TableState.Cleaning, false)]
    [InlineData(TableState.OutOfService, false)]
    [InlineData(TableState.Occupied, false)]
    public void OccupiedCanTransitionTo(TableState target, bool allowed)
        => NewTable(TableState.Occupied).CanTransitionTo(target).Should().Be(allowed);

    [Theory]
    [InlineData(TableState.Available, true)]
    [InlineData(TableState.Occupied, false)]
    [InlineData(TableState.Reserved, false)]
    [InlineData(TableState.Cleaning, false)]
    [InlineData(TableState.OutOfService, false)]
    public void ReservedCanTransitionTo(TableState target, bool allowed)
        => NewTable(TableState.Reserved).CanTransitionTo(target).Should().Be(allowed);

    [Theory]
    [InlineData(TableState.Available, true)]
    [InlineData(TableState.Occupied, false)]
    [InlineData(TableState.Reserved, false)]
    [InlineData(TableState.Cleaning, false)]
    [InlineData(TableState.OutOfService, false)]
    public void CleaningCanTransitionTo(TableState target, bool allowed)
        => NewTable(TableState.Cleaning).CanTransitionTo(target).Should().Be(allowed);

    [Theory]
    [InlineData(TableState.Available, true)]
    [InlineData(TableState.Cleaning, true)]
    [InlineData(TableState.Occupied, false)]
    [InlineData(TableState.Reserved, false)]
    [InlineData(TableState.OutOfService, false)]
    public void OutOfServiceCanTransitionTo(TableState target, bool allowed)
        => NewTable(TableState.OutOfService).CanTransitionTo(target).Should().Be(allowed);

    [Fact]
    public void TransitionToAllowedUpdatesState()
    {
        var table = NewTable(TableState.Available);

        var occupied = table.TransitionTo(TableState.Occupied);

        occupied.State.Should().Be(TableState.Occupied);
        occupied.Id.Should().Be(table.Id);
        occupied.RowVersion.Should().Be(table.RowVersion);
    }

    [Fact]
    public void TransitionToDisallowedThrowsInvalidOperationException()
    {
        var table = NewTable(TableState.Available);

        var act = () => table.TransitionTo(TableState.Available);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Table {table.Id} cannot transition from Available to Available.");
    }

    [Fact]
    public void TransitionToCleaningFromReservedIsRejected()
    {
        var table = NewTable(TableState.Reserved);

        var act = () => table.TransitionTo(TableState.Cleaning);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void EmptyTableNumberIsRejected()
    {
        var act = () => new Table(Guid.NewGuid(), "   ");

        act.Should().Throw<ArgumentException>()
            .WithParameterName("tableNumber");
    }

    [Fact]
    public void NegativeCapacityIsRejected()
    {
        var act = () => new Table(Guid.NewGuid(), "7", capacity: -1);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("capacity");
    }

    [Fact]
    public void TableDefaultsMatchPdfSchema()
    {
        var table = new Table(Guid.NewGuid(), "12", Guid.NewGuid());

        table.Capacity.Should().Be(0);
        table.Active.Should().BeTrue();
        table.State.Should().Be(TableState.Available);
        table.CurrentOrderId.Should().BeNull();
        table.CurrentBillId.Should().BeNull();
        table.RowVersion.Should().Be(1);
    }
}

public class ZoneDomainTests
{
    [Fact]
    public void ConstructorSetsAllFields()
    {
        var id = Guid.NewGuid();
        var zone = new Zone(id, "TERRACE", "Terrace", 3, active: false);

        zone.Id.Should().Be(id);
        zone.Code.Should().Be("TERRACE");
        zone.Name.Should().Be("Terrace");
        zone.SortOrder.Should().Be(3);
        zone.Active.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void EmptyCodeThrowsArgumentException(string? code)
    {
        var act = () => new Zone(Guid.NewGuid(), code!, "Name");

        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(code));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void EmptyNameThrowsArgumentException(string? name)
    {
        var act = () => new Zone(Guid.NewGuid(), "CODE", name!);

        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(name));
    }
}