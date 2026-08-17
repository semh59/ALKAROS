using ALKAROS.TestHelpers;
using Xunit;

namespace ALKAROS.Transactions.Tests.Concurrency;

/// <summary>
/// Tests that concurrent root transactions are isolated: the ambient
/// transaction of one flow never leaks into another flow.
/// </summary>
public static class TransactionConcurrencyTests
{
    [Fact]
    public static async Task ParallelRootTransactionsDoNotLeakAmbientState()
    {
        var heldStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var heldResource = new RecordingResource("held");
        var fastResource = new RecordingResource("fast");
        Guid? heldId = null;
        Guid? fastId = null;

        var heldTask = TransactionContext.RunAsync(async context =>
        {
            heldId = context.Id;
            context.Enlist(heldResource);
            heldStarted.SetResult();
            await gate.Task;
        });

        await heldStarted.Task;

        var fastTask = TransactionContext.RunAsync(context =>
        {
            fastId = context.Id;
            context.Enlist(fastResource);
            return Task.CompletedTask;
        });

        await fastTask;

        Assert.True(fastResource.CommitSucceeded);
        Assert.False(heldResource.CommitSucceeded);
        Assert.NotNull(heldId);
        Assert.NotNull(fastId);
        Assert.NotEqual(heldId, fastId);

        gate.SetResult();
        await heldTask;

        Assert.True(heldResource.CommitSucceeded);
        Assert.False(heldResource.RolledBack);
        Assert.False(fastResource.RolledBack);
    }

    [Fact]
    public static async Task ManyParallelRootTransactionsCommitIndependently()
    {
        const int count = 16;
        var resources = Enumerable.Range(0, count)
            .Select(i => new RecordingResource($"resource-{i}"))
            .ToArray();

        var tasks = new Task[count];
        for (var i = 0; i < count; i++)
        {
            var index = i;
            tasks[index] = TransactionContext.RunAsync(context =>
            {
                context.Enlist(resources[index]);
                return Task.CompletedTask;
            });
        }

        await Task.WhenAll(tasks);

        for (var i = 0; i < count; i++)
        {
            Assert.True(resources[i].CommitSucceeded, $"resource-{i} must commit");
            Assert.False(resources[i].RolledBack, $"resource-{i} must not roll back");
        }
    }

    [Fact]
    public static async Task ParallelRootWithNestedJoinKeepsSeparateScopes()
    {
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstResource = new RecordingResource("first-inner");
        var secondResource = new RecordingResource("second-inner");

        var firstTask = TransactionContext.RunAsync(async context =>
        {
            await TransactionContext.RunAsync(async nested =>
            {
                nested.Enlist(firstResource);
                firstStarted.SetResult();
                await gate.Task;
            });
        });

        await firstStarted.Task;

        var secondTask = TransactionContext.RunAsync(async context =>
        {
            await TransactionContext.RunAsync(nested =>
            {
                nested.Enlist(secondResource);
                return Task.CompletedTask;
            });
        });

        await secondTask;
        Assert.True(secondResource.CommitSucceeded);
        Assert.False(firstResource.CommitSucceeded);

        gate.SetResult();
        await firstTask;
        Assert.True(firstResource.CommitSucceeded);
    }
}
