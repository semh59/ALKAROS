using ALKAROS.Transactions.Tests.Fixtures;
using Xunit;

namespace ALKAROS.Transactions.Tests.Retry;

/// <summary>
/// Tests for retry classification: only explicitly transient failures are
/// retried, unknown failures are never retried, and every attempt runs on a
/// fresh transaction scope.
/// </summary>
public static class TransactionRetryTests
{
    [Fact]
    public static async Task UnknownFailureIsNeverRetried()
    {
        var attempts = 0;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TransactionContext.RunAsync(
                async context =>
                {
                    attempts++;
                    await Task.Yield();
                    throw new InvalidOperationException("unknown failure");
                },
                retryPolicy: new TransactionRetryPolicy(maxAttempts: 5)));

        Assert.Equal(1, attempts);
        Assert.Contains("unknown failure", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public static async Task TransientFailureIsRetriedUntilSuccess()
    {
        var attempts = 0;
        var resource = new RecordingResource("resource");

        await TransactionContext.RunAsync(
            async context =>
            {
                attempts++;
                context.Enlist(resource);
                if (attempts < 3)
                    throw new SimulatedTransientException($"attempt {attempts} failed");
                await Task.Yield();
            },
            retryPolicy: new TransactionRetryPolicy(maxAttempts: 3));

        Assert.Equal(3, attempts);
        Assert.True(resource.RolledBack);
        Assert.True(resource.CommitSucceeded);
    }

    [Fact]
    public static async Task TransientFailureExhaustsAttemptsAndSurfacesTheFailure()
    {
        var attempts = 0;

        var ex = await Assert.ThrowsAsync<SimulatedTransientException>(() =>
            TransactionContext.RunAsync(
                async context =>
                {
                    attempts++;
                    await Task.Yield();
                    throw new SimulatedTransientException($"attempt {attempts} failed");
                },
                retryPolicy: new TransactionRetryPolicy(maxAttempts: 3)));

        Assert.Equal(3, attempts);
        Assert.Contains("attempt 3 failed", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public static async Task TransientExceptionWrappedInInnerExceptionIsClassifiedTransient()
    {
        var attempts = 0;

        await TransactionContext.RunAsync(
            async context =>
            {
                attempts++;
                if (attempts == 1)
                    throw new AggregateException(new SimulatedTransientException("transient inner"));
                await Task.Yield();
            },
            retryPolicy: new TransactionRetryPolicy(maxAttempts: 2));

        Assert.Equal(2, attempts);
    }

    [Fact]
    public static async Task CustomClassifierCanDeclareFailuresNonRetryable()
    {
        var attempts = 0;
        var policy = new TransactionRetryPolicy(
            maxAttempts: 3,
            classifier: new FixedClassifier(RetryClassification.NonTransient));

        var ex = await Assert.ThrowsAsync<SimulatedTransientException>(() =>
            TransactionContext.RunAsync(
                async context =>
                {
                    attempts++;
                    await Task.Yield();
                    throw new SimulatedTransientException("declared non-retryable");
                },
                retryPolicy: policy));

        Assert.Equal(1, attempts);
        Assert.Contains("declared non-retryable", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public static async Task DefaultPolicyNeverRetries()
    {
        var attempts = 0;

        var ex = await Assert.ThrowsAsync<SimulatedTransientException>(() =>
            TransactionContext.RunAsync(async context =>
            {
                attempts++;
                await Task.Yield();
                throw new SimulatedTransientException("no policy");
            }));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public static async Task TransientCommitFailureRetriesWithAFreshScope()
    {
        var resource = new RecordingResource(
            "resource",
            commitFailureAt: 1,
            transientFailure: true);

        await TransactionContext.RunAsync(
            context =>
            {
                context.Enlist(resource);
                return Task.CompletedTask;
            },
            retryPolicy: new TransactionRetryPolicy(maxAttempts: 2));

        Assert.True(resource.RolledBack);
        Assert.True(resource.CommitSucceeded);
    }

    [Fact]
    public static async Task RetryDelayReceivesCompletedAttemptNumbers()
    {
        var attempts = 0;
        var delayCalls = new List<int>();

        await TransactionContext.RunAsync(
            async context =>
            {
                attempts++;
                if (attempts < 3)
                    throw new SimulatedTransientException("retry me");
                await Task.Yield();
            },
            retryPolicy: new TransactionRetryPolicy(
                maxAttempts: 3,
                delayForAttempt: completedAttempts =>
                {
                    delayCalls.Add(completedAttempts);
                    return TimeSpan.FromMilliseconds(1);
                }));

        Assert.Equal(3, attempts);
        Assert.Equal([1, 2], delayCalls.ToArray());
    }

    [Fact]
    public static async Task RetryPolicyRejectsInvalidMaxAttempts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TransactionRetryPolicy(maxAttempts: 0));
    }
}
