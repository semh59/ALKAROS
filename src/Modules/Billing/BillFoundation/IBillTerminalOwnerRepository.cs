namespace ALKAROS.Billing.BillFoundation;

public interface IBillTerminalOwnerRepository
{
    /// <summary>
    /// Gives an unclaimed bill to the terminal (one atomic update, so concurrent claims have a single winner) and returns the
    /// terminal that owns the bill afterwards, or <c>null</c> when the bill does not exist.
    /// </summary>
    Task<Guid?> ClaimAsync(Guid billId, Guid terminalId, CancellationToken cancellationToken = default);

    /// <summary>The terminal that owns the bill; <c>null</c> when nobody owns it yet or the bill does not exist.</summary>
    Task<Guid?> FindOwnerAsync(Guid billId, CancellationToken cancellationToken = default);
}
