using ALKAROS.Billing.Adjustments;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Cash.Contracts;
using ALKAROS.Cash.SessionLifecycle;
using ALKAROS.Cash.TransactionLedger;
using ALKAROS.Payments.Allocations.Persistence;
using ALKAROS.Payments.PaymentAggregate;
using Npgsql;

namespace ALKAROS.Cash.TenderHandler;

/// <summary>
/// Cash tender handler (V13-CSH-003, PDF:I.26-I.29/I.49/II.2.7/II.5.9/III.9):
/// validates the open session and the tendered amount, then creates a
/// Payment, PaymentAllocation and CashTransaction atomically in one
/// database transaction — either all three (plus the Payment's own status
/// history rows) commit together, or none of them do.
///
/// The session-open check and the Bill lookup happen BEFORE that
/// transaction opens (plain reads against already-committed state) — this
/// task's own Owned surface may not change CashSession or allocation
/// persistence schema, and a session closing in the narrow window between
/// that check and the write is the same class of race every other
/// business-rule precondition in this codebase accepts (the write itself
/// still cannot corrupt anything: the four records either all land or none
/// do, which is this task's actual Acceptance evidence).
/// </summary>
public sealed class CashTenderHandler : ICashTenderHandler
{
    private readonly ICashSessionRepository _sessionRepository;
    private readonly IBillRepository _billRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPaymentAllocationRepository _allocationRepository;
    private readonly ICashTransactionLedgerRepository _ledgerRepository;
    private readonly IBillAdjustmentRepository _adjustmentRepository;
    private readonly NpgsqlDataSource _dataSource;

    public CashTenderHandler(
        ICashSessionRepository sessionRepository,
        IBillRepository billRepository,
        IPaymentRepository paymentRepository,
        IPaymentAllocationRepository allocationRepository,
        ICashTransactionLedgerRepository ledgerRepository,
        IBillAdjustmentRepository adjustmentRepository,
        NpgsqlDataSource dataSource)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _billRepository = billRepository ?? throw new ArgumentNullException(nameof(billRepository));
        _paymentRepository = paymentRepository ?? throw new ArgumentNullException(nameof(paymentRepository));
        _allocationRepository = allocationRepository ?? throw new ArgumentNullException(nameof(allocationRepository));
        _ledgerRepository = ledgerRepository ?? throw new ArgumentNullException(nameof(ledgerRepository));
        _adjustmentRepository = adjustmentRepository ?? throw new ArgumentNullException(nameof(adjustmentRepository));
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<CashTenderResult> HandleAsync(CashTenderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        var session = await _sessionRepository.GetByIdAsync(request.CashSessionId, cancellationToken)
            ?? throw new CashSessionNotFoundException(request.CashSessionId);
        if (session.Snapshot.Status != CashSessionStatus.Open)
            throw new ClosedCashSessionException(request.CashSessionId, session.Snapshot.Status);

        var bill = await _billRepository.GetByIdAsync(request.BillId, cancellationToken)
            ?? throw new CashTenderBillNotFoundException(request.BillId);

        if (request.TenderedAmount < request.AmountDue)
            throw new InsufficientCashTenderException(request.TenderedAmount, request.AmountDue);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var dbTransaction = await connection.BeginTransactionAsync(cancellationToken);

        // Advisory lock on the idempotency key itself (held for the whole
        // transaction, same pg_advisory_xact_lock pattern
        // PostgresPaymentAllocationRepository already uses per-bill): a
        // second concurrent submit of the exact same command blocks here
        // until the first one commits, then finds the allocation already
        // recorded below and replays instead of racing its own Payment/
        // CashTransaction insert against the first attempt's.
        // V1-RMD-409 (V1-RMD-393 F-04): the shared bill-settlement lock FIRST, idempotency-key lock second — the
        // same fixed order and key EftTenderHandler and CardSettlementOrchestrator use, so a cash tender now
        // serializes against a concurrent card attempt on the same bill instead of racing it.
        await LockBillForSettlementAsync(connection, dbTransaction, bill.Id, cancellationToken);
        await LockIdempotencyKeyAsync(connection, dbTransaction, request.IdempotencyKey, cancellationToken);

        var existingAllocation = await _allocationRepository.GetByIdempotencyKeyAsync(
            request.IdempotencyKey, connection, dbTransaction, cancellationToken);
        if (existingAllocation is not null)
        {
            var replay = await BuildReplayResultAsync(existingAllocation, request, cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);
            return replay;
        }

        // V1-RMD-409 (V1-RMD-393 F-04): V1-RMD-258 left cash out of this guard because a cash payment itself is
        // never Unknown — but another payment on the same bill can be. If that card attempt was really charged,
        // cash on top of it charges the guest twice. Read under the bill-settlement lock just taken.
        var existingPayments = await _paymentRepository.GetByBillIdAsync(bill.Id, cancellationToken);
        var unsettledPayment = existingPayments.FirstOrDefault(p =>
            p.Status is PaymentStatus.Pending or PaymentStatus.Unknown or PaymentStatus.ReconciliationRequired);
        if (unsettledPayment is not null)
            throw new CashTenderUnsettledPaymentExistsException(bill.Id, unsettledPayment.Id);

        // Fail-fast check, now that this is confirmed to be a genuinely new
        // command (not a replay of one that already succeeded): a plain
        // read of the bill's own already-allocated total, same connection.
        // AllocateAsync below remains the real, concurrency-safe guard (its
        // own per-bill advisory lock, inside this same atomic transaction)
        // — this one only avoids inserting and then immediately rolling
        // back the Payment/CashTransaction for the common case of a
        // caller-supplied AmountDue that plainly exceeds what the bill has
        // left, without weakening the actual invariant (a bill's total
        // allocated can never exceed its payable amount, enforced under
        // lock either way). Found by an independent review (2026-09-18): an
        // earlier draft ran this check BEFORE the replay check above and
        // broke replay itself (a second, identical submit of an
        // already-fully-allocated bill was wrongly rejected as
        // over-allocating).
        // V1-RMD-298: the real (discount/tip-adjusted) ceiling, not bill.PayableAmount - see
        // PaymentAllocationFactory.Create's own comment. This is still only the fail-fast pre-check (see the
        // class doc comment above); AllocateAsync's own call into the factory below remains the real,
        // concurrency-safe guard.
        var existingAllocations = await _allocationRepository.GetByBillIdAsync(bill.Id, cancellationToken);
        var alreadyAllocated = existingAllocations.Sum(a => a.Amount);
        var billAdjustments = await _adjustmentRepository.GetByBillIdAsync(bill.Id, cancellationToken);
        var adjustedPayableAmount = AdjustmentCalculator.Calculate(bill, billAdjustments).AdjustedPayableAmount;
        var remainingPayable = adjustedPayableAmount - alreadyAllocated;
        if (request.AmountDue > remainingPayable)
            throw new OverAllocationException(bill.Id, request.AmountDue, remainingPayable);

        var approvedAmount = request.AmountDue;
        var changeAmount = request.TenderedAmount - approvedAmount;

        var payment = new Payment(Guid.NewGuid(), bill.Id, request.AmountDue)
            .Tender(request.TenderedAmount, changedBy: request.RecordedBy)
            .Approve(approvedAmount, changedBy: request.RecordedBy);
        await _paymentRepository.AddAsync(payment, connection, dbTransaction, cancellationToken);

        var cashTransaction = new CashTransaction(
            Guid.NewGuid(),
            request.CashSessionId,
            CashTransactionType.Sale,
            approvedAmount,
            CashTransactionDirection.In,
            relatedPaymentId: payment.Id,
            recordedBy: request.RecordedBy);
        await _ledgerRepository.RecordAsync(cashTransaction, connection, dbTransaction, cancellationToken);

        var allocation = await _allocationRepository.AllocateAsync(
            payment, bill, approvedAmount, request.IdempotencyKey, connection, dbTransaction, cancellationToken);

        await dbTransaction.CommitAsync(cancellationToken);

        return new CashTenderResult(
            payment.Id, allocation.Id, cashTransaction.Id, approvedAmount, changeAmount, WasReplayed: false);
    }

