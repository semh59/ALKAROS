namespace ALKAROS.Clients.WaiterPwa.SessionQueue;

/// <summary>
/// Domain engine for Waiter PWA offline queuing, persistence, and reconnection replay (V1-WTR-001, PDF:I.14-I.15, V0-CMP-005).
/// </summary>
public sealed class WaiterOfflineQueueEngine
{
    private WaiterPwaSession? _session;
    private readonly List<QueuedOperation> _queue = new();
    private bool _isOnline = true;

    public WaiterPwaSession? CurrentSession => _session;
    public bool IsOnline => _isOnline;
    public IReadOnlyList<QueuedOperation> PendingOperations => _queue.AsReadOnly();

    public void SetSession(WaiterPwaSession session)
    {
        _session = session;
    }

    public void RevokeSession()
    {
        if (_session is not null)
        {
            _session = _session with { IsRevoked = true, IsActive = false };
        }
    }

    public void SetNetworkState(bool isOnline)
    {
        _isOnline = isOnline;
    }

    /// <summary>
    /// Loads persisted operations from local storage (e.g. following browser restart).
    /// </summary>
    public void LoadPersistedQueue(IEnumerable<QueuedOperation> operations)
    {
        _queue.Clear();
        if (operations is not null)
        {
            _queue.AddRange(operations);
        }
    }

    /// <summary>
    /// Enqueues an operation. Unsupported offline operations (e.g. payment settlement) are strictly rejected.
    /// </summary>
    public QueueOperationResult EnqueueOperation(
        QueuedOperationType operationType,
        string payloadJson,
        DateTimeOffset? utcNow = null)
    {
        var now = utcNow ?? DateTimeOffset.UtcNow;

        if (_session is null || !_session.IsValid(now))
        {
            return new QueueOperationResult(
                IsEnqueued: false,
                IsReplayed: false,
                ErrorMessage: "Geçersiz veya süresi dolmuş oturum. İşlem sıraya alınamadı.",
                IsRejectedUnsupportedOffline: false);
        }

        // Acceptance evidence #3: Unsupported offline operations (such as payment) NEVER report offline success
        if (operationType == QueuedOperationType.DirectPaymentSettlement)
        {
            return new QueueOperationResult(
                IsEnqueued: false,
                IsReplayed: false,
                ErrorMessage: "Ödeme ve mali işlemler çevrimdışı kuyruğa alınamaz. Çevrimiçi bağlantı gereklidir.",
                IsRejectedUnsupportedOffline: true);
        }

        var op = new QueuedOperation(
            Guid.NewGuid(),
            Guid.NewGuid().ToString("N"),
            operationType,
            payloadJson,
            now,
            0);

        _queue.Add(op);

        return new QueueOperationResult(
            IsEnqueued: true,
            IsReplayed: false,
            ErrorMessage: null,
            IsRejectedUnsupportedOffline: false);
    }

    /// <summary>
    /// Replays pending queue when back online. Rejects replay if offline or session was revoked.
    /// </summary>
    public IReadOnlyList<QueueOperationResult> ReplayPendingQueue(
        Func<QueuedOperation, bool>? serverDispatcher = null,
        DateTimeOffset? utcNow = null)
    {
        var now = utcNow ?? DateTimeOffset.UtcNow;
        var results = new List<QueueOperationResult>();

        // Precondition: Network connectivity must be active to replay
        if (!_isOnline)
        {
            results.Add(new QueueOperationResult(
                IsEnqueued: false,
                IsReplayed: false,
                ErrorMessage: "Cihaz çevrimdışı. Kuyruk ancak çevrimiçi olunduğunda sunucuya iletilebilir.",
                IsRejectedUnsupportedOffline: false));

            return results;
        }

        // Acceptance evidence #2: Revoked/expired session cannot replay queue
        if (_session is null || !_session.IsValid(now))
        {
            results.Add(new QueueOperationResult(
                IsEnqueued: false,
                IsReplayed: false,
                ErrorMessage: "Oturum iptal edilmiş veya süresi dolmuş. Çevrimdışı kuyruk sunucuya iletilemez.",
                IsRejectedUnsupportedOffline: false));

            return results;
        }

        // Precondition: Server dispatcher adapter is required to acknowledge delivery (WTR-01)
        if (serverDispatcher is null)
        {
            results.Add(new QueueOperationResult(
                IsEnqueued: false,
                IsReplayed: false,
                ErrorMessage: "Sunucu bağlantı dağıtıcısı (serverDispatcher) gereklidir.",
                IsRejectedUnsupportedOffline: false));

            return results;
        }

        var operationsToProcess = _queue.ToList();
        foreach (var op in operationsToProcess)
        {
            bool success = true;
            string? errorMessage = null;

            try
            {
                success = serverDispatcher(op);
                if (!success)
                {
                    errorMessage = "Sunucu işlemi onaylamadı.";
                }
            }
            catch (Exception ex)
            {
                success = false;
                errorMessage = ex.Message;
            }

            if (success)
            {
                results.Add(new QueueOperationResult(
                    IsEnqueued: true,
                    IsReplayed: true,
                    ErrorMessage: null,
                    IsRejectedUnsupportedOffline: false));

                _queue.Remove(op);
            }
            else
            {
                results.Add(new QueueOperationResult(
                    IsEnqueued: true,
                    IsReplayed: false,
                    ErrorMessage: errorMessage,
                    IsRejectedUnsupportedOffline: false));

                // On first error, stop replay loop to preserve FIFO ordering for dependent operations
                break;
            }
        }

        return results;
    }
}
