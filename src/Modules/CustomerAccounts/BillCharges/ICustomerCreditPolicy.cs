namespace ALKAROS.CustomerAccounts.BillCharges;

/// <summary>Whether a customer may have a given amount charged to their account, and why not if denied.</summary>
public sealed record CreditPolicyResult(bool Approved, string? DeniedReason);

/// <summary>
/// Credit-worthiness check beyond plain eligibility (does the customer exist
/// and remain un-anonymized), evaluated by <see cref="AccountChargeHandler"/>
/// before anything is written.
/// </summary>
public interface ICustomerCreditPolicy
{
    Task<CreditPolicyResult> EvaluateAsync(Guid customerId, decimal amount, CancellationToken cancellationToken);
}

/// <summary>
/// V1-RMD-436 (V1-RMD-393 F-13): the production policy while the system has
/// no credit limit concept. Extending credit is a decision about a limit, and
/// without one no charge is approved - the account and the Bill stay
/// untouched and the denial carries a Turkish reason. A policy that reads
/// real per-customer limits replaces this registration when that decision is
/// made.
/// </summary>
public sealed class NoCreditLimitDefinedPolicy : ICustomerCreditPolicy
{
    public const string DeniedReason = "Müşteri için kredi limiti tanımlanmadığından cari hesaba borç yazılamaz.";

    public Task<CreditPolicyResult> EvaluateAsync(Guid customerId, decimal amount, CancellationToken cancellationToken) =>
        Task.FromResult(new CreditPolicyResult(false, DeniedReason));
}
