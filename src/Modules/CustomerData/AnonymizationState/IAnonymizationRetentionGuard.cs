namespace ALKAROS.CustomerData.AnonymizationState;

/// <summary>
/// Extension seam a financial-reference-owning module implements to report
/// why a customer cannot yet be anonymized (V14-CST-002's own Goal: never
/// wipe a customer whose account balance/invoice history is still legally
/// retained). This module does not register an implementation: the module
/// that owns the financial records does (V1-RMD-435:
/// `ALKAROS.CustomerAccounts.Retention.OutstandingBalanceRetentionGuard`,
/// registered by `CustomerAccountsBillChargesModule`). A composition without
/// such a module cannot resolve `CustomerAnonymizationService` at all, which
/// is the intended failure - an anonymization is never allowed by default.
/// </summary>
public interface IAnonymizationRetentionGuard
{
    /// <summary>Returns a human-readable reason the customer cannot be anonymized yet, or null if this guard finds no blocker.</summary>
    Task<string?> CheckAsync(Guid customerId, CancellationToken cancellationToken);
}
