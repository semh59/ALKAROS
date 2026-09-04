using System;
using System.Linq;
using ALKAROS.Clients.WaiterPwa.ManagerDecisions;
using FluentAssertions;
using Xunit;

namespace ALKAROS.WaiterPwa.ManagerDecisions.Tests;

public sealed class WaiterManagerDecisionEngineTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 20, 0, 0, TimeSpan.Zero);

    private static WaiterPendingGrant Grant(Guid? id = null, decimal amount = 120m)
        => new(id ?? Guid.NewGuid(), "bills.comp", Guid.NewGuid(), "waiter", amount, "CustomerChange", Now);

    [Fact]
    public void StartsConnectedWithNoPending()
    {
        var state = new WaiterManagerDecisionEngine().CurrentState;

        state.Pending.Should().BeEmpty();
        state.IsConnected.Should().BeTrue();
        state.IsStale.Should().BeFalse();
    }

    [Fact]
    public void DisconnectionMarksTheViewStaleButKeepsTheList()
    {
        var engine = new WaiterManagerDecisionEngine();
        engine.ApplyServerSnapshot(new[] { Grant() }, Now);

        engine.HandleDisconnection();

        engine.CurrentState.IsConnected.Should().BeFalse();
        engine.CurrentState.IsStale.Should().BeTrue();
        engine.CurrentState.Pending.Should().HaveCount(1);
    }

    [Fact]
    public void ReconnectionReplacesTheListWithTheServerSnapshotAndClearsStale()
    {
        var engine = new WaiterManagerDecisionEngine();
        engine.ApplyServerSnapshot(new[] { Grant(), Grant() }, Now);
        engine.HandleDisconnection();

        var fresh = Grant();
        engine.HandleReconnection(new[] { fresh }, Now.AddMinutes(1));

        engine.CurrentState.Pending.Select(g => g.GrantId).Should().Equal(fresh.GrantId);
        engine.CurrentState.IsConnected.Should().BeTrue();
        engine.CurrentState.IsStale.Should().BeFalse();
        engine.CurrentState.LastSyncedAt.Should().Be(Now.AddMinutes(1));
    }

    [Fact]
    public void ALocallyResolvedGrantIsHiddenUntilTheSnapshotDropsIt()
    {
        var engine = new WaiterManagerDecisionEngine();
        var target = Grant();
        var other = Grant();
        engine.ApplyServerSnapshot(new[] { target, other }, Now);

        engine.MarkResolvedLocally(target.GrantId);
        engine.CurrentState.Pending.Select(g => g.GrantId).Should().Equal(other.GrantId);

        // The server still lists it on the next snapshot (race): still hidden.
        engine.ApplyServerSnapshot(new[] { target, other }, Now.AddSeconds(2));
        engine.CurrentState.Pending.Select(g => g.GrantId).Should().Equal(other.GrantId);

        // The server drops it: the hide is retired and would not mask a
        // future grant that happens to reuse the id.
        engine.ApplyServerSnapshot(new[] { other }, Now.AddSeconds(4));
        engine.CurrentState.Pending.Select(g => g.GrantId).Should().Equal(other.GrantId);
        engine.ApplyServerSnapshot(new[] { target, other }, Now.AddSeconds(6));
        engine.CurrentState.Pending.Select(g => g.GrantId).Should().BeEquivalentTo(new[] { target.GrantId, other.GrantId });
    }

    [Fact]
    public void MarkResolvedLocallyIgnoresAnUnknownGrant()
    {
        var engine = new WaiterManagerDecisionEngine();
        engine.ApplyServerSnapshot(new[] { Grant() }, Now);

        engine.MarkResolvedLocally(Guid.NewGuid());

        engine.CurrentState.Pending.Should().HaveCount(1);
    }

    [Fact]
    public void TheViewCannotMutateDirectly()
    {
        WaiterManagerDecisionEngine.TryMutateDirectly(out var error).Should().BeFalse();
        error.Should().Contain("yalnızca");
    }
}
