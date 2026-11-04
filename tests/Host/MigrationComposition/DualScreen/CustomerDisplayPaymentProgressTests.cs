using ALKAROS.Host.DualScreen;
using Xunit;

namespace ALKAROS.Host.Tests.DualScreen;

public sealed class CustomerDisplayPaymentProgressTests
{
    private static CustomerDisplaySnapshotDto Snapshot(string state = "Active") => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 4, state, false, "POS-4", [],
        236.36m, 0m, 23.64m, 260m, "TRY", DateTimeOffset.UtcNow, "Siparişiniz işleniyor.");

    [Fact]
    public async Task AnIdleScreenNeverReadsTheBills()
    {
        var snapshot = Snapshot("Idle");

        // Null collaborators: a read of bills or payments would throw.
        Assert.Equal(snapshot, await CustomerDisplayPaymentProgress.AttachAsync(snapshot, null!, null!, null!, null!, CancellationToken.None));
    }

    [Fact]
    public async Task ASnapshotWithoutAnOrderNeverReadsTheBills()
    {
        var snapshot = Snapshot() with { OrderId = null };

        Assert.Equal(snapshot, await CustomerDisplayPaymentProgress.AttachAsync(snapshot, null!, null!, null!, null!, CancellationToken.None));
    }
}
