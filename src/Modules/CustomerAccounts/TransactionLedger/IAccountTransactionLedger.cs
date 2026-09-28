namespace ALKAROS.CustomerAccounts.TransactionLedger;

/// <summary>
/// Append-only persistence for <c>customer_account.account_transactions</c>
/// (migration 161). There is deliberately no update/delete method anywhere
/// on this interface - in-place edits are rejected structurally, not by
/// convention: a correction is a new, separately-recorded transaction (an
/// Adjustment or Refund), never a mutation of an existing row.
/// </summary>
public interface IAccountTransactionLedger
{
    /// <summary>
    /// Idempotent on (CustomerId, TransactionType, SourceReferenceType,
    /// SourceReferenceId): retrying the same source event (a webhook retry,
    /// an at-least-once outbox delivery) returns the already-recorded
    /// transaction instead of creating a duplicate.
    /// </summary>
    Task<AccountTransaction> RecordAsync(RecordAccountTransactionRequest request, CancellationToken cancellationToken = default);

    Task<AccountTransaction?> GetAsync(Guid transactionId, CancellationToken cancellationToken = default);

    /// <summary>All transactions for a customer, oldest first, bounded by <paramref name="limit"/>.</summary>
    Task<IReadOnlyList<AccountTransaction>> GetByCustomerAsync(Guid customerId, int limit = 1000, CancellationToken cancellationToken = default);
}
