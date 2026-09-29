using ALKAROS.Cash.Contracts;
using ALKAROS.Cash.TransactionLedger;
using ALKAROS.CustomerAccounts.AccountPayments;
using ALKAROS.CustomerAccounts.TransactionLedger;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.CustomerAccounts.CashReceipts;

/// <summary>
/// V14-ACC-005: a customer pays cash towards their receivable account, independent of any Bill. In ONE database
/// transaction it writes
/// <list type="number">
/// <item>the AccountPayment (V14-ACC-004), requested and then approved with the cash transaction as its evidence,</item>
/// <item>the CashIn CashTransaction in the open session (the drawer's expected cash grows by the amount), and</item>
/// <item>the Payment AccountTransaction (the receivable, and through its trigger the balance projection, shrinks).</item>
/// </list>
/// All three commit together or none do. The same idempotency key replays the stored result.
///
/// Serialization: the idempotency key and the customer are advisory-locked for the whole transaction - the customer
/// lock is the same one <c>AccountChargeHandler</c> takes, so a receipt and a charge never check the balance against
/// each other's uncommitted state. The session row is read FOR SHARE, so a session cannot close while a receipt is
/// being recorded into it.
///
/// No <c>payments.payments</c> row is written: that table is Bill-scoped and counted as revenue; the charge that
/// created this debt already recorded its approved Payment on the Bill (see the task file's implementation notes).
/// </summary>
public sealed class CashAccountReceiptHandler : ICashAccountReceiptHandler
{
    private readonly IAccountPaymentRepository _payments;
    private readonly ICashTransactionLedgerRepository _cashLedger;
    private readonly IAccountTransactionLedger _accountLedger;
    private readonly NpgsqlDataSource _dataSource;

    public CashAccountReceiptHandler(
        IAccountPaymentRepository payments,
        ICashTransactionLedgerRepository cashLedger,
        IAccountTransactionLedger accountLedger,
        NpgsqlDataSource dataSource)
    {
        _payments = payments ?? throw new ArgumentNullException(nameof(payments));
        _cashLedger = cashLedger ?? throw new ArgumentNullException(nameof(cashLedger));
        _accountLedger = accountLedger ?? throw new ArgumentNullException(nameof(accountLedger));
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<CashAccountReceiptResult> ReceiveAsync(CashAccountReceiptRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await LockAsync(connection, transaction, $"account-receipt:{request.IdempotencyKey}", cancellationToken);

        var existing = await _payments.GetByIdempotencyKeyAsync(request.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.CustomerId != request.CustomerId || existing.Method != AccountPaymentMethod.Cash || existing.Amount != request.Amount)
                throw new AccountPaymentIdempotencyKeyReusedException(request.IdempotencyKey);
            await transaction.CommitAsync(cancellationToken);
            return await BuildReplayResultAsync(existing, cancellationToken);
        }

        await LockAsync(connection, transaction, $"account-charge-customer:{request.CustomerId:N}", cancellationToken);

        if (!await CustomerIsActiveAsync(connection, transaction, request.CustomerId, cancellationToken))
            throw new CashAccountReceiptCustomerNotFoundException(request.CustomerId);

        var sessionStatus = await ReadSessionStatusForShareAsync(connection, transaction, request.CashSessionId, cancellationToken);
        if (sessionStatus != "Open")
            throw new CashAccountReceiptSessionNotOpenException(request.CashSessionId, sessionStatus);

        var outstanding = await ReadBalanceAsync(connection, transaction, request.CustomerId, cancellationToken);
        if (request.Amount > outstanding)
            throw new CashAccountReceiptOverpaymentException(request.CustomerId, request.Amount, outstanding);

        var now = DateTimeOffset.UtcNow;
        var requested = await _payments.RequestAsync(
            AccountPayment.Request(request.CustomerId, AccountPaymentMethod.Cash, request.Amount, request.IdempotencyKey, request.RecordedBy, now),
            connection, transaction, cancellationToken);

        var cashTransaction = new CashTransaction(
            Guid.NewGuid(),
            request.CashSessionId,
            CashTransactionType.CashIn,
            request.Amount,
            CashTransactionDirection.In,
            notes: $"Cari hesap tahsilatı (müşteri {request.CustomerId:D})",
            recordedBy: request.RecordedBy,
            occurredAt: now,
            idempotencyKey: $"account-payment:{requested.Id:D}");
        await _cashLedger.RecordAsync(cashTransaction, connection, transaction, cancellationToken);

        await _payments.SaveTransitionAsync(
            requested.Approve(AccountPaymentEvidence.ForCashTransaction(cashTransaction.Id), now),
            requested.RowVersion, "Nakit cari tahsilat", request.RecordedBy, connection, transaction, cancellationToken);

        var accountTransaction = await _accountLedger.RecordAsync(
            new RecordAccountTransactionRequest(
                request.CustomerId,
                AccountTransactionType.Payment,
                request.Amount,
                sourceReferenceType: "AccountPayment",
                sourceReferenceId: requested.Id,
                note: null,
                createdBy: request.RecordedBy,
                occurredAt: now),
            connection, transaction, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new CashAccountReceiptResult(
            requested.Id, cashTransaction.Id, accountTransaction.Id, request.Amount, outstanding - request.Amount, WasReplayed: false);
    }

    private async Task<CashAccountReceiptResult> BuildReplayResultAsync(AccountPayment payment, CancellationToken cancellationToken)
    {
        var cashTransactionId = payment.Evidence is { Type: AccountPaymentEvidenceType.CashTransaction } evidence
            ? Guid.Parse(evidence.Reference)
            : Guid.Empty;
        var ledger = await _accountLedger.GetByCustomerAsync(payment.CustomerId, cancellationToken: cancellationToken);
        var accountTransaction = ledger.FirstOrDefault(
            entry => entry.SourceReferenceType == "AccountPayment" && entry.SourceReferenceId == payment.Id);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var balance = await ReadBalanceAsync(connection, null, payment.CustomerId, cancellationToken);
        return new CashAccountReceiptResult(
            payment.Id, cashTransactionId, accountTransaction?.Id ?? Guid.Empty, payment.Amount, balance, WasReplayed: true);
    }

    private static async Task LockAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string key, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext(@key)::bigint);", connection, transaction);
        command.Parameters.Add("key", NpgsqlDbType.Text).Value = key;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> CustomerIsActiveAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid customerId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT 1 FROM customer_data.profiles WHERE customer_id = @customer_id AND NOT anonymized;", connection, transaction);
        command.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = customerId;
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task<string?> ReadSessionStatusForShareAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid cashSessionId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT status FROM cash.cash_sessions WHERE cash_session_id = @id FOR SHARE;", connection, transaction);
        command.Parameters.Add("id", NpgsqlDbType.Uuid).Value = cashSessionId;
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    private static async Task<decimal> ReadBalanceAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid customerId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT current_balance FROM customer_account.balances WHERE customer_id = @customer_id;", connection, transaction);
        command.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = customerId;
        return await command.ExecuteScalarAsync(cancellationToken) is decimal balance ? balance : 0m;
    }
}
