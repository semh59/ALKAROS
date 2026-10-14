using ALKAROS.Audit.EventStore;
using ALKAROS.Cash.Contracts;
using ALKAROS.Cash.SessionLifecycle;
using ALKAROS.Cash.TransactionLedger;
using ALKAROS.Payments.PaymentAggregate;
using Npgsql;
using NpgsqlTypes;
using System.Data;
using System.Text.Json;

namespace ALKAROS.Host.Experience.Billing;

public sealed record RefundRequestInputV1(Guid PaymentId, string? Reason);

public sealed record RefundDecisionInputV1(string? Note);

public sealed record RefundCompleteInputV1(string? SlipNumber, string? Note);

public sealed record RefundRequestV1(
    Guid RequestId,
    Guid BillId,
    Guid PaymentId,
    decimal Amount,
    string Method,
    string Reason,
    string Status,
    Guid RequestedBy,
    DateTimeOffset RequestedAt,
    Guid? DecidedBy,
    DateTimeOffset? DecidedAt,
    string? SlipNumber,
    bool BillCancelled,
    bool ApprovedFromAllowance = false);

public sealed record RefundAllowanceInputV1(Guid CashierUserId, decimal Amount);

public sealed record RefundAllowanceV1(bool HasOpenSession, decimal Allowance, decimal Used, decimal Remaining);

public sealed class RefundInputException(string message) : Exception(message);

public sealed class RefundPaymentNotRefundableException(Guid paymentId)
    : Exception($"Payment {paymentId} is not an approved payment of this bill.");

public sealed class RefundAlreadyRequestedException(Guid paymentId)
    : Exception($"Payment {paymentId} already has an open refund request.");

public sealed class RefundStateException(Guid requestId, string status)
    : Exception($"Refund request {requestId} is {status}.");

public sealed class RefundSelfDecisionException()
    : Exception("The person who asked for a refund cannot decide it.");

public sealed class RefundNoOpenCashSessionException(Guid userId)
    : Exception($"User {userId} has no open cash session to pay the refund from.");

public sealed class RefundAccountChargeUnsupportedException(Guid paymentId)
    : Exception($"Payment {paymentId} was charged to a customer account and cannot be refunded here.");

/// <summary>
/// Refund of one approved payment: a cashier asks, someone else decides. Approved cash goes out of the cashier's open
/// cash session at once; any other method waits for the manual result (slip number). Once every payment of the bill
/// is back the bill is cancelled.
/// </summary>
public sealed class PaymentRefundStore
{
    public const int MaxTextLength = 500;
    public const int MaxSlipLength = 50;
    public const decimal MaxAllowance = 10_000_000m;

    private readonly NpgsqlDataSource _dataSource;
    private readonly IAuditEventStore _audit;
    private readonly PostgresPaymentReversalRepository _reversals;
    private readonly PostgresCashTransactionLedgerRepository _cash;

    public PaymentRefundStore(NpgsqlDataSource dataSource, IAuditEventStore audit)
    {
        _dataSource = dataSource;
        _audit = audit;
        _reversals = new PostgresPaymentReversalRepository(dataSource, new PostgresPaymentRepository(dataSource));
        _cash = new PostgresCashTransactionLedgerRepository(dataSource);
    }

