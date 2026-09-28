namespace ALKAROS.CustomerAccounts.BillCharges;

/// <summary>Base exception for account-charge domain errors (V14-ACC-003).</summary>
public abstract class AccountChargeException : Exception
{
    protected AccountChargeException(string message) : base(message) { }
}

/// <summary>Thrown when the Bill named by an account charge request does not exist.</summary>
public sealed class AccountChargeBillNotFoundException : AccountChargeException
{
    public AccountChargeBillNotFoundException(Guid billId)
        : base($"Bill '{billId}' was not found.")
    {
        BillId = billId;
    }

    public Guid BillId { get; }
}

/// <summary>
/// Thrown when an account charge names a customer that has already been
/// anonymized (V14-CST-001/002) - an anonymized customer can have no new
/// financial activity attached, the same guard V14-INV-002 will need before
/// issuing an invoice against a customer reference.
/// </summary>
public sealed class AccountChargeCustomerAnonymizedException : AccountChargeException
{
    public AccountChargeCustomerAnonymizedException(Guid customerId)
        : base($"Customer '{customerId}' is anonymized; a charge cannot be posted to their account.")
    {
        CustomerId = customerId;
    }

    public Guid CustomerId { get; }
}

/// <summary>
/// Thrown when `ICustomerCreditPolicy` denies the charge. Per this task's
/// own Acceptance evidence, this must happen BEFORE any Bill or account
/// write - neither is touched.
/// </summary>
public sealed class AccountChargeCreditPolicyDeniedException : AccountChargeException
{
    public AccountChargeCreditPolicyDeniedException(Guid customerId, decimal amount, string? reason)
        : base($"Credit policy denied a charge of '{amount}' to customer '{customerId}'{(reason is null ? "." : $": {reason}")}")
    {
        CustomerId = customerId;
        Amount = amount;
        Reason = reason;
    }

    public Guid CustomerId { get; }
    public decimal Amount { get; }
    public string? Reason { get; }
}
