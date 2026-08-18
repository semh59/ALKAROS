using FluentAssertions;
using Xunit;

namespace ALKAROS.Tables.Reservations.Tests;

public sealed class TableReservationDomainTests
{
    [Fact]
    public void TableReservationRecordValidArgumentsConstructsCorrectly()
    {
        var id = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var reservedAt = DateTimeOffset.UtcNow;
        var expiresAt = reservedAt.AddHours(2);
        const string reason = "VIP guest reservation.";

        var record = new TableReservationRecord(
            id,
            tableId,
            orderId,
            actorId,
            TableReservationActorType.User,
            TableReservationStatus.Active,
            reason,
            partySize: 4,
            reservedAt,
            expiresAt);

        record.Id.Should().Be(id);
        record.TableId.Should().Be(tableId);
        record.OrderId.Should().Be(orderId);
        record.ActorId.Should().Be(actorId);
        record.ActorType.Should().Be(TableReservationActorType.User);
        record.Status.Should().Be(TableReservationStatus.Active);
        record.IsActive.Should().BeTrue();
        record.Reason.Should().Be(reason);
        record.PartySize.Should().Be(4);
        record.ReservedAt.Should().Be(reservedAt);
        record.ExpiresAt.Should().Be(expiresAt);
        record.ReleasedAt.Should().BeNull();
        record.ReleasedBy.Should().BeNull();
        record.ReleaseReason.Should().BeNull();
        record.RowVersion.Should().Be(1);
    }

    [Fact]
    public void TableReservationRecordEmptyIdThrowsArgumentException()
    {
        var act = () => new TableReservationRecord(
            Guid.Empty,
            Guid.NewGuid(),
            null,
            null,
            TableReservationActorType.User,
            TableReservationStatus.Active,
            "Reason",
            partySize: 2,
            DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("id");
    }

    [Fact]
    public void TableReservationRecordEmptyTableIdThrowsArgumentException()
    {
        var act = () => new TableReservationRecord(
            Guid.NewGuid(),
            Guid.Empty,
            null,
            null,
            TableReservationActorType.User,
            TableReservationStatus.Active,
            "Reason",
            partySize: 2,
            DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("tableId");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TableReservationRecordEmptyReasonThrowsArgumentException(string invalidReason)
    {
        var act = () => new TableReservationRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            TableReservationActorType.User,
            TableReservationStatus.Active,
            invalidReason,
            partySize: 2,
            DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("reason");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TableReservationRecordInvalidPartySizeThrowsArgumentOutOfRangeException(int invalidPartySize)
    {
        var act = () => new TableReservationRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            TableReservationActorType.User,
            TableReservationStatus.Active,
            "Reason",
            invalidPartySize,
            DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("partySize");
    }

    [Fact]
    public void CreateReservationRequestEmptyTableIdThrowsArgumentException()
    {
        var request = new CreateReservationRequest(
            Guid.Empty,
            1,
            null,
            null,
            TableReservationActorType.User,
            "Reason");

        var act = () => request.Validate();

        act.Should().Throw<ArgumentException>()
            .WithParameterName("TableId");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateReservationRequestInvalidTableRowVersionThrowsArgumentOutOfRangeException(long invalidVersion)
    {
        var request = new CreateReservationRequest(
            Guid.NewGuid(),
            invalidVersion,
            null,
            null,
            TableReservationActorType.User,
            "Reason");

        var act = () => request.Validate();

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("ExpectedTableRowVersion");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateReservationRequestEmptyReasonThrowsArgumentException(string invalidReason)
    {
        var request = new CreateReservationRequest(
            Guid.NewGuid(),
            1,
            null,
            null,
            TableReservationActorType.User,
            invalidReason);

        var act = () => request.Validate();

        act.Should().Throw<ArgumentException>()
            .WithParameterName("Reason");
    }

    [Fact]
    public void CreateReservationRequestExpiresBeforeReservedThrowsArgumentException()
    {
        var now = DateTimeOffset.UtcNow;
        var request = new CreateReservationRequest(
            Guid.NewGuid(),
            1,
            null,
            null,
            TableReservationActorType.User,
            "Reason",
            PartySize: 2,
            ReservedAt: now,
            ExpiresAt: now.AddMinutes(-5));

        var act = () => request.Validate();

        act.Should().Throw<ArgumentException>()
            .WithParameterName("ExpiresAt");
    }

    [Fact]
    public void ClaimReservationRequestEmptyIdThrowsArgumentException()
    {
        var request = new ClaimReservationRequest(
            Guid.Empty,
            1,
            1,
            null,
            null);

        var act = () => request.Validate();

        act.Should().Throw<ArgumentException>()
            .WithParameterName("ReservationId");
    }

    [Fact]
    public void CancelReservationRequestEmptyReasonThrowsArgumentException()
    {
        var request = new CancelReservationRequest(
            Guid.NewGuid(),
            1,
            1,
            null,
            "   ");

        var act = () => request.Validate();

        act.Should().Throw<ArgumentException>()
            .WithParameterName("Reason");
    }

    [Fact]
    public void ExceptionsCapturePropertiesCorrectly()
    {
        var tableId = Guid.NewGuid();
        var notAvailEx = new TableNotAvailableForReservationException(tableId, "Occupied", "Table busy");
        notAvailEx.TableId.Should().Be(tableId);
        notAvailEx.ActualState.Should().Be("Occupied");
        notAvailEx.Reason.Should().Be("Table busy");

        var resId = Guid.NewGuid();
        var notFoundEx = new ReservationNotFoundException(resId);
        notFoundEx.ReservationId.Should().Be(resId);

        var stateEx = new InvalidReservationStateException(resId, TableReservationStatus.Cancelled, "Claim");
        stateEx.ReservationId.Should().Be(resId);
        stateEx.ActualStatus.Should().Be(TableReservationStatus.Cancelled);
        stateEx.AttemptedAction.Should().Be("Claim");

        var concEx = new TableReservationConcurrencyException(tableId, "Table", 1, 2);
        concEx.EntityId.Should().Be(tableId);
        concEx.EntityType.Should().Be("Table");
        concEx.ExpectedVersion.Should().Be(1);
        concEx.ActualVersion.Should().Be(2);
    }
}