    /// <summary>
    /// Reconstructs the result of a command that already completed on an
    /// earlier attempt — the CashTransaction it posted is the only one of
    /// the three records not addressable directly by id, so it is found by
    /// scanning the session's own ledger for the one tied to this
    /// allocation's Payment (only ever exercised on a genuine retry, never
    /// the hot path).
    ///
    /// V1-RMD-415 (V1-RMD-393 F-12): a key only replays the cash sale it named. A key another tender method
    /// (EFT, card) already used, or one reused for another bill, amount or drawer session, has no cash Sale in
    /// this session behind it; answering 200 there told the cashier to put money in a drawer whose ledger never
    /// saw it, so the drawer came out over at close.
    /// </summary>
    private async Task<CashTenderResult> BuildReplayResultAsync(
        PaymentAllocation allocation, CashTenderRequest request, CancellationToken cancellationToken)
    {
        var payment = await _paymentRepository.GetByIdAsync(allocation.PaymentId, cancellationToken)
            ?? throw new PaymentNotFoundException(allocation.PaymentId);
        var ledger = await _ledgerRepository.GetBySessionIdAsync(request.CashSessionId, cancellationToken);
        var cashTransaction = ledger.FirstOrDefault(entry =>
            entry.RelatedPaymentId == payment.Id && entry.Type == CashTransactionType.Sale);
        if (cashTransaction is null || allocation.BillId != request.BillId || allocation.Amount != request.AmountDue)
            throw new CashTenderIdempotencyKeyReusedException(request.IdempotencyKey);

        return new CashTenderResult(
            payment.Id,
            allocation.Id,
            cashTransaction.Id,
            payment.ApprovedAmount ?? 0m,
            payment.ChangeAmount,
            WasReplayed: true);
    }

    private static async Task LockBillForSettlementAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid billId, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT pg_advisory_xact_lock(hashtext($1)::bigint);";
        await using (command)
        {
            command.Parameters.AddWithValue($"bill-settlement:{billId:N}");
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task LockIdempotencyKeyAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string idempotencyKey, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT pg_advisory_xact_lock(hashtext($1)::bigint);";
        await using (command)
        {
            command.Parameters.AddWithValue($"cash-tender:{idempotencyKey}");
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
