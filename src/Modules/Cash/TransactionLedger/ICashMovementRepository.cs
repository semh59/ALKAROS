using Npgsql;

namespace ALKAROS.Cash.TransactionLedger;

/// <summary>Persistence for cash-out approval requests and the drawer screen's movement list.</summary>
public interface ICashMovementRepository
{
    /// <summary>The request already opened under this session and key, for idempotent replay.</summary>
    Task<CashMovementRequest?> GetRequestBySessionAndKeyAsync(
        Guid cashSessionId, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>The same lookup inside the caller's transaction.</summary>
    Task<CashMovementRequest?> GetRequestBySessionAndKeyAsync(
        Guid cashSessionId, string idempotencyKey, NpgsqlConnection connection, NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);

    Task InsertRequestAsync(
        CashMovementRequest request, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>Loads and row-locks a request inside the caller's transaction.</summary>
    Task<CashMovementRequest?> GetRequestForUpdateAsync(
        Guid requestId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default);

    Task MarkDecidedAsync(
        Guid requestId, string status, Guid decidedBy, DateTimeOffset decidedAt, string? decisionNote, bool selfApproved,
        Guid? cashTransactionId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>The session's open requests, oldest first, at most <see cref="MaxListedRequests"/>.</summary>
    Task<IReadOnlyList<PendingCashMovementRequest>> ListPendingAsync(Guid cashSessionId, CancellationToken cancellationToken = default);

    Task<bool> HasPendingAsync(Guid cashSessionId, CancellationToken cancellationToken = default);

    /// <summary>The session's most recent manual cash-in/cash-out rows (newest first, at most <see cref="MaxListedMovements"/> + 1: the extra row tells the caller the list was cut).</summary>
    Task<IReadOnlyList<CashMovementEntry>> ListMovementsAsync(Guid cashSessionId, CancellationToken cancellationToken = default);

    const int MaxListedMovements = 200;

    /// <summary>Open requests listed per session; a drawer never has more waiting.</summary>
    const int MaxListedRequests = 100;
}
