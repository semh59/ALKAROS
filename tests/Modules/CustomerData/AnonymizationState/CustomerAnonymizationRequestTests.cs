using Xunit;

namespace ALKAROS.CustomerData.AnonymizationState.Tests;

public sealed class CustomerAnonymizationRequestTests
{
    private static readonly Guid Id = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly DateTimeOffset RequestedAt = DateTimeOffset.UtcNow;

    [Fact]
    public void IdMustNotBeEmpty()
    {
        Assert.Throws<ArgumentException>(() => new CustomerAnonymizationRequest(
            Guid.Empty, CustomerId, AnonymizationRequestStatus.Pending, RequestedAt, null, null, null, 1));
    }

    [Fact]
    public void CustomerIdMustNotBeEmpty()
    {
        Assert.Throws<ArgumentException>(() => new CustomerAnonymizationRequest(
            Id, Guid.Empty, AnonymizationRequestStatus.Pending, RequestedAt, null, null, null, 1));
    }

    [Fact]
    public void RowVersionMustBePositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CustomerAnonymizationRequest(
            Id, CustomerId, AnonymizationRequestStatus.Pending, RequestedAt, null, null, null, 0));
    }

    [Fact]
    public void RetentionBlockedWithoutAReasonIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new CustomerAnonymizationRequest(
            Id, CustomerId, AnonymizationRequestStatus.RetentionBlocked, RequestedAt, null, blockedReason: null, null, 1));
    }

    [Fact]
    public void PendingWithAReasonIsRejected()
    {
        // A reason surviving past the blocked state would misleadingly imply the request is still blocked.
        Assert.Throws<ArgumentException>(() => new CustomerAnonymizationRequest(
            Id, CustomerId, AnonymizationRequestStatus.Pending, RequestedAt, null, blockedReason: "stale reason", null, 1));
    }

    [Fact]
    public void AnonymizedWithoutAnAnonymizedAtIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new CustomerAnonymizationRequest(
            Id, CustomerId, AnonymizationRequestStatus.Anonymized, RequestedAt, null, null, anonymizedAt: null, 1));
    }

    [Fact]
    public void PendingWithAnAnonymizedAtIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new CustomerAnonymizationRequest(
            Id, CustomerId, AnonymizationRequestStatus.Pending, RequestedAt, null, null, anonymizedAt: DateTimeOffset.UtcNow, 1));
    }

    [Fact]
    public void AValidRetentionBlockedRequestConstructsCleanly()
    {
        var request = new CustomerAnonymizationRequest(
            Id, CustomerId, AnonymizationRequestStatus.RetentionBlocked, RequestedAt, "system", "open invoice", null, 1);

        Assert.Equal(AnonymizationRequestStatus.RetentionBlocked, request.Status);
        Assert.Equal("open invoice", request.BlockedReason);
    }

    [Fact]
    public void AValidAnonymizedRequestConstructsCleanly()
    {
        var anonymizedAt = DateTimeOffset.UtcNow;
        var request = new CustomerAnonymizationRequest(
            Id, CustomerId, AnonymizationRequestStatus.Anonymized, RequestedAt, null, null, anonymizedAt, 2);

        Assert.Equal(anonymizedAt, request.AnonymizedAt);
        Assert.Null(request.BlockedReason);
    }
}
