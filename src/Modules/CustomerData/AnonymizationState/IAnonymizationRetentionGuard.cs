namespace ALKAROS.CustomerData.AnonymizationState;

/// <summary>
/// Extension seam a financial-reference-owning module implements to report
/// why a customer cannot yet be anonymized (this task's own Goal: never
/// wipe a customer whose account balance/invoice history is still legally
/// retained). Neither owner exists as code yet (`V14-ACC` customer account
/// balances, `V14-INV` invoices are both still `Planned`), so
/// `CustomerDataModule` registers <see cref="NoKnownBlockingReferencesGuard"/>
/// as an honest placeholder - it never reports a block - until one of those
/// tasks replaces the registration with a real check. Mirrors the codebase's
/// existing "RequiresReconciliation placeholder" pattern (BankCard tender,
/// Faz 2) for the same reason: an unbuilt dependency gets an explicit,
/// documented stand-in, never a silent assumption.
/// </summary>
public interface IAnonymizationRetentionGuard
{
    /// <summary>Returns a human-readable reason the customer cannot be anonymized yet, or null if this guard finds no blocker.</summary>
    Task<string?> CheckAsync(Guid customerId, CancellationToken cancellationToken);
}

public sealed class NoKnownBlockingReferencesGuard : IAnonymizationRetentionGuard
{
    public Task<string?> CheckAsync(Guid customerId, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
}
