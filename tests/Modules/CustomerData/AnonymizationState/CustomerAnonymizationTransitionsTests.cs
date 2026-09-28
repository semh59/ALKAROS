using Xunit;

namespace ALKAROS.CustomerData.AnonymizationState.Tests;

public sealed class CustomerAnonymizationTransitionsTests
{
    [Theory]
    [InlineData(AnonymizationRequestStatus.Requested, AnonymizationRequestStatus.RetentionBlocked, true)]
    [InlineData(AnonymizationRequestStatus.Requested, AnonymizationRequestStatus.Pending, true)]
    [InlineData(AnonymizationRequestStatus.RetentionBlocked, AnonymizationRequestStatus.Pending, true)]
    [InlineData(AnonymizationRequestStatus.Pending, AnonymizationRequestStatus.Anonymized, true)]
    public void AllowedEdgesAreAllowed(AnonymizationRequestStatus from, AnonymizationRequestStatus to, bool expected)
    {
        Assert.Equal(expected, CustomerAnonymizationTransitions.CanTransition(from, to));
    }

    [Theory]
    [InlineData(AnonymizationRequestStatus.Requested, AnonymizationRequestStatus.Anonymized)] // no skipping Pending
    [InlineData(AnonymizationRequestStatus.RetentionBlocked, AnonymizationRequestStatus.Anonymized)] // no skipping Pending
    [InlineData(AnonymizationRequestStatus.RetentionBlocked, AnonymizationRequestStatus.RetentionBlocked)] // no-op transition
    [InlineData(AnonymizationRequestStatus.Pending, AnonymizationRequestStatus.RetentionBlocked)] // no going backward
    [InlineData(AnonymizationRequestStatus.Anonymized, AnonymizationRequestStatus.Pending)] // terminal reopen
    [InlineData(AnonymizationRequestStatus.Anonymized, AnonymizationRequestStatus.RetentionBlocked)] // terminal reopen
    public void ForbiddenEdgesAreRejected(AnonymizationRequestStatus from, AnonymizationRequestStatus to)
    {
        Assert.False(CustomerAnonymizationTransitions.CanTransition(from, to));
    }

    [Fact]
    public void AnonymizedIsTerminalWithNoOutgoingEdgesAtAll()
    {
        foreach (var target in Enum.GetValues<AnonymizationRequestStatus>())
            Assert.False(CustomerAnonymizationTransitions.CanTransition(AnonymizationRequestStatus.Anonymized, target));
    }
}
