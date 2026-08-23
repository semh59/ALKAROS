using FluentAssertions;
using Xunit;

namespace ALKAROS.Clients.WaiterPwa.SessionQueue.Tests;

public sealed class WaiterSessionQueueTests
{
    private readonly WaiterOfflineQueueEngine _engine = new();

    [Fact]
    public void BrowserRestartRetainsPersistedAllowableQueue()
    {
        var directory = Path.Combine(Path.GetTempPath(), "alkaros-waiter-queue-tests", Guid.NewGuid().ToString("N"));
        var storePath = Path.Combine(directory, "offline-queue.json");
        var firstEngine = new WaiterOfflineQueueEngine(new JsonFileOfflineQueueStore(storePath));
        var session = new WaiterPwaSession(
            SessionId: Guid.NewGuid(),
            WaiterId: Guid.NewGuid(),
            WaiterName: "Garson Can",
            DeviceFingerprint: "device_pwa_tablet_01",
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(8),
            IsActive: true,
            IsRevoked: false);

        firstEngine.SetSession(session);
        firstEngine.EnqueueOperation(
            QueuedOperationType.SubmitOrder,
            "{\"table\":\"M-03\",\"items\":[{\"id\":\"p1\",\"qty\":2}]}",
            DateTimeOffset.UtcNow.AddMinutes(-5));
        firstEngine.EnqueueOperation(
            QueuedOperationType.AddOrderNote,
            "{\"table\":\"M-03\",\"note\":\"Alerji uyarısı\"}",
            DateTimeOffset.UtcNow.AddMinutes(-3));

        var restartedEngine = new WaiterOfflineQueueEngine(new JsonFileOfflineQueueStore(storePath));

        restartedEngine.PendingOperations.Should().HaveCount(2);
        restartedEngine.PendingOperations[0].OperationType.Should().Be(QueuedOperationType.SubmitOrder);
        restartedEngine.PendingOperations[1].OperationType.Should().Be(QueuedOperationType.AddOrderNote);

        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public void CorruptPersistentQueueFailsClosed()
    {
        var directory = Path.Combine(Path.GetTempPath(), "alkaros-waiter-queue-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var storePath = Path.Combine(directory, "offline-queue.json");
        File.WriteAllText(storePath, "not-json");

        var act = () => new WaiterOfflineQueueEngine(new JsonFileOfflineQueueStore(storePath));

        act.Should().Throw<InvalidDataException>();
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public void ExplicitQueueImportPersistsAcrossRestart()
    {
        var store = new InMemoryOfflineQueueStore();
        var engine = new WaiterOfflineQueueEngine(store);
        var persistedOps = new List<QueuedOperation>
        {
            new(Guid.NewGuid(), "idemp_1", QueuedOperationType.SubmitOrder, "{\"table\":\"M-03\",\"items\":[{\"id\":\"p1\",\"qty\":2}]}", DateTimeOffset.UtcNow.AddMinutes(-5)),
            new(Guid.NewGuid(), "idemp_2", QueuedOperationType.AddOrderNote, "{\"table\":\"M-03\",\"note\":\"Alerji uyarısı\"}", DateTimeOffset.UtcNow.AddMinutes(-3))
        };

        engine.LoadPersistedQueue(persistedOps);
        var restartedEngine = new WaiterOfflineQueueEngine(store);

        restartedEngine.PendingOperations.Should().HaveCount(2);
    }

    [Fact]
    public void RevokedSessionCannotReplayQueuedOperations()
    {
        var session = new WaiterPwaSession(
            SessionId: Guid.NewGuid(),
            WaiterId: Guid.NewGuid(),
            WaiterName: "Garson Elif",
            DeviceFingerprint: "device_pwa_phone_02",
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(4),
            IsActive: true,
            IsRevoked: false);

        _engine.SetSession(session);

        _engine.EnqueueOperation(QueuedOperationType.SubmitOrder, "{\"table\":\"B-01\"}");
        _engine.PendingOperations.Should().ContainSingle();

        // Supervisor revokes session (Acceptance Evidence #2)
        _engine.RevokeSession();

        var replayResults = _engine.ReplayPendingQueue();

        replayResults.Should().ContainSingle();
        replayResults[0].IsReplayed.Should().BeFalse();
        replayResults[0].ErrorMessage.Should().Contain("Oturum iptal edilmiş");

        // Queue not cleared on rejected replay
        _engine.PendingOperations.Should().ContainSingle();
    }

    [Fact]
    public void UnsupportedDirectPaymentSettlementNeverReportsOfflineSuccess()
    {
        var session = new WaiterPwaSession(
            SessionId: Guid.NewGuid(),
            WaiterId: Guid.NewGuid(),
            WaiterName: "Garson Murat",
            DeviceFingerprint: "device_pwa_phone_03",
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(6),
            IsActive: true,
            IsRevoked: false);

        _engine.SetSession(session);

        // Attempting direct payment settlement in offline queue (Acceptance Evidence #3)
        var result = _engine.EnqueueOperation(
            QueuedOperationType.DirectPaymentSettlement,
            "{\"bill_id\":\"b1\",\"amount\":150.00}");

        result.IsEnqueued.Should().BeFalse();
        result.IsRejectedUnsupportedOffline.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Ödeme ve mali işlemler çevrimdışı kuyruğa alınamaz");

        _engine.PendingOperations.Should().BeEmpty();
    }

    [Fact]
    public void ValidSessionOnlineReplayClearsPendingQueue()
    {
        var session = new WaiterPwaSession(
            SessionId: Guid.NewGuid(),
            WaiterId: Guid.NewGuid(),
            WaiterName: "Garson Deniz",
            DeviceFingerprint: "device_pwa_phone_04",
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(5),
            IsActive: true,
            IsRevoked: false);

        _engine.SetSession(session);

        _engine.EnqueueOperation(QueuedOperationType.SubmitOrder, "{\"table\":\"T-02\"}");
        _engine.EnqueueOperation(QueuedOperationType.UpdateTableStatus, "{\"table\":\"T-02\",\"status\":\"Occupied\"}");

        _engine.PendingOperations.Should().HaveCount(2);

        var replayResults = _engine.ReplayPendingQueue(serverDispatcher: _ => true);

        replayResults.Should().HaveCount(2);
        replayResults.Should().OnlyContain(r => r.IsReplayed);

        // Queue drained
        _engine.PendingOperations.Should().BeEmpty();
    }

    [Fact]
    public void MissingServerDispatcherRejectsReplayWithoutModifyingQueue()
    {
        var session = new WaiterPwaSession(
            SessionId: Guid.NewGuid(),
            WaiterId: Guid.NewGuid(),
            WaiterName: "Garson Deniz",
            DeviceFingerprint: "device_pwa_phone_04b",
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(5),
            IsActive: true,
            IsRevoked: false);

        _engine.SetSession(session);
        _engine.EnqueueOperation(QueuedOperationType.SubmitOrder, "{\"table\":\"T-02\"}");

        var replayResults = _engine.ReplayPendingQueue(serverDispatcher: null);

        replayResults.Should().ContainSingle();
        replayResults[0].IsReplayed.Should().BeFalse();
        replayResults[0].ErrorMessage.Should().Contain("serverDispatcher");

        _engine.PendingOperations.Should().ContainSingle();
    }

    [Fact]
    public void OfflineReplayLeavesQueueIntactAndReturnsOfflineError()
    {
        var session = new WaiterPwaSession(
            SessionId: Guid.NewGuid(),
            WaiterId: Guid.NewGuid(),
            WaiterName: "Garson Deniz",
            DeviceFingerprint: "device_pwa_phone_05",
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(5),
            IsActive: true,
            IsRevoked: false);

        _engine.SetSession(session);
        _engine.EnqueueOperation(QueuedOperationType.SubmitOrder, "{\"table\":\"T-05\"}");
        _engine.PendingOperations.Should().ContainSingle();

        // Simulate network loss
        _engine.SetNetworkState(false);

        var replayResults = _engine.ReplayPendingQueue();

        replayResults.Should().ContainSingle();
        replayResults[0].IsReplayed.Should().BeFalse();
        replayResults[0].ErrorMessage.Should().Contain("çevrimdışı");

        // Queue remains intact
        _engine.PendingOperations.Should().ContainSingle();
    }

    [Fact]
    public void FailedServerDispatchLeavesFailedOperationInPendingQueue()
    {
        var session = new WaiterPwaSession(
            SessionId: Guid.NewGuid(),
            WaiterId: Guid.NewGuid(),
            WaiterName: "Garson Deniz",
            DeviceFingerprint: "device_pwa_phone_06",
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(5),
            IsActive: true,
            IsRevoked: false);

        _engine.SetSession(session);
        _engine.EnqueueOperation(QueuedOperationType.SubmitOrder, "{\"table\":\"T-06\"}");
        _engine.EnqueueOperation(QueuedOperationType.AddOrderNote, "{\"note\":\"Acısız\"}");

        // Dispatcher approves only the first op
        var replayResults = _engine.ReplayPendingQueue(serverDispatcher: op => op.OperationType == QueuedOperationType.SubmitOrder);

        replayResults.Should().HaveCount(2);
        replayResults[0].IsReplayed.Should().BeTrue();
        replayResults[1].IsReplayed.Should().BeFalse();

        // Second operation remains in queue
        _engine.PendingOperations.Should().ContainSingle();
        _engine.PendingOperations[0].OperationType.Should().Be(QueuedOperationType.AddOrderNote);
    }
}
