namespace ALKAROS.CustomerAccounts.TransactionLedger;

/// <summary>The idempotency key already belongs to a ledger row with a different amount or note; the retry is a different request, not a replay.</summary>
public sealed class AccountTransactionIdempotencyConflictException(Guid existingTransactionId)
    : InvalidOperationException($"The source event is already recorded as account transaction {existingTransactionId} with a different amount or note.")
{
    public Guid ExistingTransactionId { get; } = existingTransactionId;
}
