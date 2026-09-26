using FluentAssertions;
using Xunit;
using ALKAROS.OnlineOrdering.Providers.Contracts;

namespace ALKAROS.OnlineOrdering.Yemeksepeti.StatusMapping.Tests;

public sealed class YemeksepetiStatusMapperTests
{
    private const string OrderId = "9f0c2d4e-5b6a-4c3d-8e7f-001122334455";
    private const string Vendor = "VENDOR_DELIVERY";
    private const string Logistics = "LOGISTICS_DELIVERY";

    private static StatusMappingResult Map(string? status, string? transport, YemeksepetiCancellationSignal? cancellation = null) =>
        YemeksepetiStatusMapper.Map(new YemeksepetiStatusSignal(OrderId, status, transport, cancellation));

    /// <summary>The complete documented vocabulary: every (status, delivery kind) pair and its single outcome.</summary>
    public static TheoryData<string, string, StatusMappingKind, InternalOrderCommand?, StatusNoOpReason?> DocumentedVocabulary => new()
    {
        { "RECEIVED", Vendor, StatusMappingKind.Command, InternalOrderCommand.AcceptIncomingOrder, null },
        { "RECEIVED", Logistics, StatusMappingKind.Command, InternalOrderCommand.AcceptIncomingOrder, null },
        { "CANCELLED", Vendor, StatusMappingKind.Command, InternalOrderCommand.CancelOrder, null },
        { "CANCELLED", Logistics, StatusMappingKind.Command, InternalOrderCommand.CancelOrder, null },
        { "READY_FOR_PICKUP", Logistics, StatusMappingKind.NoOp, null, StatusNoOpReason.OwnOutboundStatusEcho },
        { "READY_FOR_PICKUP", Vendor, StatusMappingKind.Unknown, null, null },
        { "DISPATCHED", Vendor, StatusMappingKind.NoOp, null, StatusNoOpReason.OwnOutboundStatusEcho },
        { "DISPATCHED", Logistics, StatusMappingKind.NoOp, null, StatusNoOpReason.HandedToCourier },
        { "DELIVERED", Logistics, StatusMappingKind.NoOp, null, StatusNoOpReason.DeliveredByPlatform },
        { "DELIVERED", Vendor, StatusMappingKind.Unknown, null, null }
    };

    [Theory]
    [MemberData(nameof(DocumentedVocabulary))]
    public void EveryDocumentedStatusHasExactlyOneOutcome(
        string status, string transport, StatusMappingKind kind, InternalOrderCommand? command, StatusNoOpReason? noOp)
    {
        var result = Map(status, transport);

        result.Kind.Should().Be(kind);
        result.Command.Should().Be(command);
        result.NoOpReason.Should().Be(noOp);
        (result.Evidence is not null).Should().Be(kind == StatusMappingKind.Unknown);
        (result.Cancellation is not null).Should().Be(command == InternalOrderCommand.CancelOrder);
    }

    [Theory]
    [InlineData("received", Vendor)]
    [InlineData("PREPARING", Vendor)]
    [InlineData("ACCEPTED", Logistics)]
    [InlineData(null, Logistics)]
    [InlineData("  ", Vendor)]
    [InlineData("RECEIVED", "PICKUP")]
    [InlineData("RECEIVED", null)]
    [InlineData("RECEIVED", "vendor_delivery")]
    public void AnythingOutsideTheVocabularyIsTypedUnknown(string? status, string? transport)
    {
        var result = Map(status, transport);

        result.Kind.Should().Be(StatusMappingKind.Unknown);
        result.Command.Should().BeNull("an unknown status must never drive an order change");
        result.Evidence!.VocabularyVersion.Should().Be(YemeksepetiStatusVocabulary.Version);
        result.Evidence.ExternalOrderId.Should().Be(OrderId);
    }

    [Fact]
    public void UnknownStatusEvidenceIsIdempotentForTheSameStatus()
    {
        var first = Map("PREPARING", Vendor).Evidence!;
        var retry = Map(" PREPARING ", Vendor).Evidence!;
        var otherStatus = Map("ON_HOLD", Vendor).Evidence!;
        var otherOrder = YemeksepetiStatusMapper.Map(new YemeksepetiStatusSignal("another-order", "PREPARING", Vendor)).Evidence!;

        retry.EvidenceId.Should().Be(first.EvidenceId);
        otherStatus.EvidenceId.Should().NotBe(first.EvidenceId);
        otherOrder.EvidenceId.Should().NotBe(first.EvidenceId);
    }

    [Fact]
    public void ConcurrentMappingOfTheSameSignalAlwaysAgrees()
    {
        var results = Enumerable.Range(0, 64)
            .AsParallel()
            .Select(_ => Map("PREPARING", Logistics).Evidence!.EvidenceId)
            .Distinct()
            .ToList();

        results.Should().ContainSingle();
    }

    [Theory]
    [InlineData("CUSTOMER", CancellationParty.Customer)]
    [InlineData("customer", CancellationParty.Customer)]
    [InlineData("VENDOR", CancellationParty.Vendor)]
    [InlineData("LOGISTICS", CancellationParty.Logistics)]
    [InlineData("RIDER_APP", CancellationParty.Unrecognized)]
    [InlineData(null, CancellationParty.Unrecognized)]
    public void CancellationKeepsWhoCancelledAndWhy(string? cancelledBy, CancellationParty party)
    {
        var result = Map("CANCELLED", Logistics, new YemeksepetiCancellationSignal(cancelledBy, "ITEM_UNAVAILABLE", PostPickedUp: true));

        result.Command.Should().Be(InternalOrderCommand.CancelOrder);
        result.Cancellation!.Party.Should().Be(party);
        result.Cancellation.RawParty.Should().Be(cancelledBy);
        result.Cancellation.Reason.Should().Be("ITEM_UNAVAILABLE");
        result.Cancellation.AfterPickup.Should().BeTrue();
    }

    [Fact]
    public void ACancellationWithoutDetailIsStillOneCancelCommand()
    {
        var result = Map("CANCELLED", Vendor);

        result.Command.Should().Be(InternalOrderCommand.CancelOrder);
        result.Cancellation.Should().Be(new CancellationDetail(CancellationParty.Unrecognized, null, null, false));
    }

    [Fact]
    public void HostileOrOversizedProviderValuesAreBoundedAndStripped()
    {
        var huge = new string('A', 10_000);
        var result = Map(huge + "\u0000\u001b[31m", Vendor, null);
        var cancel = Map("CANCELLED", Vendor, new YemeksepetiCancellationSignal("CUSTOMER\u0000", new string('r', 5_000), false));

        result.Kind.Should().Be(StatusMappingKind.Unknown);
        result.Evidence!.RawStatus!.Length.Should().Be(64);
        result.Evidence.RawStatus.Should().NotContain("\u0000");
        cancel.Cancellation!.Party.Should().Be(CancellationParty.Customer);
        cancel.Cancellation.Reason!.Length.Should().Be(200);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("order\nid")]
    public void AStatusWithoutAUsableOrderIdentityIsRejected(string externalOrderId)
    {
        var act = () => YemeksepetiStatusMapper.Map(new YemeksepetiStatusSignal(externalOrderId, "RECEIVED", Vendor));

        act.Should().Throw<InvalidStatusSignalException>();
    }

    [Fact]
    public void AnOverlongOrderIdentityIsRejected()
    {
        var act = () => YemeksepetiStatusMapper.Map(new YemeksepetiStatusSignal(new string('9', 65), "RECEIVED", Vendor));

        act.Should().Throw<InvalidStatusSignalException>();
    }
}
