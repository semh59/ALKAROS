using Npgsql;

namespace ALKAROS.CustomerAccounts.AccountPayments;

/// <summary>
/// V14-ACC-004: persistence for <see cref="AccountPayment"/> (<c>customer_account.account_payments</c>, migration
/// 165) and its append-only status history. The connection/transaction overloads let a method-specific owner write
/// the payment together with its own records in one transaction.
/// </summary>
public interface IAccountPaymentRepository
{
    /// <summary>
    /// Stores a new Requested payment, or returns the payment already stored under the same idempotency key. Throws
    /// <see cref="AccountPaymentIdempotencyKeyReusedException"/> when that key belongs to a different request.
    /// </summary>
    Task<AccountPayment> RequestAsync(AccountPayment payment, CancellationToken cancellationToken = default);

    Task<AccountPayment> RequestAsync(
        AccountPayment payment, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a transition made on a payment read at <paramref name="expectedRowVersion"/> and appends its history row.
    /// Throws <see cref="AccountPaymentConcurrencyException"/> when the row moved on, and
    /// <see cref="AccountPaymentEvidenceAlreadyLinkedException"/> when the evidence proves another payment.
    /// </summary>
    Task<AccountPayment> SaveTransitionAsync(
        AccountPayment updated, long expectedRowVersion, string? reason, Guid? changedBy, CancellationToken cancellationToken = default);

    Task<AccountPayment> SaveTransitionAsync(
        AccountPayment updated, long expectedRowVersion, string? reason, Guid? changedBy,
        NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default);

    Task<AccountPayment?> GetAsync(Guid accountPaymentId, CancellationToken cancellationToken = default);

    Task<AccountPayment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>The customer's most recent account payments, newest first, at most <paramref name="limit"/>.</summary>
    Task<IReadOnlyList<AccountPayment>> GetByCustomerAsync(Guid customerId, int limit = 500, CancellationToken cancellationToken = default);

    /// <summary>The payment's status history, oldest first (a payment has at most three transitions).</summary>
    Task<IReadOnlyList<AccountPaymentStatusChange>> GetHistoryAsync(Guid accountPaymentId, CancellationToken cancellationToken = default);
}
