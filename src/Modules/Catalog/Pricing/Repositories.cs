namespace ALKAROS.Catalog.Pricing;

/// <summary>
/// Read and append access to dated product price records. The authoritative
/// effective-price lookup is deterministic by timestamp and never returns
/// more than one price per (product, price_type, currency) at a given
/// timestamp (PDF III.4.4). In-place UpdateAsync/DeleteAsync were removed as
/// dead — a price change is a new dated row (append), and no caller ever
/// mutated one (independent audit, 2026-09-05, B6).
/// </summary>
public interface IPricingRepository
{
    Task<ProductPrice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductPrice?> GetEffectivePriceAsync(
        Guid productId,
        PriceType priceType,
        string currencyCode,
        DateTimeOffset at,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductPrice>> GetByProductAsync(Guid productId, CancellationToken cancellationToken = default);
    Task AddAsync(ProductPrice price, CancellationToken cancellationToken = default);
}