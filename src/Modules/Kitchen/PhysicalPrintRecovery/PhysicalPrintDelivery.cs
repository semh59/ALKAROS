namespace ALKAROS.Kitchen.PhysicalPrintRecovery;

/// <summary>
/// Aggregate root representing a physical print delivery attempt (kitchen.physical_print_deliveries).
/// Enforces crash-window safety, prevents unapproved duplicate prints, and tracks operator authorizations.
/// </summary>
public sealed class PhysicalPrintDelivery
{
    public PhysicalPrintDelivery(
        Guid id,
        Guid printJobId,
        Guid ticketId,
        Guid printerId,
        PhysicalPrintDeliveryStatus status,
        int attemptNumber,
        bool isReprint,
        string? operatorId,
        string? operatorReason,
        string? crashWindowReason,
        string payloadSnapshot,
        string? reprintPayload,
        DateTimeOffset createdAt,
        DateTimeOffset? deliveredAt,
        DateTimeOffset? resolvedAt,
        long rowVersion = 1)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Delivery ID cannot be empty.", nameof(id));
        if (printJobId == Guid.Empty)
            throw new ArgumentException("PrintJob ID cannot be empty.", nameof(printJobId));
        if (ticketId == Guid.Empty)
            throw new ArgumentException("Ticket ID cannot be empty.", nameof(ticketId));
        if (printerId == Guid.Empty)
            throw new ArgumentException("Printer ID cannot be empty.", nameof(printerId));
        if (string.IsNullOrWhiteSpace(payloadSnapshot))
            throw new ArgumentException("Payload snapshot cannot be empty.", nameof(payloadSnapshot));
        if (attemptNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(attemptNumber), "Attempt number must be at least 1.");

