namespace ALKAROS.Inventory.PortionReservations.Lifecycle;

public sealed record CreateReservationCommand(
    Guid OrderId,
    Guid OrderItemId,
    Guid StockItemId,
    Guid StockLocationId,
    decimal Quantity,
    string UnitCode,
    Guid CreatedBy,
    string? IdempotencyKey = null,
    string? MetadataJson = null,
    Guid? Id = null);

public sealed record TransitionReservationCommand(
    Guid ReservationId,
    PortionReservationStatus TargetStatus,
    Guid TransitionedBy,
    string? Reason = null,
    string? IdempotencyKey = null);

public sealed class PortionReservation
{
    public PortionReservation(
        Guid id,
        Guid orderId,
        Guid orderItemId,
        Guid stockItemId,
        Guid stockLocationId,
        decimal quantity,
        string unitCode,
        PortionReservationStatus status,
        int version,
        DateTimeOffset reservedAt,
        Guid createdBy,
        string? idempotencyKey = null,
        DateTimeOffset? transitionedAt = null,
        string? transitionReason = null,
        Guid? transitionedBy = null,
        string? metadataJson = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (orderId == Guid.Empty)
            throw new ArgumentException("OrderId cannot be empty.", nameof(orderId));
        if (orderItemId == Guid.Empty)
            throw new ArgumentException("OrderItemId cannot be empty.", nameof(orderItemId));
        if (stockItemId == Guid.Empty)
            throw new ArgumentException("StockItemId cannot be empty.", nameof(stockItemId));
        if (stockLocationId == Guid.Empty)
            throw new ArgumentException("StockLocationId cannot be empty.", nameof(stockLocationId));
        if (quantity <= 0m)
            throw new InvalidPortionReservationQuantityException($"Quantity must be strictly positive, got {quantity}.");
        if (string.IsNullOrWhiteSpace(unitCode))
            throw new ArgumentException("UnitCode cannot be empty.", nameof(unitCode));
        if (createdBy == Guid.Empty)
            throw new UnauthorizedReservationActorException("CreatedBy actor ID cannot be empty.");
        if (version < 1)
            throw new ArgumentException("Version must be at least 1.", nameof(version));

        Id = id;
        OrderId = orderId;
        OrderItemId = orderItemId;
        StockItemId = stockItemId;
        StockLocationId = stockLocationId;
        Quantity = quantity;
        UnitCode = unitCode.Trim().ToLowerInvariant();
        Status = status;
        Version = version;
        ReservedAt = reservedAt;
        CreatedBy = createdBy;
        IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        TransitionedAt = transitionedAt;
        TransitionReason = transitionReason?.Trim();
        TransitionedBy = transitionedBy;
        MetadataJson = metadataJson;
    }

    public Guid Id { get; }
    public Guid OrderId { get; }
    public Guid OrderItemId { get; }
    public Guid StockItemId { get; }
    public Guid StockLocationId { get; }
    public decimal Quantity { get; }
    public string UnitCode { get; }
    public PortionReservationStatus Status { get; private set; }
    public int Version { get; private set; }
    public string? IdempotencyKey { get; }
    public DateTimeOffset ReservedAt { get; }
    public DateTimeOffset? TransitionedAt { get; private set; }
    public string? TransitionReason { get; private set; }
    public Guid CreatedBy { get; }
    public Guid? TransitionedBy { get; private set; }
    public string? MetadataJson { get; }

    public static PortionReservation Create(
        Guid orderId,
        Guid orderItemId,
        Guid stockItemId,
        Guid stockLocationId,
        decimal quantity,
        string unitCode,
        Guid createdBy,
        string? idempotencyKey = null,
        string? metadataJson = null,
        Guid? id = null)
    {
        return new PortionReservation(
            id: id ?? Guid.NewGuid(),
            orderId: orderId,
            orderItemId: orderItemId,
            stockItemId: stockItemId,
            stockLocationId: stockLocationId,
            quantity: quantity,
            unitCode: unitCode,
            status: PortionReservationStatus.Reserved,
            version: 1,
            reservedAt: DateTimeOffset.UtcNow,
            createdBy: createdBy,
            idempotencyKey: idempotencyKey,
            metadataJson: metadataJson);
    }

    public void TransitionTo(PortionReservationStatus targetStatus, Guid actorId, string? reason, DateTimeOffset? at = null)
    {
        if (actorId == Guid.Empty)
            throw new UnauthorizedReservationActorException("Transition actor ID cannot be empty.");

        if (Status != PortionReservationStatus.Reserved)
        {
            throw new InvalidPortionReservationTransitionException(
                $"Cannot transition reservation '{Id}' from terminal status '{Status}' to '{targetStatus}'.");
        }

        if (targetStatus == PortionReservationStatus.Reserved)
        {
            throw new InvalidPortionReservationTransitionException("Reservation is already in Reserved status.");
        }

        Status = targetStatus;
        TransitionedBy = actorId;
        TransitionReason = reason?.Trim();
        TransitionedAt = at ?? DateTimeOffset.UtcNow;
        Version++;
    }

    public void Release(Guid actorId, string? reason = null, DateTimeOffset? at = null)
        => TransitionTo(PortionReservationStatus.Released, actorId, reason, at);

    public void Consume(Guid actorId, string? reason = null, DateTimeOffset? at = null)
        => TransitionTo(PortionReservationStatus.Consumed, actorId, reason, at);

    public void Waste(Guid actorId, string? reason = null, DateTimeOffset? at = null)
        => TransitionTo(PortionReservationStatus.Waste, actorId, reason, at);
}

public sealed record ReservationTransitionResult(
    PortionReservation Reservation,
    bool IsIdempotentReplay);
