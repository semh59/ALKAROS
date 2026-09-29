using ALKAROS.CustomerAccounts.AccountPayments;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.CustomerAccounts.AccountReceipts;

/// <summary>
/// Postgres-backed <see cref="IAccountReceiptService"/> (migration 166). Reads the account payment through
/// <see cref="IAccountPaymentRepository"/> and never writes to it; creates no Bill and no PaymentAllocation.
/// </summary>
public sealed class PostgresAccountReceiptService : IAccountReceiptService
{
    private const string Columns =
        "account_receipt_id, receipt_number, customer_id, account_payment_id, amount, currency_code, idempotency_key, issued_by, issued_at";

    private readonly IAccountPaymentRepository _payments;
    private readonly NpgsqlDataSource _dataSource;

    public PostgresAccountReceiptService(IAccountPaymentRepository payments, NpgsqlDataSource dataSource)
    {
        _payments = payments ?? throw new ArgumentNullException(nameof(payments));
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<AccountReceiptIssueResult> IssueAsync(
        Guid accountPaymentId, string idempotencyKey, Guid? issuedBy, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key cannot be empty.", nameof(idempotencyKey));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // One lock per account payment: two concurrent requests for it (with the same or different keys) are
        // serialized, and the second one finds the first one's receipt.
        await using (var lockCommand = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext(@key)::bigint);", connection, transaction))
        {
            lockCommand.Parameters.Add("key", NpgsqlDbType.Text).Value = $"account-receipt-payment:{accountPaymentId:N}";
            await lockCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        var byKey = await ReadOneAsync(connection, transaction, "idempotency_key = @value",
            command => command.Parameters.Add("value", NpgsqlDbType.Text).Value = idempotencyKey, cancellationToken);
        if (byKey is not null)
        {
            if (byKey.AccountPaymentId != accountPaymentId)
                throw new AccountReceiptIdempotencyKeyReusedException(idempotencyKey);
            await transaction.CommitAsync(cancellationToken);
            return new AccountReceiptIssueResult(byKey, WasReplayed: true);
        }

        var byPayment = await ReadOneAsync(connection, transaction, "account_payment_id = @value",
            command => command.Parameters.Add("value", NpgsqlDbType.Uuid).Value = accountPaymentId, cancellationToken);
        if (byPayment is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new AccountReceiptIssueResult(byPayment, WasReplayed: true);
        }

        var payment = await _payments.GetAsync(accountPaymentId, cancellationToken)
            ?? throw new AccountPaymentNotFoundException(accountPaymentId);
        if (payment.Status != AccountPaymentStatus.Approved || payment.Evidence is null)
        {
            throw new AccountReceiptPaymentNotVerifiedException(new AccountReceiptReconciliationEvidence(
                payment.Id, payment.CustomerId, payment.Status, payment.Amount,
                payment.Status == AccountPaymentStatus.Unknown
                    ? "The payment result is unknown; it must be reconciled before a receipt is issued."
                    : $"The payment is {payment.Status}; only an Approved payment is receipted."));
        }

        var receipt = new AccountReceipt(
            Guid.NewGuid(), string.Empty, payment.CustomerId, payment.Id, payment.Amount, payment.CurrencyCode,
            idempotencyKey, issuedBy, DateTimeOffset.UtcNow);
        await using (var insert = new NpgsqlCommand(
            $"""
            INSERT INTO customer_account.account_receipts ({Columns})
            VALUES (@id, 'CT-' || lpad(nextval('customer_account.account_receipt_number_seq')::text, 8, '0'),
                    @customer_id, @payment_id, @amount, @currency_code, @idempotency_key, @issued_by, @issued_at)
            RETURNING receipt_number, issued_at;
            """, connection, transaction))
        {
            insert.Parameters.Add("id", NpgsqlDbType.Uuid).Value = receipt.Id;
            insert.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = receipt.CustomerId;
            insert.Parameters.Add("payment_id", NpgsqlDbType.Uuid).Value = receipt.AccountPaymentId;
            insert.Parameters.Add("amount", NpgsqlDbType.Numeric).Value = receipt.Amount;
            insert.Parameters.Add("currency_code", NpgsqlDbType.Char).Value = receipt.CurrencyCode;
            insert.Parameters.Add("idempotency_key", NpgsqlDbType.Text).Value = receipt.IdempotencyKey;
            insert.Parameters.Add("issued_by", NpgsqlDbType.Uuid).Value = (object?)receipt.IssuedBy ?? DBNull.Value;
            insert.Parameters.Add("issued_at", NpgsqlDbType.TimestampTz).Value = receipt.IssuedAt.UtcDateTime;
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            receipt = receipt with { ReceiptNumber = reader.GetString(0), IssuedAt = reader.GetFieldValue<DateTimeOffset>(1) };
        }

        await transaction.CommitAsync(cancellationToken);
        return new AccountReceiptIssueResult(receipt, WasReplayed: false);
    }

    public async Task<AccountReceipt?> GetByPaymentAsync(Guid accountPaymentId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadOneAsync(connection, null, "account_payment_id = @value",
            command => command.Parameters.Add("value", NpgsqlDbType.Uuid).Value = accountPaymentId, cancellationToken);
    }

    public async Task<IReadOnlyList<AccountReceipt>> GetByCustomerAsync(Guid customerId, int limit = 200, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        await using var command = _dataSource.CreateCommand(
            $"SELECT {Columns} FROM customer_account.account_receipts WHERE customer_id = @customer_id ORDER BY issued_at DESC, account_receipt_id LIMIT @limit;");
        command.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = customerId;
        command.Parameters.Add("limit", NpgsqlDbType.Integer).Value = limit;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var receipts = new List<AccountReceipt>();
        while (await reader.ReadAsync(cancellationToken))
            receipts.Add(Map(reader));
        return receipts;
    }

    private static async Task<AccountReceipt?> ReadOneAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, string where, Action<NpgsqlCommand> bind, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            $"SELECT {Columns} FROM customer_account.account_receipts WHERE {where};", connection, transaction);
        bind(command);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    private static AccountReceipt Map(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetString(1),
        reader.GetGuid(2),
        reader.GetGuid(3),
        reader.GetDecimal(4),
        reader.GetString(5),
        reader.GetString(6),
        reader.IsDBNull(7) ? null : reader.GetGuid(7),
        reader.GetFieldValue<DateTimeOffset>(8));
}
