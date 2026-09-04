namespace ALKAROS.Identity.Authorization.Offline;

/// <summary>
/// Reads and writes <c>identity.offline_authority_budgets</c> and its lines.
/// One budget per device session; re-issuing for a session replaces the prior
/// budget and its lines.
/// </summary>
public interface IOfflineAuthorityBudgetRepository
{
    /// <summary>
    /// Stores a fresh budget for <paramref name="sessionId"/>, replacing any
    /// existing one. Returns the stored row.
    /// </summary>
    Task<OfflineAuthorityBudget> CreateAsync(
        Guid userId,
        Guid sessionId,
        IReadOnlyList<OfflineAuthorityBudgetLine> lines,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);

    /// <summary>The budget with <paramref name="budgetId"/>, or null.</summary>
    Task<OfflineAuthorityBudget?> GetAsync(Guid budgetId, CancellationToken cancellationToken = default);

    /// <summary>The current budget for <paramref name="sessionId"/>, or null.</summary>
    Task<OfflineAuthorityBudget?> GetBySessionAsync(
        Guid sessionId, CancellationToken cancellationToken = default);
}
