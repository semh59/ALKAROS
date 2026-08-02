using ALKAROS.TestHelpers;
using Xunit;

namespace ALKAROS.Transactions.Tests.Propagation;

/// <summary>
/// Tests for ambient transaction propagation and nested-call rejection.
/// </summary>
public static class TransactionPropagationTests
{
    [Fact]
    public static async Task NestedRunJoinsTheAmbientTransaction()
    {
        var outerResource = new RecordingResource("outer");
        var innerResource = new RecordingResource("inner");
        Guid? outerId = null;
        Guid? innerId = null;

        await TransactionContext.RunAsync(async context =>
        {
            outerId = context.Id;
            context.Enlist(outerResource);
            await TransactionContext.RunAsync(async nested =>
            {
                innerId = nested.Id;
                nested.Enlist(innerResource);
                await Task.Yield();
            });
        });

        Assert.NotNull(outerId);
        Assert.Equal(outerId, innerId);
        Assert.True(outerResource.CommitSucceeded);
        Assert.True(innerResource.CommitSucceeded);
    }

    [Fact]
    public static async Task NestedWorkflowFailureRollsBackTheWholeAmbientTransaction()
    {
        var outerResource = new RecordingResource("outer");
        var innerResource = new RecordingResource("inner");

        await Assert.ThrowsAsync<SimulatedFailureException>(() =>
            TransactionContext.RunAsync(async context =>
            {
                context.Enlist(outerResource);
                await TransactionContext.RunAsync(nested =>
                {
                    nested.Enlist(innerResource);
                    return Task.FromException(new SimulatedFailureException("nested failed"));
                });
            }));

        Assert.True(outerResource.RolledBack);
        Assert.True(innerResource.RolledBack);
        Assert.False(outerResource.CommitSucceeded);
        Assert.False(innerResource.CommitSucceeded);
    }

    [Fact]
    public static async Task CreateNewInsideActiveScopeIsRejected()
    {
        var outerResource = new RecordingResource("outer");

        await Assert.ThrowsAsync<NestedTransactionException>(() =>
            TransactionContext.RunAsync(async context =>
            {
                context.Enlist(outerResource);
                await TransactionContext.RunAsync(
                    _ => Task.CompletedTask,
                    new TransactionOptions
                    {
                        JoinBehavior = TransactionJoinBehavior.CreateNew,
                    });
                await Task.Yield();
            }));

        Assert.False(outerResource.CommitSucceeded);
        Assert.True(outerResource.RolledBack);
    }

    [Fact]
    public static async Task CreateNewWithoutAmbientStartsARootTransaction()
    {
        var resource = new RecordingResource("root");

        await TransactionContext.RunAsync(
            context =>
            {
                context.Enlist(resource);
                return Task.CompletedTask;
            },
            new TransactionOptions
            {
                JoinBehavior = TransactionJoinBehavior.CreateNew,
            });

        Assert.True(resource.CommitSucceeded);
    }
}
