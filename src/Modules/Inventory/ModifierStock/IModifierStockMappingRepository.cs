namespace ALKAROS.Inventory.ModifierStock;

/// <summary>
/// V1-RMD-152: persistence for what a modifier consumes. Mirrors
/// <see cref="ALKAROS.Inventory.StockMaster.IProductStockMappingRepository"/>
/// so the two BOM sides read the same way.
/// </summary>
public interface IModifierStockMappingRepository
{
    Task AddOrUpdateAsync(ModifierStockMapping mapping, CancellationToken ct = default);

    Task<IReadOnlyList<ModifierStockMapping>> GetByModifierIdAsync(Guid modifierId, CancellationToken ct = default);

    /// <summary>
    /// Every mapping for the given modifiers in one round trip — an order
    /// line can carry several extras and each of them can draw on several
    /// stock items, so consuming a line must not turn into a query per
    /// modifier.
    /// </summary>
    Task<IReadOnlyList<ModifierStockMapping>> GetByModifierIdsAsync(
        IReadOnlyCollection<Guid> modifierIds,
        CancellationToken ct = default);

    Task<bool> RemoveAsync(Guid modifierId, Guid stockItemId, CancellationToken ct = default);
}