        Id = id;
        PrintJobId = printJobId;
        TicketId = ticketId;
        PrinterId = printerId;
        Status = status;
        AttemptNumber = attemptNumber;
        IsReprint = isReprint;
        OperatorId = operatorId;
        OperatorReason = operatorReason;
        CrashWindowReason = crashWindowReason;
        PayloadSnapshot = payloadSnapshot;
        ReprintPayload = reprintPayload;
        CreatedAt = createdAt;
        DeliveredAt = deliveredAt;
        ResolvedAt = resolvedAt;
        RowVersion = rowVersion;
    }

    public Guid Id { get; }
    public Guid PrintJobId { get; }
    public Guid TicketId { get; }
    public Guid PrinterId { get; }
    public PhysicalPrintDeliveryStatus Status { get; private set; }
    public int AttemptNumber { get; }
    public bool IsReprint { get; }
    public string? OperatorId { get; private set; }
    public string? OperatorReason { get; private set; }
    public string? CrashWindowReason { get; private set; }
    public string PayloadSnapshot { get; }
    public string? ReprintPayload { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public long RowVersion { get; internal set; }

    public static PhysicalPrintDelivery CreateInFlight(
        Guid printJobId,
        Guid ticketId,
        Guid printerId,
        string payload,
        int attemptNumber = 1,
        DateTimeOffset? timestamp = null)
    {
        var now = timestamp ?? DateTimeOffset.UtcNow;
        return new PhysicalPrintDelivery(
            id: Guid.NewGuid(),
            printJobId: printJobId,
            ticketId: ticketId,
            printerId: printerId,
            status: PhysicalPrintDeliveryStatus.InFlight,
            attemptNumber: attemptNumber,
            isReprint: false,
            operatorId: null,
            operatorReason: null,
            crashWindowReason: null,
            payloadSnapshot: payload,
            reprintPayload: null,
            createdAt: now,
            deliveredAt: null,
            resolvedAt: null,
            rowVersion: 1);
    }

    public PhysicalPrintDelivery MarkPrinted(DateTimeOffset now)
    {
        if (Status != PhysicalPrintDeliveryStatus.InFlight)
        {
            throw new InvalidPhysicalPrintTransitionException(
                $"Cannot transition to Printed from {Status}. Delivery must be InFlight.");
        }

        return new PhysicalPrintDelivery(
            Id,
            PrintJobId,
            TicketId,
            PrinterId,
            status: PhysicalPrintDeliveryStatus.Printed,
            attemptNumber: AttemptNumber,
            isReprint: IsReprint,
            operatorId: OperatorId,
            operatorReason: OperatorReason,
            crashWindowReason: CrashWindowReason,
            payloadSnapshot: PayloadSnapshot,
            reprintPayload: ReprintPayload,
            createdAt: CreatedAt,
            deliveredAt: now,
            resolvedAt: now,
            rowVersion: RowVersion);
    }

    public PhysicalPrintDelivery MarkUnknown(string crashReason, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(crashReason))
            throw new ArgumentException("Crash reason cannot be empty.", nameof(crashReason));

        if (Status != PhysicalPrintDeliveryStatus.InFlight)
        {
            throw new InvalidPhysicalPrintTransitionException(
                $"Cannot transition to Unknown from {Status}. Delivery must be InFlight.");
        }

        return new PhysicalPrintDelivery(
            Id,
            PrintJobId,
            TicketId,
            PrinterId,
            status: PhysicalPrintDeliveryStatus.Unknown,
            attemptNumber: AttemptNumber,
            isReprint: IsReprint,
            operatorId: null,
            operatorReason: null,
            crashWindowReason: crashReason,
            payloadSnapshot: PayloadSnapshot,
            reprintPayload: null,
            createdAt: CreatedAt,
            deliveredAt: null,
            resolvedAt: null,
            rowVersion: RowVersion);
    }

    public PhysicalPrintDelivery ApproveReprint(string operatorId, string reason, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(operatorId))
            throw new ArgumentException("Operator ID cannot be empty.", nameof(operatorId));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Operator reason cannot be empty.", nameof(reason));

        if (Status != PhysicalPrintDeliveryStatus.Unknown)
        {
            throw new InvalidPhysicalPrintTransitionException(
                $"Reprint can only be approved when status is Unknown. Current status={Status}.");
        }

        var reprintBannerPayload = ReprintTicketBannerFormatter.WrapWithReprintBanner(
            PayloadSnapshot, operatorId, reason, now);

        return new PhysicalPrintDelivery(
            Id,
            PrintJobId,
            TicketId,
            PrinterId,
            status: PhysicalPrintDeliveryStatus.ReprintApproved,
            attemptNumber: AttemptNumber,
            isReprint: true,
            operatorId: operatorId,
            operatorReason: reason,
            crashWindowReason: CrashWindowReason,
            payloadSnapshot: PayloadSnapshot,
            reprintPayload: reprintBannerPayload,
            createdAt: CreatedAt,
            deliveredAt: null,
            resolvedAt: now,
            rowVersion: RowVersion);
    }

    public PhysicalPrintDelivery RejectReprint(string operatorId, string reason, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(operatorId))
            throw new ArgumentException("Operator ID cannot be empty.", nameof(operatorId));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Operator reason cannot be empty.", nameof(reason));

        if (Status != PhysicalPrintDeliveryStatus.Unknown)
        {
            throw new InvalidPhysicalPrintTransitionException(
                $"Reprint can only be rejected when status is Unknown. Current status={Status}.");
        }

        return new PhysicalPrintDelivery(
            Id,
            PrintJobId,
            TicketId,
            PrinterId,
            status: PhysicalPrintDeliveryStatus.ReprintRejected,
            attemptNumber: AttemptNumber,
            isReprint: false,
            operatorId: operatorId,
            operatorReason: reason,
            crashWindowReason: CrashWindowReason,
            payloadSnapshot: PayloadSnapshot,
            reprintPayload: null,
            createdAt: CreatedAt,
            deliveredAt: null,
            resolvedAt: now,
            rowVersion: RowVersion);
    }

    public PhysicalPrintDelivery MarkReprinted(DateTimeOffset now)
    {
        if (Status != PhysicalPrintDeliveryStatus.ReprintApproved)
        {
            throw new UnauthorizedReprintException(
                $"Cannot execute reprint from status '{Status}'. Operator must approve reprint first.");
        }

        return new PhysicalPrintDelivery(
            Id,
            PrintJobId,
            TicketId,
            PrinterId,
            status: PhysicalPrintDeliveryStatus.Reprinted,
            attemptNumber: AttemptNumber,
            isReprint: true,
            operatorId: OperatorId,
            operatorReason: OperatorReason,
            crashWindowReason: CrashWindowReason,
            payloadSnapshot: PayloadSnapshot,
            reprintPayload: ReprintPayload,
            createdAt: CreatedAt,
            deliveredAt: now,
            resolvedAt: now,
            rowVersion: RowVersion);
    }
}
