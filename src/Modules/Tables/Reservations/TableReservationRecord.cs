namespace ALKAROS.Tables.Reservations;

/// <summary>
/// A persistent domain model of a table reservation record (table_mgmt.table_reservations, PDF:II.5.15, V0-DOM-005).
/// Holds the authoritative actor, reason, party size, and expiration for a table's Reserved state.
/// </summary>
public sealed class TableReservationRecord
{
    public TableReservationRecord(
        Guid id,
        Guid tableId,
        Guid? orderId,
        Guid? actorId,
        TableReservationActorType actorType,
        TableReservationStatus status,
        string reason,
        int partySize,
        DateTimeOffset reservedAt,
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? releasedAt = null,
        Guid? releasedBy = null,
        string? releaseReason = null,
        long rowVersion = 1)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Reservation ID cannot be empty.", nameof(id));
        if (tableId == Guid.Empty)
            throw new ArgumentException("Table ID cannot be empty.", nameof(tableId));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason cannot be empty.", nameof(reason));
        if (partySize <= 0)
            throw new ArgumentOutOfRangeException(nameof(partySize), "Party size must be positive.");
        if (rowVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(rowVersion), "Row version must be positive.");

        Id = id;
        TableId = tableId;
        OrderId = orderId;
        ActorId = actorId;
        ActorType = actorType;
        Status = status;
        Reason = reason;
        PartySize = partySize;
        ReservedAt = reservedAt;
        ExpiresAt = expiresAt;
        ReleasedAt = releasedAt;
        ReleasedBy = releasedBy;
        ReleaseReason = releaseReason;
        RowVersion = rowVersion;
    }

    public Guid Id { get; }
    public Guid TableId { get; }
    public Guid? OrderId { get; }
    public Guid? ActorId { get; }
    public TableReservationActorType ActorType { get; }
    public TableReservationStatus Status { get; }
    public string Reason { get; }
    public int PartySize { get; }
    public DateTimeOffset ReservedAt { get; }
    public DateTimeOffset? ExpiresAt { get; }
    public DateTimeOffset? ReleasedAt { get; }
    public Guid? ReleasedBy { get; }
    public string? ReleaseReason { get; }
    public long RowVersion { get; }

    public bool IsActive => Status == TableReservationStatus.Active;
}
