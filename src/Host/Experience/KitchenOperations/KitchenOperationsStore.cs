using ALKAROS.Audit.EventStore;
using ALKAROS.Kitchen.PhysicalPrintRecovery;
using ALKAROS.Kitchen.PrintQueue;
using ALKAROS.Kitchen.Routing;
using ALKAROS.Kitchen.TicketLifecycle;
using ALKAROS.Operations.BackupHealth;

namespace ALKAROS.Host.Experience.KitchenOperations;

public sealed class KitchenOperationsStore
{
    private readonly IKitchenTicketRepository _tickets;
    private readonly IPrinterRepository _printers;
    private readonly IPrinterRouteRepository _routes;
    private readonly IPrintQueueRepository _printJobs;
    private readonly IPhysicalPrintRecoveryRepository _deliveries;
    private readonly IBackupHealthService _backupHealth;
    private readonly IAuditEventStore _audit;

    public KitchenOperationsStore(
        IKitchenTicketRepository tickets,
        IPrinterRepository printers,
        IPrinterRouteRepository routes,
        IPrintQueueRepository printJobs,
        IPhysicalPrintRecoveryRepository deliveries,
        IBackupHealthService backupHealth,
        IAuditEventStore audit)
    {
        _tickets = tickets ?? throw new ArgumentNullException(nameof(tickets));
        _printers = printers ?? throw new ArgumentNullException(nameof(printers));
        _routes = routes ?? throw new ArgumentNullException(nameof(routes));
        _printJobs = printJobs ?? throw new ArgumentNullException(nameof(printJobs));
        _deliveries = deliveries ?? throw new ArgumentNullException(nameof(deliveries));
        _backupHealth = backupHealth ?? throw new ArgumentNullException(nameof(backupHealth));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public async Task<IReadOnlyList<KitchenTicketV1>> GetActiveTicketsAsync(
        string stationId,
        CancellationToken cancellationToken)
    {
        var normalizedStation = RequireText(stationId, 100, nameof(stationId));
        var tickets = await _tickets.GetActiveByStationAsync(normalizedStation, cancellationToken);
        return tickets.Select(ToDto).ToArray();
    }

    public async Task<KitchenTicketV1> GetTicketAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        EnsureId(ticketId, nameof(ticketId));
        var ticket = await _tickets.GetByIdAsync(ticketId, cancellationToken)
            ?? throw new KitchenOperationsNotFoundException("Kitchen ticket was not found.");
        return ToDto(ticket);
    }

