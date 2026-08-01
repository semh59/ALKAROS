using ALKAROS.Messaging;
using Xunit;

namespace ALKAROS.Idempotency.Tests;

public sealed class RetryPolicyTests
{
    [Fact]
    public void MaxAttemptsIsThree()
    {
        Assert.Equal(3, RetryPolicy.MaxAttempts);
    }

    [Theory]
    [InlineData(1, 1.0)]
    [InlineData(2, 2.0)]
    public void NextRetryDelayAppliesExponentialBackoff(int completedAttempts, double expectedSeconds)
    {
        var delay = RetryPolicy.NextRetryDelay(completedAttempts, TimeSpan.FromSeconds(1));
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }

    [Fact]
    public void NextRetryDelayZeroCompletedAttemptsThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RetryPolicy.NextRetryDelay(0, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void NextRetryDelayAtMaxAttemptsThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RetryPolicy.NextRetryDelay(RetryPolicy.MaxAttempts, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void NextRetryDelayNonPositiveBaseDelayThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RetryPolicy.NextRetryDelay(1, TimeSpan.Zero));
    }
}
