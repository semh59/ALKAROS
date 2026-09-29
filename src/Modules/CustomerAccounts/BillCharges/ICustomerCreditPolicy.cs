namespace ALKAROS.CustomerAccounts.BillCharges;

/// <summary>Whether a customer may have a given amount charged to their account, and why not if denied.</summary>
public sealed record CreditPolicyResult(bool Approved, string? DeniedReason);

/// <summary>
/// Credit-worthiness check beyond plain eligibility (does the customer exist
/// and remain un-anonymized), evaluated by <see cref="AccountChargeHandler"/>
/// under its per-customer lock before anything is written. The production
/// implementation is `ALKAROS.CustomerAccounts.CreditTerms.CreditTermsCreditPolicy`
/// (V1-RMD-440).
/// </summary>
public interface ICustomerCreditPolicy
{
    Task<CreditPolicyResult> EvaluateAsync(Guid customerId, decimal amount, CancellationToken cancellationToken);
}
