using ALKAROS.Payments.PaymentAggregate;
using Npgsql;

namespace ALKAROS.Payments.ManualResolution;

/// <summary>
/// V1-RMD-264. Takes the shared <c>"bill-settlement:{billId:N}"</c> advisory
/// lock (the same string convention CardSettlementOrchestrator, EftTenderHandler
/// and PaymentAwareTableTopologyPolicy use) before re-reading the payment, so
/// a resolution cannot interleave with a concurrent settlement or table move
/// on the same bill.
/// </summary>
public sealed class ManualPaymentResolutionService : IManualPaymentResolutionService
{
    public const int MaxReasonLength = 500;

    private readonly IPaymentRepository _paymentRepository;
    private readonly NpgsqlDataSource _dataSource;

    public ManualPaymentResolutionService(IPaymentRepository paymentRepository, NpgsqlDataSource dataSource)
    {
        _paymentRepository = paymentRepository ?? throw new ArgumentNullException(nameof(paymentRepository));
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<ManualPaymentResolutionResult> MarkNotChargedAsync(
        Guid paymentId, Guid actorUserId, string reason, CancellationToken cancellationToken = default)
    {
        var trimmed = reason?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ManualResolutionReasonInvalidException("A reason is required.");
        if (trimmed.Length > MaxReasonLength)
            throw new ManualResolutionReasonInvalidException($"The reason cannot exceed {MaxReasonLength} characters.");

        var initial = await _paymentRepository.GetByIdAsync(paymentId, cancellationToken)
            ?? throw new ManualResolutionPaymentNotFoundException(paymentId);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var lockCommand = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtext($1)::bigint);", connection, transaction))
        {
            lockCommand.Parameters.AddWithValue($"bill-settlement:{initial.BillId:N}");
            await lockCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        // Re-read under the lock: the state seen before it may already be stale.
        var payment = await _paymentRepository.GetByIdAsync(paymentId, cancellationToken)
            ?? throw new ManualResolutionPaymentNotFoundException(paymentId);
        if (payment.Status is not (PaymentStatus.Unknown or PaymentStatus.ReconciliationRequired))
            throw new ManualResolutionNotResolvableException(paymentId, payment.Status.ToString());

        var previous = payment.Status.ToString();
        var note = $"Manuel çözüm: kart çekilmedi. {trimmed}";
        var expectedRowVersion = payment.RowVersion;
        var resolved = payment;
        if (resolved.Status == PaymentStatus.Unknown)
            resolved = resolved.RequestReconciliation(note, actorUserId);
        resolved = resolved.Decline(note, actorUserId);

        await _paymentRepository.SaveAsync(resolved, expectedRowVersion, connection, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ManualPaymentResolutionResult(payment.Id, payment.BillId, previous, resolved.Status.ToString());
    }
}
