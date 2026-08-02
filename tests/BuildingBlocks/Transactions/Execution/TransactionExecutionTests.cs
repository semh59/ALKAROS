using ALKAROS.TestHelpers;
using Xunit;

namespace ALKAROS.Transactions.Tests.Execution;

/// <summary>
/// Tests for the commit/rollback orchestration of a single transaction
/// execution, including the crash-window behavior.
/// </summary>
public static class TransactionExecutionTests
{
    [Fact]
    public static async Task AllResourcesCommitInEnlistmentOrder()
    {
        var journal = new List<string>();
        var first = new RecordingResource("first", sharedLog: journal);
        var second = new RecordingResource("second", sharedLog: journal);
        var third = new RecordingResource("third", sharedLog: journal);

        await TransactionContext.RunAsync(context =>
        {
            context.Enlist(first);
            context.Enlist(second);
            context.Enlist(third);
            return Task.CompletedTask;
        });

        Assert.True(first.CommitSucceeded);
        Assert.True(second.CommitSucceeded);
        Assert.True(third.CommitSucceeded);
        Assert.Equal(
            ["first:committed", "second:committed", "third:committed"],
            journal.ToArray());
    }

    [Fact]
    public static async Task WorkflowExceptionRollsBackAllResourcesInReverseOrder()
    {
        var journal = new List<string>();
        var first = new RecordingResource("first", sharedLog: journal);
        var second = new RecordingResource("second", sharedLog: journal);
        var third = new RecordingResource("third", sharedLog: journal);

        await Assert.ThrowsAsync<SimulatedFailureException>(() =>
            TransactionContext.RunAsync(context =>
            {
                context.Enlist(first);
                context.Enlist(second);
                context.Enlist(third);
                return Task.FromException(new SimulatedFailureException("workflow failed"));
            }));

        Assert.True(first.RolledBack);
        Assert.True(second.RolledBack);
        Assert.True(third.RolledBack);
        Assert.False(first.CommitSucceeded);
        Assert.Equal(
            ["third:rolled-back", "second:rolled-back", "first:rolled-back"],
            journal.ToArray());
    }

    [Fact]
    public static async Task WorkflowExceptionIsRethrownUnchanged()
    {
        var failure = new SimulatedFailureException("boom");
        var resource = new RecordingResource("resource");

        var ex = await Assert.ThrowsAsync<SimulatedFailureException>(() =>
            TransactionContext.RunAsync(context =>
            {
                context.Enlist(resource);
                return Task.FromException(failure);
            }));

        Assert.Same(failure, ex);
        Assert.True(resource.RolledBack);
    }

    [Fact]
    public static async Task CommitFailureRollsBackEveryResourceIncludingAlreadyCommittedOnes()
    {
        var journal = new List<string>();
        var first = new RecordingResource("first", sharedLog: journal);
        var second = new RecordingResource("second", commitFailureAt: 1, sharedLog: journal);
        var third = new RecordingResource("third", sharedLog: journal);

        var ex = await Assert.ThrowsAsync<SimulatedFailureException>(() =>
            TransactionContext.RunAsync(context =>
            {
                context.Enlist(first);
                context.Enlist(second);
                context.Enlist(third);
                return Task.CompletedTask;
            }));

        Assert.Contains("commit failed", ex.Message, StringComparison.Ordinal);
        Assert.True(first.CommitSucceeded);
        Assert.True(first.RolledBack);
        Assert.False(second.CommitSucceeded);
        Assert.True(second.RolledBack);
        Assert.True(third.RolledBack);
        Assert.Equal(
            [
                "first:committed",
                "second:commit-failed",
                "third:rolled-back",
                "second:rolled-back",
                "first:rolled-back",
            ],
            journal.ToArray());
    }

    [Fact]
    public static async Task RollbackFailureSurfacesTransactionExecutionException()
    {
        var resource = new RecordingResource("resource", rollbackFails: true);

        var ex = await Assert.ThrowsAsync<TransactionExecutionException>(() =>
            TransactionContext.RunAsync(context =>
            {
                context.Enlist(resource);
                return Task.FromException(new SimulatedFailureException("workflow failed"));
            }));

        var aggregate = Assert.IsType<AggregateException>(ex.InnerException);
        Assert.Equal(2, aggregate.InnerExceptions.Count);
    }

    [Fact]
    public static async Task EnlistRejectsNullResource()
    {
        await TransactionContext.RunAsync(context =>
        {
            Assert.Throws<ArgumentNullException>(() => context.Enlist(null!));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public static async Task RunRejectsNullWorkflow()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            TransactionContext.RunAsync(null!));
    }

    [Fact]
    public static async Task RunRejectsInvalidJoinBehavior()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            TransactionContext.RunAsync(
                _ => Task.CompletedTask,
                new TransactionOptions
                {
                    JoinBehavior = (TransactionJoinBehavior)99,
                }));
    }

    [Fact]
    public static async Task CancellationRollsBackAndIsNotRetried()
    {
        var resource = new RecordingResource("resource");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var attempts = 0;

        var ex = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            TransactionContext.RunAsync(
                async context =>
                {
                    attempts++;
                    context.Enlist(resource);
                    await Task.Yield();
                    cts.Token.ThrowIfCancellationRequested();
                },
                retryPolicy: new TransactionRetryPolicy(maxAttempts: 3)));

        Assert.Equal(cts.Token, ex.CancellationToken);
        Assert.Equal(1, attempts);
        Assert.True(resource.RolledBack);
    }
}
