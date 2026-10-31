namespace ALKAROS.Menu.DailyMenuLifecycle;

public interface IDailyMenuItemLocator
{
    /// <summary>
    /// For each product sold from today's published (<c>Open</c>) daily menu, the id of its active menu item.
    /// A product is in at most one item of a menu, so the answer is never ambiguous; products not on the
    /// menu are absent.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, Guid>> FindPublishedItemIdsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken ct = default);
}
