using ALKAROS.Billing.BillFoundation;
using ALKAROS.Payments.Allocations.Persistence;
using ALKAROS.Payments.PaymentAggregate;
using ALKAROS.Payments.TenderRouting;
using Npgsql;

namespace ALKAROS.Payments.EftTender;

/// <summary>
/// EFT/Havale tender handler (V13-PAY-005, PO:2026-09-16): records a
/// cashier's declaration that a bank-transfer amount landed in the
/// business's own account statement, creating a Payment and
/// PaymentAllocation atomically — no provider/API integration exists or is
/// attempted (the cashier's visual confirmation IS the tender).
///
/// Deliberately different from <c>CashTenderHandler</c> (V13-CSH-003) in two
/// load-bearing ways: (1) there is no change amount — the applied amount is
/// always exactly the requested amount, capped at the bill's remaining
/// payable, never more (there is no physical cash surplus to hand back);
/// (2) this handler has ZERO relationship to <c>CashSession</c> — it never
/// looks up, requires, or references one, so it works identically whether a
/// cash drawer session is open, closed, or does not exist at all (this
/// task's own In-scope explicitly requires this, as the opposite of Cash's
/// own Open-session requirement).
///
/// The optional free-text note (reference number is explicitly NOT
/// required — Semih's product decision) is carried through as the
/// <c>Payment</c> status-history entry's own <c>reason</c> field rather than
/// a new column: this task's Owned surface adds no migration, and
/// <see cref="PaymentStatusHistoryEntry"/> already exists exactly for this
/// kind of free-text audit trail.
/// </summary>
public sealed class EftTenderHandler : IEftTenderHandler
{
    private readonly IBillRepository _billRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPaymentAllocationRepository _allocationRepository;
    private readonly NpgsqlDataSource _dataSource;

    public EftTenderHandler(
        IBillRepository billRepository,
        IPaymentRepository paymentRepository,
        IPaymentAllocationRepository allocationRepository,
        NpgsqlDataSource dataSource)
    {
        _billRepository = billRepository ?? throw new ArgumentNullException(nameof(billRepository));
        _paymentRepository = paymentRepository ?? throw new ArgumentNullException(nameof(paymentRepository));
        _allocationRepository = allocationRepository ?? throw new ArgumentNullException(nameof(allocationRepository));
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public TenderMethod Method => TenderMethod.Eft;

    public async Task<TenderHandlerResult> HandleAsync(
        TenderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        if (request.Method != TenderMethod.Eft)
            throw new ArgumentException(
                $"'{nameof(EftTenderHandler)}' only handles '{TenderMethod.Eft}' requests.",
                nameof(request));
        if (request.BillId is null || request.BillId == Guid.Empty)
            throw new ArgumentException("Bill id is required for an EFT tender request.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            throw new ArgumentException("Idempotency key is required for an EFT tender request.", nameof(request));

        var billId = request.BillId.Value;
        var bill = await _billRepository.GetByIdAsync(billId, cancellationToken).ConfigureAwait(false)
            ?? throw new EftTenderBillNotFoundException(billId);

        // No CashSession lookup anywhere in this handler — deliberate (see
        // the class doc comment); EFT works identically whether a drawer
        // session is open, closed, or does not exist.

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var dbTransaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // V1-RMD-258: bill-scoped lock FIRST, idempotency-key lock second —
        // the same fixed order CardSettlementOrchestrator and
        // PaymentAwareTableTopologyPolicy (Tables module) use on the SAME
        // namespaced key, so a concurrent table transfer/merge/unmerge or a
        // concurrent BankCard settlement attempt against the same bill
        // genuinely serializes against this EFT tender rather than racing.
        await LockBillForSettlementAsync(connection, dbTransaction, billId, cancellationToken)
            .ConfigureAwait(false);
        // Same per-idempotency-key advisory lock pattern as CashTenderHandler
        // (own namespaced key so it can never collide with Cash's/another
        // method's lock on the same raw key string).
        await LockIdempotencyKeyAsync(connection, dbTransaction, request.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);

        var existingAllocation = await _allocationRepository.GetByIdempotencyKeyAsync(
            request.IdempotencyKey, connection, dbTransaction, cancellationToken).ConfigureAwait(false);
        if (existingAllocation is not null)
        {
            if (existingAllocation.BillId != billId)
                throw new EftBillMismatchException(request.IdempotencyKey, existingAllocation.BillId, billId);
            await dbTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new TenderApproved(existingAllocation.Amount);
        }

        // V1-RMD-258: a genuinely NEW tender must never be layered on top of
        // a Bill that already has an unresolved Payment (e.g. an unresolved
        // BankCard settlement attempt sitting at Unknown) — held under the
        // same bill-settlement lock just acquired above, so this read is
        // race-free against any concurrent settlement attempt for the same
        // bill.
        var existingPayments = await _paymentRepository.GetByBillIdAsync(billId, cancellationToken)
            .ConfigureAwait(false);
        var unsettledPayment = existingPayments.FirstOrDefault(p =>
            p.Status is PaymentStatus.Pending or PaymentStatus.Unknown or PaymentStatus.ReconciliationRequired);
        if (unsettledPayment is not null)
            throw new EftUnsettledPaymentExistsException(billId, unsettledPayment.Id, unsettledPayment.Status.ToString());

        var existingAllocations = await _allocationRepository.GetByBillIdAsync(billId, cancellationToken)
            .ConfigureAwait(false);
        var alreadyAllocated = existingAllocations.Sum(a => a.Amount);
        var remainingPayable = bill.PayableAmount - alreadyAllocated;
        if (request.Amount > remainingPayable)
            throw new EftOverTenderException(billId, request.Amount, remainingPayable);

        var payment = new Payment(Guid.NewGuid(), bill.Id, request.Amount)
            .Tender(request.Amount, reason: request.Note, changedBy: request.RecordedBy)
            .Approve(request.Amount, reason: request.Note, changedBy: request.RecordedBy);
        await _paymentRepository.AddAsync(payment, connection, dbTransaction, cancellationToken)
            .ConfigureAwait(false);

        var allocation = await _allocationRepository.AllocateAsync(
            payment, bill, request.Amount, request.IdempotencyKey, connection, dbTransaction, cancellationToken)
            .ConfigureAwait(false);

        await dbTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return new TenderApproved(allocation.Amount);
    }

    private static async Task LockIdempotencyKeyAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string idempotencyKey, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT pg_advisory_xact_lock(hashtext($1)::bigint);";
        await using (command)
        {
            command.Parameters.AddWithValue($"eft-tender:{idempotencyKey}");
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// V1-RMD-258: the shared bill-settlement advisory lock — same
    /// namespaced key (<c>"bill-settlement:{billId:N}"</c>) that
    /// <c>ALKAROS.Tables.PaymentTopology.PaymentAwareTableTopologyPolicy</c>
    /// and <c>ALKAROS.Payments.CardSettlement.CardSettlementOrchestrator</c>
    /// take, so a concurrent table transfer/merge/unmerge or BankCard
    /// settlement attempt against the same bill genuinely serializes
    /// against this EFT tender.
    /// </summary>
    private static async Task LockBillForSettlementAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid billId, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT pg_advisory_xact_lock(hashtext($1)::bigint);";
        await using (command)
        {
            command.Parameters.AddWithValue($"bill-settlement:{billId:N}");
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