    public async Task<RefundRequestV1> RequestAsync(
        Guid billId, Guid paymentId, string? reason, Guid userId, CancellationToken cancellationToken)
    {
        var text = RequireText(reason, MaxTextLength, "Refund reason");
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await LockBillAsync(connection, transaction, billId, cancellationToken);

        decimal amount;
        await using (var read = new NpgsqlCommand(
            "SELECT status, approved_amount FROM payments.payments WHERE payment_id = @id AND bill_id = @bill FOR UPDATE;",
            connection, transaction))
        {
            read.Parameters.Add("id", NpgsqlDbType.Uuid).Value = paymentId;
            read.Parameters.Add("bill", NpgsqlDbType.Uuid).Value = billId;
            await using var reader = await read.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) || reader.GetString(0) != "Approved" || reader.IsDBNull(1))
                throw new RefundPaymentNotRefundableException(paymentId);
            amount = reader.GetDecimal(1);
        }

        string method;
        await using (var kind = new NpgsqlCommand(
            """
            SELECT CASE
                WHEN EXISTS (SELECT 1 FROM customer_account.account_transactions a
                             WHERE a.source_reference_type = 'Payment' AND a.source_reference_id = @id
                               AND a.transaction_type = 'Charge') THEN 'Account'
                WHEN EXISTS (SELECT 1 FROM cash.cash_transactions c
                             WHERE c.related_payment_id = @id AND c.type = 'Sale') THEN 'Cash'
                ELSE 'Manual' END;
            """, connection, transaction))
        {
            kind.Parameters.Add("id", NpgsqlDbType.Uuid).Value = paymentId;
            method = (string)(await kind.ExecuteScalarAsync(cancellationToken))!;
        }
        if (method == "Account")
            throw new RefundAccountChargeUnsupportedException(paymentId);

        var requestId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var created = new RefundRequest(
            requestId, billId, paymentId, amount, method, text, "Requested", userId, now, null, null, null);
        if (!await PostgresRefundRequestRepository.TryInsertAsync(created, connection, transaction, cancellationToken))
            throw new RefundAlreadyRequestedException(paymentId);

        var request = new RefundRequestV1(requestId, billId, paymentId, amount, method, text, "Requested", userId, now, null, null, null, false);
        var sessionId = await FindOpenSessionAsync(connection, transaction, userId, cancellationToken);
        if (sessionId is null || !await PostgresRefundAllowanceRepository.TrySpendAsync(sessionId.Value, amount, connection, transaction, cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return request;
        }

        var billCancelled = false;
        var completed = method == "Cash";
        if (completed)
            billCancelled = await PayOutCashAsync(connection, transaction, request, sessionId.Value, userId, now, cancellationToken);
        await PostgresRefundRequestRepository.MarkApprovedAsync(
            requestId, completed, userId, "Allowance", now, connection, transaction, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        var result = request with
        {
            Status = completed ? "Completed" : "AwaitingManualRefund", DecidedBy = userId, DecidedAt = now,
            BillCancelled = billCancelled, ApprovedFromAllowance = true,
        };
        if (completed)
            await AuditRefundAsync(result, userId, null, billCancelled, cancellationToken);
        return result;
    }

    /// <summary>Gives the cashier's open shift a refund budget the cashier may spend without a second person.</summary>
    public async Task<RefundAllowanceV1> SetAllowanceAsync(
        Guid cashierUserId, decimal amount, Guid actorId, CancellationToken cancellationToken)
    {
        if (amount < 0 || amount > MaxAllowance)
            throw new RefundInputException($"Allowance must be between 0 and {MaxAllowance}.");
        amount = decimal.Round(amount, 2);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var change = await PostgresRefundAllowanceRepository.SetAsync(
            cashierUserId, amount, connection, transaction, cancellationToken);
        if (change.SessionId is not { } sessionId)
            throw new RefundNoOpenCashSessionException(cashierUserId);
        if (!change.Applied)
            throw new RefundInputException("Allowance cannot be lower than what the cashier already used.");
        var before = change.Previous;
        var used = change.Used;

        await transaction.CommitAsync(cancellationToken);
        await _audit.AppendAsync(
            new AuditEvent(
                Guid.NewGuid(), "refund.allowance.set", "CashSession", sessionId, "User", sessionId.ToString("N"),
                actorId, "Shift refund allowance set",
                beforeStateJson: JsonSerializer.Serialize(new { allowance = before }),
                afterStateJson: JsonSerializer.Serialize(new { allowance = amount, cashierUserId })),
            cancellationToken);
        return new RefundAllowanceV1(true, amount, used, amount - used);
    }

    public async Task<RefundAllowanceV1> GetAllowanceAsync(Guid cashierUserId, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT refund_allowance, refund_allowance_used FROM cash.cash_sessions
            WHERE cashier_user_id = @user AND status = 'Open'
            ORDER BY opened_at DESC LIMIT 1;
            """);
        command.Parameters.Add("user", NpgsqlDbType.Uuid).Value = cashierUserId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return new RefundAllowanceV1(false, 0m, 0m, 0m);
        var allowance = reader.GetDecimal(0);
        var used = reader.GetDecimal(1);
        return new RefundAllowanceV1(true, allowance, used, allowance - used);
    }

    public async Task<IReadOnlyList<RefundRequestV1>> ListAsync(string? status, CancellationToken cancellationToken)
    {
        var rows = await PostgresRefundRequestRepository.ListAsync(_dataSource, status, cancellationToken);
        return rows.Select(row => ToDto(row)).ToList();
    }

    public async Task<RefundRequestV1> ApproveAsync(Guid requestId, Guid deciderId, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var request = await LoadAsync(connection, transaction, requestId, cancellationToken);
        await LockBillAsync(connection, transaction, request.BillId, cancellationToken);
        if (request.Status != "Requested")
            throw new RefundStateException(requestId, request.Status);
        if (request.RequestedBy == deciderId)
            throw new RefundSelfDecisionException();

        var now = DateTimeOffset.UtcNow;
        var billCancelled = false;
        string status;
        if (request.Method == "Cash")
        {
            var sessionId = await FindOpenSessionAsync(connection, transaction, request.RequestedBy, cancellationToken)
                ?? throw new RefundNoOpenCashSessionException(request.RequestedBy);
            billCancelled = await PayOutCashAsync(connection, transaction, request, sessionId, deciderId, now, cancellationToken);
            status = "Completed";
        }
        else
        {
            status = "AwaitingManualRefund";
        }

        await PostgresRefundRequestRepository.MarkApprovedAsync(
            requestId, status == "Completed", deciderId, "Manager", now, connection, transaction, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        if (status == "Completed")
            await AuditRefundAsync(request, deciderId, null, billCancelled, cancellationToken);
        return request with
        {
            Status = status, DecidedBy = deciderId, DecidedAt = now, BillCancelled = billCancelled,
        };
    }

    public async Task<RefundRequestV1> RejectAsync(
        Guid requestId, Guid deciderId, string? note, CancellationToken cancellationToken)
    {
        var text = RequireText(note, MaxTextLength, "Rejection note");
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var request = await LoadAsync(connection, transaction, requestId, cancellationToken);
        if (request.Status != "Requested")
            throw new RefundStateException(requestId, request.Status);
        if (request.RequestedBy == deciderId)
            throw new RefundSelfDecisionException();

        var now = DateTimeOffset.UtcNow;
        await PostgresRefundRequestRepository.MarkRejectedAsync(requestId, deciderId, text, now, connection, transaction, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        await _audit.AppendAsync(
            new AuditEvent(
                Guid.NewGuid(), "refund.rejected", "Payment", request.PaymentId, "User", requestId.ToString("N"),
                deciderId, text,
                beforeStateJson: JsonSerializer.Serialize(new { status = "Requested" }),
                afterStateJson: JsonSerializer.Serialize(new { status = "Rejected", requestId })),
            cancellationToken);
        return request with { Status = "Rejected", DecidedBy = deciderId, DecidedAt = now };
    }

    public async Task<RefundRequestV1> CompleteManualAsync(
        Guid requestId, string? slipNumber, string? note, Guid userId, CancellationToken cancellationToken)
    {
        var slip = RequireText(slipNumber, MaxSlipLength, "Slip number");
        var noteText = string.IsNullOrWhiteSpace(note) ? null : RequireText(note, MaxTextLength, "Note");
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var request = await LoadAsync(connection, transaction, requestId, cancellationToken);
        await LockBillAsync(connection, transaction, request.BillId, cancellationToken);
        if (request.Status != "AwaitingManualRefund")
            throw new RefundStateException(requestId, request.Status);

        var now = DateTimeOffset.UtcNow;
        await _reversals.AppendAsync(
            request.PaymentId, request.Amount, request.Reason, userId, "refund-" + requestId.ToString("N"),
            connection, transaction, now, cancellationToken);
        var billCancelled = await CancelBillWhenFullyRefundedAsync(
            connection, transaction, request.BillId, userId, now, cancellationToken);

        await PostgresRefundRequestRepository.MarkCompletedAsync(
            requestId, slip, noteText, userId, now, connection, transaction, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        await AuditRefundAsync(request, userId, slip, billCancelled, cancellationToken);
        return request with { Status = "Completed", SlipNumber = slip, BillCancelled = billCancelled };
    }

    /// <summary>Pays a cash refund out of the cashier's session, reverses the payment and closes the bill when nothing is left.</summary>
    private async Task<bool> PayOutCashAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, RefundRequestV1 request, Guid sessionId,
        Guid deciderId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var key = "refund-" + request.RequestId.ToString("N");
        await _cash.RecordAsync(
            new CashTransaction(
                Guid.NewGuid(), sessionId, CashTransactionType.Refund, request.Amount, CashTransactionDirection.Out,
                relatedPaymentId: request.PaymentId, notes: request.Reason, recordedBy: request.RequestedBy,
                occurredAt: now, idempotencyKey: key),
            connection, transaction, cancellationToken);
        await _reversals.AppendAsync(
            request.PaymentId, request.Amount, request.Reason, deciderId, key, connection, transaction, now, cancellationToken);
        return await CancelBillWhenFullyRefundedAsync(
            connection, transaction, request.BillId, deciderId, now, cancellationToken);
    }

    private Task AuditRefundAsync(
        RefundRequestV1 request, Guid actorId, string? slip, bool billCancelled, CancellationToken cancellationToken)
        => _audit.AppendAsync(
            new AuditEvent(
                Guid.NewGuid(), "payment.refunded", "Payment", request.PaymentId, "User", request.RequestId.ToString("N"),
                actorId, request.Reason,
                beforeStateJson: JsonSerializer.Serialize(new { status = "Approved", amount = request.Amount }),
                afterStateJson: JsonSerializer.Serialize(new
                {
                    status = "Refunded", request.Method, slipNumber = slip, request.BillId, billCancelled,
                    requestId = request.RequestId, requestedBy = request.RequestedBy,
                    viaAllowance = request.ApprovedFromAllowance,
                })),
            cancellationToken);

    /// <summary>
    /// Cancels the bill once at least one payment of it was refunded and none still holds money; the allocations stay
    /// as the record of what was paid.
    /// </summary>
    private static async Task<bool> CancelBillWhenFullyRefundedAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid billId, Guid actorId,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            WITH decision AS (
                SELECT EXISTS (SELECT 1 FROM payments.payments WHERE bill_id = @bill AND status = 'Refunded')
                   AND NOT EXISTS (SELECT 1 FROM payments.payments
                                   WHERE bill_id = @bill
                                     AND status IN ('Approved', 'PartiallyRefunded', 'Pending', 'Unknown', 'ReconciliationRequired'))
                       AS close_it),
            cancelled AS (
                UPDATE billing.bills
                SET status = 'Cancelled', cancelled_at = @now, updated_at = @now,
                    cancellation_reason = 'Ödeme iade edildi', cancelled_by = @actor, row_version = row_version + 1
                WHERE bill_id = @bill AND status <> 'Cancelled' AND (SELECT close_it FROM decision)
                RETURNING bill_id),
            items AS (
                UPDATE billing.bill_items SET superseded_at = @now
                WHERE bill_id IN (SELECT bill_id FROM cancelled) AND superseded_at IS NULL
                RETURNING 1),
            pointer AS (
                UPDATE table_mgmt.tables SET current_bill_id = NULL, row_version = row_version + 1
                WHERE current_bill_id IN (SELECT bill_id FROM cancelled)
                RETURNING 1)
            SELECT count(*) FROM cancelled;
            """, connection, transaction);
        command.Parameters.Add("bill", NpgsqlDbType.Uuid).Value = billId;
        command.Parameters.Add("now", NpgsqlDbType.TimestampTz).Value = now.UtcDateTime;
        command.Parameters.Add("actor", NpgsqlDbType.Uuid).Value = actorId;
        return (long)(await command.ExecuteScalarAsync(cancellationToken))! > 0;
    }

    private static async Task<Guid?> FindOpenSessionAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid cashierUserId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT cash_session_id FROM cash.cash_sessions
            WHERE cashier_user_id = @user AND status = 'Open'
            ORDER BY opened_at DESC LIMIT 1;
            """, connection, transaction);
        command.Parameters.Add("user", NpgsqlDbType.Uuid).Value = cashierUserId;
        return await command.ExecuteScalarAsync(cancellationToken) as Guid?;
    }

    private static async Task LockBillAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid billId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtext($1)::bigint);", connection, transaction);
        command.Parameters.AddWithValue($"bill-settlement:{billId:N}");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<RefundRequestV1> LoadAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid requestId, CancellationToken cancellationToken)
    {
        var row = await PostgresRefundRequestRepository.LoadForUpdateAsync(requestId, connection, transaction, cancellationToken)
            ?? throw new BillingSplitNotFoundException($"Refund request {requestId} was not found.");
        return ToDto(row);
    }

    private static RefundRequestV1 ToDto(RefundRequest row) => new(
        row.Id, row.BillId, row.PaymentId, row.Amount, row.Method, row.Reason, row.Status, row.RequestedBy,
        row.RequestedAt, row.DecidedBy, row.DecidedAt, row.SlipNumber, BillCancelled: false,
        ApprovedFromAllowance: row.ApprovalMode == "Allowance");

    private static string RequireText(string? value, int max, string what)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > max)
            throw new RefundInputException($"{what} must be 1 to {max} characters.");
        return trimmed;
    }
}
