using FluentAssertions;
using Xunit;

namespace ALKAROS.Tables.TableMerge.Tests;

public sealed class TableMergeDomainTests
{
    [Fact]
    public void TableMergeRecordValidArgumentsConstructsCorrectly()
    {
        var id = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var primaryId = Guid.NewGuid();
        var mergedId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var billId = Guid.NewGuid();
        var mergedBy = Guid.NewGuid();
        var mergedAt = DateTimeOffset.UtcNow;
        const string reason = "Large group joined tables.";

        var record = new TableMergeRecord(
            id,
            groupId,
            primaryId,
            mergedId,
            orderId,
            billId,
            TableMergeStatus.Active,
            reason,
            mergedBy,
            mergedAt);

        record.Id.Should().Be(id);
        record.MergeGroupId.Should().Be(groupId);
        record.PrimaryTableId.Should().Be(primaryId);
        record.MergedTableId.Should().Be(mergedId);
        record.OriginalOrderId.Should().Be(orderId);
        record.OriginalBillId.Should().Be(billId);
        record.Status.Should().Be(TableMergeStatus.Active);
        record.IsActive.Should().BeTrue();
        record.Reason.Should().Be(reason);
        record.MergedBy.Should().Be(mergedBy);
        record.MergedAt.Should().Be(mergedAt);
        record.UnmergedAt.Should().BeNull();
        record.UnmergedBy.Should().BeNull();
        record.UnmergeReason.Should().BeNull();
        record.RowVersion.Should().Be(1);
    }

    [Fact]
    public void TableMergeRecordEmptyIdThrowsArgumentException()
    {
        var act = () => new TableMergeRecord(
            Guid.Empty,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            TableMergeStatus.Active,
            "Reason",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("id");
    }

    [Fact]
    public void TableMergeRecordSamePrimaryAndMergedThrowsArgumentException()
    {
        var sameId = Guid.NewGuid();
        var act = () => new TableMergeRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            sameId,
            sameId,
            null,
            null,
            TableMergeStatus.Active,
            "Reason",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("mergedTableId");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TableMergeRecordEmptyReasonThrowsArgumentException(string invalidReason)
    {
        var act = () => new TableMergeRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            TableMergeStatus.Active,
            invalidReason,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("reason");
    }

    [Fact]
    public void TableMergeRequestEmptyPrimaryIdThrowsArgumentException()
    {
        var request = new TableMergeRequest(
            Guid.Empty,
            1,
            new[] { new TableMergeParticipant(Guid.NewGuid(), 1) },
            "Reason",
            Guid.NewGuid());

        var act = () => request.Validate();

        act.Should().Throw<ArgumentException>()
            .WithParameterName("PrimaryTableId");
    }

    [Fact]
    public void TableMergeRequestEmptyParticipantsThrowsArgumentException()
    {
        var request = new TableMergeRequest(
            Guid.NewGuid(),
            1,
            Array.Empty<TableMergeParticipant>(),
            "Reason",
            Guid.NewGuid());

        var act = () => request.Validate();

        act.Should().Throw<ArgumentException>()
            .WithParameterName("Participants");
    }

    [Fact]
    public void TableMergeRequestSameTableInParticipantsThrowsSameTableMergeException()
    {
        var primaryId = Guid.NewGuid();
        var request = new TableMergeRequest(
            primaryId,
            1,
            new[] { new TableMergeParticipant(primaryId, 1) },
            "Reason",
            Guid.NewGuid());

        var act = () => request.Validate();

        act.Should().Throw<SameTableMergeException>()
            .Which.TableId.Should().Be(primaryId);
    }

    [Fact]
    public void TableMergeRequestDuplicateParticipantThrowsDuplicateMergeParticipantException()
    {
        var primaryId = Guid.NewGuid();
        var partId = Guid.NewGuid();
        var request = new TableMergeRequest(
            primaryId,
            1,
            new[]
            {
                new TableMergeParticipant(partId, 1),
                new TableMergeParticipant(partId, 1)
            },
            "Reason",
            Guid.NewGuid());

        var act = () => request.Validate();

        act.Should().Throw<DuplicateMergeParticipantException>()
            .Which.TableId.Should().Be(partId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TableMergeRequestInvalidPrimaryRowVersionThrowsArgumentOutOfRangeException(long invalidVersion)
    {
        var request = new TableMergeRequest(
            Guid.NewGuid(),
            invalidVersion,
            new[] { new TableMergeParticipant(Guid.NewGuid(), 1) },
            "Reason",
            Guid.NewGuid());

        var act = () => request.Validate();

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("ExpectedPrimaryRowVersion");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TableMergeRequestEmptyReasonThrowsArgumentException(string invalidReason)
    {
        var request = new TableMergeRequest(
            Guid.NewGuid(),
            1,
            new[] { new TableMergeParticipant(Guid.NewGuid(), 1) },
            invalidReason,
            Guid.NewGuid());

        var act = () => request.Validate();

        act.Should().Throw<ArgumentException>()
            .WithParameterName("Reason");
    }

    [Fact]
    public void TableMergeRequestEmptyMergedByThrowsArgumentException()
    {
        var request = new TableMergeRequest(
            Guid.NewGuid(),
            1,
            new[] { new TableMergeParticipant(Guid.NewGuid(), 1) },
            "Reason",
            Guid.Empty);

        var act = () => request.Validate();

        act.Should().Throw<ArgumentException>()
            .WithParameterName("MergedBy");
    }

    [Fact]
    public void TableUnmergeRequestEmptyGroupIdThrowsArgumentException()
    {
        var request = new TableUnmergeRequest(
            Guid.Empty,
            1,
            new[] { new TableMergeParticipant(Guid.NewGuid(), 1) },
            "Reason",
            Guid.NewGuid());

        var act = () => request.Validate();

        act.Should().Throw<ArgumentException>()
            .WithParameterName("MergeGroupId");
    }

    [Fact]
    public void TableUnmergeRequestEmptyParticipantVersionsThrowsArgumentException()
    {
        var request = new TableUnmergeRequest(
            Guid.NewGuid(),
            1,
            Array.Empty<TableMergeParticipant>(),
            "Reason",
            Guid.NewGuid());

        var act = () => request.Validate();

        act.Should().Throw<ArgumentException>()
            .WithParameterName("ExpectedParticipantVersions");
    }

    [Fact]
    public void ExceptionsCaptureDomainPropertiesCorrectly()
    {
        var tableId = Guid.NewGuid();
        var sameEx = new SameTableMergeException(tableId);
        sameEx.TableId.Should().Be(tableId);

        var dupEx = new DuplicateMergeParticipantException(tableId);
        dupEx.TableId.Should().Be(tableId);

        var notFoundEx = new TableNotFoundException(tableId, "Not found");
        notFoundEx.TableId.Should().Be(tableId);

        var stateEx = new InvalidTableMergeStateException(tableId, "Cleaning", "Table is cleaning");
        stateEx.TableId.Should().Be(tableId);
        stateEx.ActualState.Should().Be("Cleaning");

        var billId = Guid.NewGuid();
        var payEx = new PaymentPolicyRequiredException(billId, "Partially paid");
        payEx.BillId.Should().Be(billId);

        var concEx = new TableMergeConcurrencyException(tableId, 1, 2);
        concEx.TableId.Should().Be(tableId);
        concEx.ExpectedVersion.Should().Be(1);
        concEx.ActualVersion.Should().Be(2);
    }
}
