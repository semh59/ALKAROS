namespace ALKAROS.CustomerAccounts.BillCharges;

/// <summary>Whether a customer may have a given amount charged to their account, and why not if denied.</summary>
public sealed record CreditPolicyResult(bool Approved, string? DeniedReason);

/// <summary>
/// Extension seam for credit-worthiness checks beyond plain eligibility
/// (does the customer exist and remain un-anonymized). This task's own Out
/// of scope explicitly excludes "genel credit scoring" (general credit
/// scoring), so <see cref="AlwaysApproveCreditPolicy"/> is an honest
/// placeholder - it never denies a charge - until a real policy (credit
/// limits, aging-based holds) is designed as its own task. Mirrors
/// `ALKAROS.CustomerData.AnonymizationState.IAnonymizationRetentionGuard`'s
/// exact rationale for the same kind of not-yet-built dependency.
/// </summary>
public interface ICustomerCreditPolicy
{
    Task<CreditPolicyResult> EvaluateAsync(Guid customerId, decimal amount, CancellationToken cancellationToken);
}

public sealed class AlwaysApproveCreditPolicy : ICustomerCreditPolicy
{
    public Task<CreditPolicyResult> EvaluateAsync(Guid customerId, decimal amount, CancellationToken cancellationToken) =>
        Task.FromResult(new CreditPolicyResult(true, null));
}
