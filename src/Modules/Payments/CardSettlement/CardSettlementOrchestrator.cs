using System.Text.Json;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Messaging;
using ALKAROS.Payments.Allocations.Persistence;
using ALKAROS.Payments.PaymentAggregate;
using ALKAROS.Payments.TenderRouting;
using Npgsql;

namespace ALKAROS.Payments.CardSettlement;

/// <summary>
/// Real implementation of <see cref="ICardSettlementOrchestrator"/>
/// (V13-PAY-004). One atomic transaction per attempt, guarded by an
/// advisory lock on the idempotency key — same idiom as
/// <c>ALKAROS.Cash.TenderHandler.CashTenderHandler</c> (V13-CSH-003): check
/// for an existing attempt under the lock before doing any work, so a
/// crash-and-resume or a genuine duplicate call replays instead of
/// re-processing, and a real mismatch (a different provider correlation id
/// or a different outcome under the same idempotency key) is rejected
/// rather than silently overwritten.
/// </summary>
public sealed class CardSettlementOrchestrator : ICardSettlementOrchestrator
{
    private readonly IBillRepository _billRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPaymentAllocationRepository _allocationRepository;
    private readonly ICardSettlementAttemptRepository _attemptRepository;
    private readonly NpgsqlDataSource _dataSource;

