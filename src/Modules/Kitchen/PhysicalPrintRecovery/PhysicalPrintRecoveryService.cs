namespace ALKAROS.Kitchen.PhysicalPrintRecovery;

/// <summary>
/// Service interface for orchestrating crash-window uncertain delivery management,
/// preventing automatic duplicate prints, and executing operator-approved reprints.
/// </summary>
public interface IPhysicalPrintRecoveryService
{
    Task<PhysicalPrintDelivery> StartInFlightDeliveryAsync(
        Guid printJobId,
        Guid ticketId,
        Guid printerId,
        string payload,
        int attemptNumber = 1,
        CancellationToken ct = default);

    Task<PhysicalPrintDelivery> ConfirmDeliverySuccessAsync(
        Guid deliveryId,
        CancellationToken ct = default);

    Task<PhysicalPrintDelivery> ReportCrashWindowUncertaintyAsync(
        Guid deliveryId,
        string crashReason,
        CancellationToken ct = default);

    Task<PhysicalPrintDelivery> ApproveOperatorReprintAsync(
        Guid deliveryId,
        string operatorId,
        string reason,
        CancellationToken ct = default);

    Task<PhysicalPrintDelivery> RejectOperatorReprintAsync(
        Guid deliveryId,
        string operatorId,
        string reason,
        CancellationToken ct = default);

    Task<PhysicalPrintDelivery> ExecuteApprovedReprintAsync(
        Guid deliveryId,
        Func<string, Task<bool>> physicalPrinterTransport,
        CancellationToken ct = default);

    Task<IReadOnlyList<PhysicalPrintDelivery>> GetPendingUnknownDeliveriesAsync(
        CancellationToken ct = default);
}

/// <summary>
/// Implementation of crash-window safeguards and operator-controlled reprints (V1-KIT-004).
/// </summary>
public sealed class PhysicalPrintRecoveryService : IPhysicalPrintRecoveryService
{
    private readonly IPhysicalPrintRecoveryRepository _repository;

    public PhysicalPrintRecoveryService(IPhysicalPrintRecoveryRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<PhysicalPrintDelivery> StartInFlightDeliveryAsync(
        Guid printJobId,
        Guid ticketId,
        Guid printerId,
        string payload,
        int attemptNumber = 1,
        CancellationToken ct = default)
    {
        var delivery = PhysicalPrintDelivery.CreateInFlight(printJobId, ticketId, printerId, payload, attemptNumber);
        await _repository.AddAsync(delivery, ct).ConfigureAwait(false);
        return delivery;
    }

    public async Task<PhysicalPrintDelivery> ConfirmDeliverySuccessAsync(
        Guid deliveryId,
        CancellationToken ct = default)
    {
        var delivery = await _repository.GetByIdAsync(deliveryId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"PhysicalPrintDelivery '{deliveryId}' was not found.");

        var printed = delivery.MarkPrinted(DateTimeOffset.UtcNow);
        await _repository.SaveAsync(printed, ct).ConfigureAwait(false);
        return printed;
    }

    public async Task<PhysicalPrintDelivery> ReportCrashWindowUncertaintyAsync(
        Guid deliveryId,
        string crashReason,
        CancellationToken ct = default)
    {
        var delivery = await _repository.GetByIdAsync(deliveryId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"PhysicalPrintDelivery '{deliveryId}' was not found.");

        var unknown = delivery.MarkUnknown(crashReason, DateTimeOffset.UtcNow);
        await _repository.SaveAsync(unknown, ct).ConfigureAwait(false);
        return unknown;
    }

    public async Task<PhysicalPrintDelivery> ApproveOperatorReprintAsync(
        Guid deliveryId,
        string operatorId,
        string reason,
        CancellationToken ct = default)
    {
        var delivery = await _repository.GetByIdAsync(deliveryId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"PhysicalPrintDelivery '{deliveryId}' was not found.");

        var approved = delivery.ApproveReprint(operatorId, reason, DateTimeOffset.UtcNow);
        await _repository.SaveAsync(approved, ct).ConfigureAwait(false);
        return approved;
    }

    public async Task<PhysicalPrintDelivery> RejectOperatorReprintAsync(
        Guid deliveryId,
        string operatorId,
        string reason,
        CancellationToken ct = default)
    {
        var delivery = await _repository.GetByIdAsync(deliveryId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"PhysicalPrintDelivery '{deliveryId}' was not found.");

        var rejected = delivery.RejectReprint(operatorId, reason, DateTimeOffset.UtcNow);
        await _repository.SaveAsync(rejected, ct).ConfigureAwait(false);
        return rejected;
    }

    public async Task<PhysicalPrintDelivery> ExecuteApprovedReprintAsync(
        Guid deliveryId,
        Func<string, Task<bool>> physicalPrinterTransport,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(physicalPrinterTransport);

        var delivery = await _repository.GetByIdAsync(deliveryId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"PhysicalPrintDelivery '{deliveryId}' was not found.");

        if (delivery.Status != PhysicalPrintDeliveryStatus.ReprintApproved)
        {
            if (delivery.Status == PhysicalPrintDeliveryStatus.ReprintInFlight)
            {
                throw new PhysicalPrintDeliveryConcurrencyException(
                    $"Reprint for delivery '{deliveryId}' is already claimed by another worker.");
            }

            throw new UnauthorizedReprintException(
                $"Cannot execute reprint on delivery '{deliveryId}' with status '{delivery.Status}'. Must be ReprintApproved.");
        }

        var payloadToPrint = delivery.ReprintPayload
            ?? throw new InvalidOperationException("Reprint payload is missing from approved delivery.");

        // Fence the external side effect before invoking the printer. Only one
        // worker can transition the row from ReprintApproved to ReprintInFlight.
        var claimed = delivery.BeginApprovedReprint(DateTimeOffset.UtcNow);
        await _repository.SaveAsync(claimed, ct).ConfigureAwait(false);

        try
        {
            var success = await physicalPrinterTransport(payloadToPrint).ConfigureAwait(false);
            if (!success)
                throw new InvalidOperationException("Physical printer transport rejected reprint transmission.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var uncertain = claimed.MarkReprintUnknown(ex.Message, DateTimeOffset.UtcNow);
            await _repository.SaveAsync(uncertain, ct).ConfigureAwait(false);
            throw;
        }

        var reprinted = claimed.MarkReprinted(DateTimeOffset.UtcNow);
        await _repository.SaveAsync(reprinted, ct).ConfigureAwait(false);
        return reprinted;
    }

    public Task<IReadOnlyList<PhysicalPrintDelivery>> GetPendingUnknownDeliveriesAsync(CancellationToken ct = default)
    {
        return _repository.GetPendingUnknownDeliveriesAsync(ct);
    }
}
