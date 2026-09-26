namespace ALKAROS.Kitchen.TicketLifecycle;

using Npgsql;

public interface IKitchenTicketRepository
{
    Task<KitchenTicket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KitchenTicket>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KitchenTicket>> GetActiveByStationAsync(string stationId, CancellationToken cancellationToken = default);
    Task AddAsync(KitchenTicket ticket, CancellationToken cancellationToken = default);
    Task AddAsync(
        KitchenTicket ticket,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);
    Task<long> SaveAsync(KitchenTicket ticket, long expectedRowVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// V1-RMD-313: an order's tickets read inside the caller's transaction with the ticket rows locked
    /// (FOR UPDATE), so they cannot change before the caller's own save of them commits.
    /// </summary>
    Task<IReadOnlyList<KitchenTicket>> GetByOrderIdAsync(
        Guid orderId,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>V1-RMD-313: the same optimistic save inside the caller's transaction.</summary>
    Task<long> SaveAsync(
        KitchenTicket ticket,
        long expectedRowVersion,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// V1-KIT-014: one row per ticket that reached Ready within
    /// [<paramref name="windowStart"/>, <paramref name="windowEnd"/>) (by
    /// <c>created_at</c>) — the raw timing data the Host-layer performance
    /// report aggregates (mean/median/target-overrun/hourly volume). A
    /// ticket never reaching Ready in the window is not "still being made
    /// slowly", it is simply not yet a completed data point, so it is
    /// excluded, not zero-filled. Parameters are not named "from"/"to" —
    /// CA1716 flags "to" as a reserved keyword collision (VB.NET's `To`)
    /// on a public interface member.
    /// </summary>
    Task<IReadOnlyList<CompletedTicketTimingRow>> GetCompletedTicketTimingsAsync(
        DateTimeOffset windowStart, DateTimeOffset windowEnd, CancellationToken cancellationToken = default);
}

/// <summary>One completed ticket's station and timing, for report aggregation.</summary>
public sealed record CompletedTicketTimingRow(string StationId, DateTimeOffset CreatedAt, DateTimeOffset ReadyAt);
