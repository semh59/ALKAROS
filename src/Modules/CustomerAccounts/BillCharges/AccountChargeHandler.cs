using ALKAROS.Billing.Adjustments;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.CustomerAccounts.TransactionLedger;
using ALKAROS.CustomerData.Profiles;
using ALKAROS.Payments.Allocations.Persistence;
using ALKAROS.Payments.PaymentAggregate;
using Npgsql;

namespace ALKAROS.CustomerAccounts.BillCharges;

/// <summary>
/// Account charge handler (V14-ACC-003, PDF:I.30-I.33/II.2.15/II.3.11/III.18):
/// checks eligibility and credit policy, then creates a Payment, an
/// AccountTransaction (Charge) and a PaymentAllocation atomically in one
/// database transaction - either all three (plus the Payment's own status
/// history rows) commit together, or none of them do. Mirrors
/// `ALKAROS.Cash.TenderHandler.CashTenderHandler`'s exact shape - an account
/// charge is a tender type exactly like cash or card, just settled against
/// the customer's own receivable ledger (V14-ACC-001) instead of a cash
/// drawer.
///
/// The eligibility check and the Bill lookup happen BEFORE that transaction
/// opens (plain reads against already-committed state) - same reasoning as
/// CashTenderHandler's own doc comment. The credit policy does not: it is
/// evaluated inside the transaction under a per-customer advisory lock
/// (V1-RMD-440), so two concurrent charges to the same customer are
/// serialized and the second one sees the first one's committed ledger row
/// instead of both passing against the same balance.
/// </summary>
public sealed class AccountChargeHandler : IAccountChargeHandler
{
    private readonly ICustomerProfileStore _profiles;
    private readonly ICustomerCreditPolicy _creditPolicy;
    private readonly IBillRepository _billRepository;
    private readonly IBillAdjustmentRepository _adjustmentRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPaymentAllocationRepository _allocationRepository;
    private readonly IAccountTransactionLedger _ledger;
    private readonly NpgsqlDataSource _dataSource;