    public CardSettlementOrchestrator(
        IBillRepository billRepository,
        IPaymentRepository paymentRepository,
        IPaymentAllocationRepository allocationRepository,
        ICardSettlementAttemptRepository attemptRepository,
        NpgsqlDataSource dataSource)
    {
        _billRepository = billRepository ?? throw new ArgumentNullException(nameof(billRepository));
        _paymentRepository = paymentRepository ?? throw new ArgumentNullException(nameof(paymentRepository));
        _allocationRepository = allocationRepository ?? throw new ArgumentNullException(nameof(allocationRepository));
        _attemptRepository = attemptRepository ?? throw new ArgumentNullException(nameof(attemptRepository));
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<CardSettlementResult> HandleAsync(
        CardSettlementRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        var bill = await _billRepository.GetByIdAsync(request.BillId, cancellationToken)
            ?? throw new CardSettlementBillNotFoundException(request.BillId);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // V1-RMD-258: bill-scoped lock FIRST, idempotency-key lock second —
        // the same fixed order PaymentAwareTableTopologyPolicy (Tables
        // module) uses on the SAME namespaced key, so a concurrent table
        // transfer/merge/unmerge attempt and a concurrent settlement attempt
        // against the same bill genuinely serialize against each other
        // rather than racing (no shared C# type needed — Tables has no
        // compile-time reference to Payments, by design — only the shared
        // "bill-settlement:{billId:N}" string convention).
        await LockBillForSettlementAsync(connection, transaction, request.BillId, cancellationToken);
        await LockIdempotencyKeyAsync(connection, transaction, request.IdempotencyKey, cancellationToken);

        var existing = await _attemptRepository.GetByIdempotencyKeyAsync(
            request.IdempotencyKey, connection, transaction, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, request, bill.Id);
            await transaction.CommitAsync(cancellationToken);
            return new CardSettlementResult(
                existing.PaymentId, existing.Outcome, existing.AllocationId, existing.ApprovedAmount, WasReplayed: true);
        }

        // V1-RMD-258: a genuinely NEW attempt (no prior row under this exact
        // idempotency key) must never be layered on top of a Bill that
        // already has an unresolved Payment — held under the same
        // bill-settlement lock just acquired above, so this read is
        // race-free against any concurrent settlement attempt for the same
        // bill.
        var existingPayments = await _paymentRepository.GetByBillIdAsync(bill.Id, cancellationToken);
        var unsettled = existingPayments.FirstOrDefault(p =>
            p.Status is PaymentStatus.Pending or PaymentStatus.Unknown or PaymentStatus.ReconciliationRequired);
        if (unsettled is not null)
            throw new CardSettlementUnsettledPaymentExistsException(bill.Id, unsettled.Id, unsettled.Status.ToString());

        var attemptId = Guid.NewGuid();
        var payment = new Payment(Guid.NewGuid(), bill.Id, request.AttemptedAmount);
        CardSettlementAttempt attempt;

        switch (request.Result)
        {
            case TenderApproved approved:
                payment = payment.Tender(approved.ApprovedAmount).Approve(approved.ApprovedAmount);
                await _paymentRepository.AddAsync(payment, connection, transaction, cancellationToken);

                var allocation = await _allocationRepository.AllocateAsync(
                    payment, bill, approved.ApprovedAmount, request.IdempotencyKey, connection, transaction, cancellationToken);

                await OutboxStore.EnqueueAsync(
                    BuildFiscalHandoffEnvelope(attemptId, payment, approved.ApprovedAmount),
                    connection, transaction, cancellationToken);

                attempt = new CardSettlementAttempt(
                    attemptId, bill.Id, request.IdempotencyKey, request.ProviderCorrelationId, payment.Id,
                    CardSettlementOutcome.Approved, approved.ApprovedAmount, allocation.Id,
                    reason: null, fiscalHandoffQueued: true);
                break;

            case TenderDeclined declined:
                payment = payment.Tender(request.AttemptedAmount).Decline(declined.Reason);
                await _paymentRepository.AddAsync(payment, connection, transaction, cancellationToken);

                attempt = new CardSettlementAttempt(
                    attemptId, bill.Id, request.IdempotencyKey, request.ProviderCorrelationId, payment.Id,
                    CardSettlementOutcome.Declined, approvedAmount: null, allocationId: null,
                    reason: declined.Reason, fiscalHandoffQueued: false);
                break;

            case TenderRequiresReconciliation requiresReconciliation:
                // Stops at Unknown (never RequestReconciliation/Approve/Decline
                // from here) — V13-REC-001's future job is to open the
                // ReconciliationCase; this row is the typed evidence it will
                // consume (V13-PAY-004 Acceptance evidence).
                payment = payment.Tender(request.AttemptedAmount).MarkUnknown(requiresReconciliation.Reason);
                await _paymentRepository.AddAsync(payment, connection, transaction, cancellationToken);

                attempt = new CardSettlementAttempt(
                    attemptId, bill.Id, request.IdempotencyKey, request.ProviderCorrelationId, payment.Id,
                    CardSettlementOutcome.RequiresReconciliation, approvedAmount: null, allocationId: null,
                    reason: requiresReconciliation.Reason, fiscalHandoffQueued: false);
                break;

            default:
                throw new NotSupportedException(
                    $"Unsupported tender handler result type '{request.Result.GetType().Name}'.");
        }

        await _attemptRepository.InsertAsync(attempt, connection, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CardSettlementResult(
            payment.Id, attempt.Outcome, attempt.AllocationId, attempt.ApprovedAmount, WasReplayed: false);
    }

    /// <summary>
    /// A replay must reproduce the exact recorded attempt. Any divergence —
    /// a different terminal reference, a different outcome, or a different
    /// approved amount under the same Approved outcome — is a genuine
    /// mismatch (V13-PAY-004 Acceptance evidence: "allocation/provider
    /// mismatch"), never a silent overwrite.
    /// </summary>
    private static void EnsureReplayMatches(CardSettlementAttempt existing, CardSettlementRequest request, Guid requestedBillId)
    {
        // V1-RMD-258: checked first — a cross-bill idempotency-key collision
        // is a more fundamental mismatch than any field-level divergence
        // below, and must never be masked by a field check happening to
        // also fail (or, worse, happening to also pass).
        if (existing.BillId != requestedBillId)
            throw new CardSettlementBillMismatchException(request.IdempotencyKey, existing.BillId, requestedBillId);

        if (!string.Equals(existing.ProviderCorrelationId, request.ProviderCorrelationId, StringComparison.Ordinal))
            throw new CardSettlementReplayMismatchException(
                request.IdempotencyKey,
                $"recorded provider correlation id '{existing.ProviderCorrelationId}' does not match " +
                $"'{request.ProviderCorrelationId}'.");

        var incomingOutcome = request.Result switch
        {
            TenderApproved => CardSettlementOutcome.Approved,
            TenderDeclined => CardSettlementOutcome.Declined,
            TenderRequiresReconciliation => CardSettlementOutcome.RequiresReconciliation,
            _ => throw new NotSupportedException(
                $"Unsupported tender handler result type '{request.Result.GetType().Name}'."),
        };
        if (existing.Outcome != incomingOutcome)
            throw new CardSettlementReplayMismatchException(
                request.IdempotencyKey,
                $"recorded outcome '{existing.Outcome}' does not match incoming outcome '{incomingOutcome}'.");

        if (request.Result is TenderApproved approved && existing.ApprovedAmount != approved.ApprovedAmount)
            throw new CardSettlementReplayMismatchException(
                request.IdempotencyKey,
                $"recorded approved amount '{existing.ApprovedAmount}' does not match " +
                $"'{approved.ApprovedAmount}'.");
    }

    private static OutboxEnvelope BuildFiscalHandoffEnvelope(Guid attemptId, Payment payment, decimal approvedAmount)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            paymentId = payment.Id,
            billId = payment.BillId,
            approvedAmount,
            currencyCode = payment.CurrencyCode,
            approvedAt = payment.ApprovedAt,
        });
        return new OutboxEnvelope("card-settlement.approved", "CardSettlementAttempt", attemptId, payload);
    }

    private static async Task LockIdempotencyKeyAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string idempotencyKey, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtext($1)::bigint);", connection, transaction);
        command.Parameters.AddWithValue($"card-settlement:{idempotencyKey}");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// V1-RMD-258: the shared bill-settlement advisory lock — same
    /// namespaced key (<c>"bill-settlement:{billId:N}"</c>) that
    /// <c>ALKAROS.Tables.PaymentTopology.PaymentAwareTableTopologyPolicy</c>
    /// and <c>ALKAROS.Payments.EftTender.EftTenderHandler</c> take, so a
    /// concurrent table transfer/merge/unmerge or EFT tender against the
    /// same bill genuinely serializes against this settlement attempt.
    /// </summary>
    private static async Task LockBillForSettlementAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid billId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtext($1)::bigint);", connection, transaction);
        command.Parameters.AddWithValue($"bill-settlement:{billId:N}");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
