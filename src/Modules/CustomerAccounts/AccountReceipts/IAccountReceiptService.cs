namespace ALKAROS.CustomerAccounts.AccountReceipts;

/// <summary>V14-ACC-009: issues and reads receipts for verified, bill-independent account payments.</summary>
public interface IAccountReceiptService
{
    /// <summary>
    /// Issues the receipt for an Approved account payment. The same idempotency key, or a second request for the same
    /// payment, returns the receipt already issued. Throws <see cref="AccountReceiptPaymentNotVerifiedException"/> for
    /// a payment that is not Approved and <c>AccountPaymentNotFoundException</c> for an unknown one.
    /// </summary>
    Task<AccountReceiptIssueResult> IssueAsync(
        Guid accountPaymentId, string idempotencyKey, Guid? issuedBy, CancellationToken cancellationToken = default);

    Task<AccountReceipt?> GetByPaymentAsync(Guid accountPaymentId, CancellationToken cancellationToken = default);

    /// <summary>The customer's most recent receipts, newest first, at most <paramref name="limit"/>.</summary>
    Task<IReadOnlyList<AccountReceipt>> GetByCustomerAsync(Guid customerId, int limit = 200, CancellationToken cancellationToken = default);
}