    public AccountChargeHandler(
        ICustomerProfileStore profiles,
        ICustomerCreditPolicy creditPolicy,
        IBillRepository billRepository,
        IBillAdjustmentRepository adjustmentRepository,
        IPaymentRepository paymentRepository,
        IPaymentAllocationRepository allocationRepository,
        IAccountTransactionLedger ledger,
        NpgsqlDataSource dataSource)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _creditPolicy = creditPolicy ?? throw new ArgumentNullException(nameof(creditPolicy));
        _billRepository = billRepository ?? throw new ArgumentNullException(nameof(billRepository));
        _adjustmentRepository = adjustmentRepository ?? throw new ArgumentNullException(nameof(adjustmentRepository));
        _paymentRepository = paymentRepository ?? throw new ArgumentNullException(nameof(paymentRepository));
        _allocationRepository = allocationRepository ?? throw new ArgumentNullException(nameof(allocationRepository));
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<AccountChargeResult> HandleAsync(AccountChargeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        // Eligibility: the customer must exist and not already be anonymized.
        var profile = await _profiles.GetAsync(request.CustomerId, CustomerAccessRole.Manager, cancellationToken)
            ?? throw new CustomerProfileNotFoundException(request.CustomerId);
        if (profile.Anonymized)
            throw new AccountChargeCustomerAnonymizedException(request.CustomerId);

        var bill = await _billRepository.GetByIdAsync(request.BillId, cancellationToken)
            ?? throw new AccountChargeBillNotFoundException(request.BillId);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var dbTransaction = await connection.BeginTransactionAsync(cancellationToken);

        // Advisory lock on the idempotency key itself, held for the whole
        // transaction - same pg_advisory_xact_lock pattern
        // CashTenderHandler/PostgresPaymentAllocationRepository already use.
        await LockAsync(connection, dbTransaction, $"account-charge:{request.IdempotencyKey}", cancellationToken);

        var existingAllocation = await _allocationRepository.GetByIdempotencyKeyAsync(
            request.IdempotencyKey, connection, dbTransaction, cancellationToken);
        if (existingAllocation is not null)
        {
            await dbTransaction.CommitAsync(cancellationToken);
            return await BuildReplayResultAsync(existingAllocation, request.CustomerId, cancellationToken);
        }

        // Credit policy (V1-RMD-440): under the customer's own lock, held until
        // this transaction ends, so a concurrent charge to the same customer
        // waits here and then evaluates against this charge's committed row.
        // Neither the Bill nor the account is touched at all if this denies
        // the charge (V14-ACC-003 Acceptance evidence).
        await LockAsync(connection, dbTransaction, $"account-charge-customer:{request.CustomerId:N}", cancellationToken);
        var creditResult = await _creditPolicy.EvaluateAsync(request.CustomerId, request.AmountDue, cancellationToken);
        if (!creditResult.Approved)
            throw new AccountChargeCreditPolicyDeniedException(request.CustomerId, request.AmountDue, creditResult.DeniedReason);

        // Fail-fast pre-check (same shape and caveat as CashTenderHandler's
        // own comment: AllocateAsync's own per-bill advisory lock below
        // remains the real, concurrency-safe guard).
        var existingAllocations = await _allocationRepository.GetByBillIdAsync(bill.Id, cancellationToken);
        var alreadyAllocated = existingAllocations.Sum(a => a.Amount);
        var billAdjustments = await _adjustmentRepository.GetByBillIdAsync(bill.Id, cancellationToken);
        var adjustedPayableAmount = AdjustmentCalculator.Calculate(bill, billAdjustments).AdjustedPayableAmount;
        var remainingPayable = adjustedPayableAmount - alreadyAllocated;
        if (request.AmountDue > remainingPayable)
            throw new OverAllocationException(bill.Id, request.AmountDue, remainingPayable);

        var payment = new Payment(Guid.NewGuid(), bill.Id, request.AmountDue)
            .Tender(request.AmountDue, changedBy: request.RecordedBy)
            .Approve(request.AmountDue, changedBy: request.RecordedBy);
        await _paymentRepository.AddAsync(payment, connection, dbTransaction, cancellationToken);

        var chargeRequest = new RecordAccountTransactionRequest(
            request.CustomerId,
            AccountTransactionType.Charge,
            request.AmountDue,
            sourceReferenceType: "Payment",
            sourceReferenceId: payment.Id,
            note: null,
            createdBy: request.RecordedBy,
            occurredAt: DateTimeOffset.UtcNow);
        var accountTransaction = await _ledger.RecordAsync(chargeRequest, connection, dbTransaction, cancellationToken);

        var allocation = await _allocationRepository.AllocateAsync(
            payment, bill, request.AmountDue, request.IdempotencyKey, connection, dbTransaction, cancellationToken);

        await dbTransaction.CommitAsync(cancellationToken);

        return new AccountChargeResult(payment.Id, allocation.Id, accountTransaction.Id, request.AmountDue, WasReplayed: false);
    }

    /// <summary>
    /// Reconstructs the result of a command that already completed on an
    /// earlier attempt - the AccountTransaction is found by scanning the
    /// customer's own ledger for the one tied to this allocation's Payment,
    /// same technique as CashTenderHandler's own BuildReplayResultAsync
    /// (only ever exercised on a genuine retry, never the hot path).
    /// </summary>
    private async Task<AccountChargeResult> BuildReplayResultAsync(
        PaymentAllocation allocation, Guid customerId, CancellationToken cancellationToken)
    {
        var payment = await _paymentRepository.GetByIdAsync(allocation.PaymentId, cancellationToken)
            ?? throw new PaymentNotFoundException(allocation.PaymentId);
        var ledger = await _ledger.GetByCustomerAsync(customerId, cancellationToken: cancellationToken);
        var accountTransaction = ledger.FirstOrDefault(
            entry => entry.SourceReferenceType == "Payment" && entry.SourceReferenceId == payment.Id);

        return new AccountChargeResult(
            payment.Id,
            allocation.Id,
            accountTransaction?.Id ?? Guid.Empty,
            payment.ApprovedAmount ?? 0m,
            WasReplayed: true);
    }

    private static async Task LockAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string lockKey, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT pg_advisory_xact_lock(hashtext($1)::bigint);";
        await using (command)
        {
            command.Parameters.AddWithValue(lockKey);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