    public async Task<KitchenTicketV1> TransitionTicketAsync(
        Guid ticketId,
        TransitionKitchenTicketV1 request,
        CancellationToken cancellationToken)
    {
        EnsureId(ticketId, nameof(ticketId));
        ArgumentNullException.ThrowIfNull(request);
        EnsureVersion(request.ExpectedRowVersion, nameof(request.ExpectedRowVersion));
        var target = ParseEnum<KitchenTicketState>(request.TargetState, nameof(request.TargetState));
        var ticket = await _tickets.GetByIdAsync(ticketId, cancellationToken)
            ?? throw new KitchenOperationsNotFoundException("Kitchen ticket was not found.");
        EnsureVersion(ticket.RowVersion, request.ExpectedRowVersion, "kitchen ticket");

        if (ticket.Status == target)
        {
            return ToDto(ticket);
        }

        KitchenTicket transitioned;
        try
        {
            transitioned = ticket.TransitionTo(target, NormalizeReason(request.Reason));
            await _tickets.SaveAsync(transitioned, request.ExpectedRowVersion, cancellationToken);
        }
        catch (InvalidKitchenTransitionException exception)
        {
            throw new KitchenOperationsConcurrencyException(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            await ThrowConcurrencyOrNotFoundAsync(ticketId, request.ExpectedRowVersion, exception, cancellationToken);
            throw;
        }

        var canonical = await _tickets.GetByIdAsync(ticketId, cancellationToken)
            ?? throw new KitchenOperationsNotFoundException("Kitchen ticket was not found after transition.");
        return ToDto(canonical);
    }

    public async Task<KitchenTicketV1> TransitionItemAsync(
        Guid ticketId,
        Guid itemId,
        TransitionKitchenItemV1 request,
        CancellationToken cancellationToken)
    {
        EnsureId(ticketId, nameof(ticketId));
        EnsureId(itemId, nameof(itemId));
        ArgumentNullException.ThrowIfNull(request);
        EnsureVersion(request.ExpectedTicketRowVersion, nameof(request.ExpectedTicketRowVersion));
        EnsureVersion(request.ExpectedItemRowVersion, nameof(request.ExpectedItemRowVersion));
        var target = ParseEnum<KitchenTicketItemState>(request.TargetState, nameof(request.TargetState));
        var ticket = await _tickets.GetByIdAsync(ticketId, cancellationToken)
            ?? throw new KitchenOperationsNotFoundException("Kitchen ticket was not found.");
        EnsureVersion(ticket.RowVersion, request.ExpectedTicketRowVersion, "kitchen ticket");
        var item = ticket.Items.FirstOrDefault(value => value.Id == itemId)
            ?? throw new KitchenOperationsNotFoundException("Kitchen ticket item was not found.");
        EnsureVersion(item.RowVersion, request.ExpectedItemRowVersion, "kitchen ticket item");

        try
        {
            var transitioned = ticket.UpdateItemStatus(itemId, target, NormalizeReason(request.Reason));
            await _tickets.SaveAsync(transitioned, request.ExpectedTicketRowVersion, cancellationToken);
            var canonical = await _tickets.GetByIdAsync(ticketId, cancellationToken)
                ?? throw new KitchenOperationsNotFoundException("Kitchen ticket was not found after transition.");
            return ToDto(canonical);
        }
        catch (InvalidKitchenTransitionException exception)
        {
            throw new KitchenOperationsConcurrencyException(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            await ThrowConcurrencyOrNotFoundAsync(ticketId, request.ExpectedTicketRowVersion, exception, cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<PrinterV1>> GetPrintersAsync(CancellationToken cancellationToken)
        => (await _printers.GetAllAsync(cancellationToken)).Select(ToDto).ToArray();

    public async Task<PrinterV1> UpdatePrinterAsync(
        Guid printerId,
        UpdatePrinterV1 request,
        CancellationToken cancellationToken)
    {
        EnsureId(printerId, nameof(printerId));
        ArgumentNullException.ThrowIfNull(request);
        var current = await _printers.GetByIdAsync(printerId, cancellationToken)
            ?? throw new KitchenOperationsNotFoundException("Printer was not found.");
        var updated = new Printer(
            printerId,
            RequireText(request.Name, 200, nameof(request.Name)),
            RequireText(request.StationId, 100, nameof(request.StationId)),
            current.IpAddress,
            current.Port,
            request.IsActive,
            current.CreatedAt,
            DateTimeOffset.UtcNow);
        await _printers.SaveAsync(updated, cancellationToken);
        return ToDto(updated);
    }

    public async Task<IReadOnlyList<PrinterRouteV1>> GetRoutesAsync(CancellationToken cancellationToken)
        => (await _routes.GetAllAsync(cancellationToken)).Select(ToDto).ToArray();

    public async Task<PrinterRouteV1> UpdateRouteAsync(
        Guid routeId,
        UpdatePrinterRouteV1 request,
        CancellationToken cancellationToken)
    {
        EnsureId(routeId, nameof(routeId));
        ArgumentNullException.ThrowIfNull(request);
        var current = await _routes.GetByIdAsync(routeId, cancellationToken)
            ?? throw new KitchenOperationsNotFoundException("Printer route was not found.");
        var level = ParseEnum<RouteLevel>(request.RouteLevel, nameof(request.RouteLevel));
        var updated = new PrinterRoute(
            routeId,
            level,
            request.PrinterId,
            request.ItemId,
            request.ProductId,
            request.CategoryId,
            request.SpecialDate,
            request.IsActive,
            current.CreatedAt,
            DateTimeOffset.UtcNow);
        await _routes.SaveRouteAsync(updated, cancellationToken);
        return ToDto(updated);
    }

    public async Task<IReadOnlyList<PrintJobV1>> GetPrintJobsAsync(
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        EnsureId(ticketId, nameof(ticketId));
        return (await _printJobs.GetByTicketIdAsync(ticketId, cancellationToken)).Select(ToDto).ToArray();
    }

    public async Task<PrintJobV1> GetPrintJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        EnsureId(jobId, nameof(jobId));
        var job = await _printJobs.GetByIdAsync(jobId, cancellationToken)
            ?? throw new KitchenOperationsNotFoundException("Print job was not found.");
        return ToDto(job);
    }

    public async Task<IReadOnlyList<PhysicalPrintDeliveryV1>> GetUnknownDeliveriesAsync(CancellationToken cancellationToken)
        => (await _deliveries.GetPendingUnknownDeliveriesAsync(cancellationToken)).Select(ToDto).ToArray();

    public async Task<PhysicalPrintDeliveryV1> GetDeliveryAsync(Guid deliveryId, CancellationToken cancellationToken)
    {
        EnsureId(deliveryId, nameof(deliveryId));
        var delivery = await _deliveries.GetByIdAsync(deliveryId, cancellationToken)
            ?? throw new KitchenOperationsNotFoundException("Physical print delivery was not found.");
        return ToDto(delivery);
    }

    public async Task<PhysicalPrintDeliveryV1> ApproveReprintAsync(
        Guid deliveryId,
        ReprintDecisionV1 request,
        Guid operatorId,
        CancellationToken cancellationToken)
    {
        EnsureId(deliveryId, nameof(deliveryId));
        EnsureId(operatorId, nameof(operatorId));
        ArgumentNullException.ThrowIfNull(request);
        var reason = RequireText(request.Reason, 500, nameof(request.Reason));
        var delivery = await _deliveries.GetByIdAsync(deliveryId, cancellationToken)
            ?? throw new KitchenOperationsNotFoundException("Physical print delivery was not found.");
        var approved = delivery.ApproveReprint(operatorId.ToString("D"), reason, DateTimeOffset.UtcNow);
        await _deliveries.SaveAsync(approved, cancellationToken);
        return ToDto(approved);
    }

    public async Task<PhysicalPrintDeliveryV1> RejectReprintAsync(
        Guid deliveryId,
        ReprintDecisionV1 request,
        Guid operatorId,
        CancellationToken cancellationToken)
    {
        EnsureId(deliveryId, nameof(deliveryId));
        EnsureId(operatorId, nameof(operatorId));
        ArgumentNullException.ThrowIfNull(request);
        var reason = RequireText(request.Reason, 500, nameof(request.Reason));
        var delivery = await _deliveries.GetByIdAsync(deliveryId, cancellationToken)
            ?? throw new KitchenOperationsNotFoundException("Physical print delivery was not found.");
        var rejected = delivery.RejectReprint(operatorId.ToString("D"), reason, DateTimeOffset.UtcNow);
        await _deliveries.SaveAsync(rejected, cancellationToken);
        return ToDto(rejected);
    }

    public async Task<HealthSnapshotV1?> GetLatestHealthAsync(CancellationToken cancellationToken)
        => (await _backupHealth.GetLatestHealthSnapshotAsync(cancellationToken)) is { } value ? ToDto(value) : null;

    public async Task<IReadOnlyList<BackupV1>> GetRecentBackupsAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 100.");
        return (await _backupHealth.GetRecentBackupsAsync(limit, cancellationToken)).Select(ToDto).ToArray();
    }

    public async Task<BackupV1> ExecuteBackupAsync(
        StartBackupV1 request,
        ProductionBackupOptions options,
        IProductionBackupPayloadProvider? payloadProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);
        var backupType = ParseEnum<BackupType>(request.BackupType, nameof(request.BackupType));
        if (request.RetentionDays is < 1 or > 3650)
            throw new ArgumentOutOfRangeException(nameof(request), "RetentionDays must be between 1 and 3650.");
        if (payloadProvider is null)
            throw new BackupProviderUnavailableException();
        var destination = string.IsNullOrWhiteSpace(options.BackupDirectory)
            ? throw new BackupProviderUnavailableException()
            : RequireText(options.BackupDirectory, 500, nameof(options.BackupDirectory));

        var payload = await payloadProvider.CreatePayloadAsync(backupType, cancellationToken);
        if (payload.Length == 0)
            throw new BackupProviderUnavailableException();
        try
        {
            var record = await _backupHealth.ExecuteBackupAsync(
                new StartBackupCommand(backupType, destination, request.RetentionDays),
                payload,
                cancellationToken);
            return ToDto(record);
        }
        catch (BackupExecutionException)
        {
            throw;
        }
    }

    public async Task<IReadOnlyList<AuditEventV1>> GetAuditByAggregateAsync(
        string aggregateType,
        Guid aggregateId,
        CancellationToken cancellationToken)
    {
        var normalizedType = RequireText(aggregateType, 100, nameof(aggregateType));
        EnsureId(aggregateId, nameof(aggregateId));
        return (await _audit.GetByAggregateAsync(normalizedType, aggregateId, cancellationToken)).Select(ToDto).ToArray();
    }

    public async Task<IReadOnlyList<AuditEventV1>> GetAuditByCorrelationAsync(
        string correlationId,
        CancellationToken cancellationToken)
    {
        var normalized = RequireText(correlationId, 200, nameof(correlationId));
        return (await _audit.GetByCorrelationIdAsync(normalized, cancellationToken)).Select(ToDto).ToArray();
    }

    private async Task ThrowConcurrencyOrNotFoundAsync(
        Guid ticketId,
        long expected,
        InvalidOperationException original,
        CancellationToken cancellationToken)
    {
        var actual = await _tickets.GetByIdAsync(ticketId, cancellationToken);
        if (actual is null)
            throw new KitchenOperationsNotFoundException("Kitchen ticket was not found.");
        throw new KitchenOperationsConcurrencyException(
            $"Kitchen ticket row version {actual.RowVersion} does not match expected version {expected}.");
    }

    private static KitchenTicketV1 ToDto(KitchenTicket value)
        => new(
            value.Id,
            value.OrderId,
            value.TicketNumber,
            value.StationId,
            value.Status.ToString(),
            value.RowVersion,
            value.CreatedAt,
            value.UpdatedAt,
            value.AcceptedAt,
            value.ReadyAt,
            value.CancelledAt,
            value.TargetPrepMinutes,
            value.Items.Select(ToDto).ToArray());

    private static KitchenTicketItemV1 ToDto(KitchenTicketItem value)
        => new(
            value.Id,
            value.OrderItemId,
            value.ProductId,
            value.ProductNameSnapshot,
            value.Quantity,
            value.ModifiersSummary,
            value.Notes,
            value.Status.ToString(),
            value.RowVersion,
            value.CreatedAt,
            value.UpdatedAt,
            value.ReadyAt,
            value.ServedAt,
            value.CancelledAt);

    private static PrinterV1 ToDto(Printer value)
        => new(value.Id, value.Name, value.StationId, value.IsActive, value.CreatedAt, value.UpdatedAt);

    private static PrinterRouteV1 ToDto(PrinterRoute value)
        => new(value.Id, value.RouteLevel.ToString(), value.PrinterId, value.ItemId, value.ProductId,
            value.CategoryId, value.SpecialDate, value.IsActive, value.CreatedAt, value.UpdatedAt);

    private static PrintJobV1 ToDto(PrintJob value)
        => new(value.Id, value.TicketId, value.PrinterId, value.Status.ToString(), value.AttemptCount,
            value.MaxAttempts, value.NextAttemptAt, value.LeaseExpiresAt, value.PrintedAt, value.FailedAt,
            value.CreatedAt, value.UpdatedAt);

    private static PhysicalPrintDeliveryV1 ToDto(PhysicalPrintDelivery value)
        => new(value.Id, value.PrintJobId, value.TicketId, value.PrinterId, value.Status.ToString(),
            value.AttemptNumber, value.IsReprint, value.OperatorReason, value.CrashWindowReason,
            value.CreatedAt, value.DeliveredAt, value.ResolvedAt, value.RowVersion);

    private static BackupV1 ToDto(BackupRecord value)
        => new(value.BackupId, value.BackupType.ToString(), value.FileSizeBytes, value.Status.ToString(),
            value.ErrorMessage, value.StartedAt, value.CompletedAt, value.RetentionDays);

    private static HealthSnapshotV1 ToDto(SystemHealthSnapshotRecord value)
        => new(value.SnapshotId, value.DatabaseStatus.ToString(), value.DiskStatus.ToString(),
            value.LastBackupStatus.ToString(), value.FreeDiskBytes, value.DatabaseSizeBytes, value.CapturedAt);

    private static AuditEventV1 ToDto(AuditEvent value)
        => new(value.Id, value.EventName, value.AggregateType, value.AggregateId, value.ActorType,
            value.Reason, value.CorrelationId, value.OccurredAt);

    private static T ParseEnum<T>(string? value, string parameterName) where T : struct, Enum
    {
        if (!Enum.TryParse<T>(value, true, out var parsed) || !Enum.IsDefined(parsed))
            throw new ArgumentException($"{parameterName} is not a supported value.", parameterName);
        return parsed;
    }

    private static string RequireText(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
            throw new ArgumentException($"{parameterName} is too long.", parameterName);
        return normalized;
    }

    private static string? NormalizeReason(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : RequireText(value, 500, nameof(value));

    private static void EnsureId(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
            throw new ArgumentException($"{parameterName} cannot be empty.", parameterName);
    }

    private static void EnsureVersion(long value, string parameterName)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(parameterName, "Row version must be positive.");
    }

    private static void EnsureVersion(long actual, long expected, string resource)
    {
        if (actual != expected)
            throw new KitchenOperationsConcurrencyException($"The {resource} was changed by another operation.");
    }
}

public sealed class BackupProviderUnavailableException : Exception
{
    public BackupProviderUnavailableException()
        : base("A verified production backup payload provider is not configured.") { }
}
