namespace ALKAROS.Security.DataProtectionRetention;

/// <summary>
/// V0-CMP-003's disposal matrix, transcribed verbatim from
/// evidence/v0/compliance/V0-CMP-003/kvkk-data-inventory.md section 1. This
/// matrix sits above <see cref="RetentionExecutionService"/> — the service
/// only dispatches on it, it never decides disposal policy itself.
/// </summary>
public static class DisposalMatrix
{
    /// <summary>
    /// Disposal action per category. Every <see cref="DataCategory"/> value
    /// must appear here; <see cref="RetentionCoverageVerifier"/> fails
    /// closed if one is missing.
    /// </summary>
    public static readonly IReadOnlyDictionary<DataCategory, DisposalAction> Actions =
        new Dictionary<DataCategory, DisposalAction>
        {
            [DataCategory.CustomerPii] = DisposalAction.Anonymize,
            [DataCategory.UserCredentials] = DisposalAction.Delete,
            [DataCategory.OrderNotes] = DisposalAction.Anonymize,
            [DataCategory.ProviderPayloads] = DisposalAction.Anonymize,
            [DataCategory.AuditLogs] = DisposalAction.Anonymize,
            [DataCategory.FiscalData] = DisposalAction.Retain,
            [DataCategory.InvoiceData] = DisposalAction.Retain,
            [DataCategory.SupplierData] = DisposalAction.Anonymize,
            [DataCategory.DeviceData] = DisposalAction.Delete,
        };

    /// <summary>
    /// The retention period the automatic, creation-time-based sweep
    /// (<see cref="RetentionExecutionService"/>) applies. <c>null</c> means
    /// the category's real-world trigger is not creation-time-based
    /// (User credentials: employment end + 1 year; Device data: device
    /// decommission) — out of this task's scope, disposal for those still
    /// requires an explicit, separately-triggered call.
    /// </summary>
    public static readonly IReadOnlyDictionary<DataCategory, TimeSpan?> RetentionPeriods =
        new Dictionary<DataCategory, TimeSpan?>
        {
            [DataCategory.CustomerPii] = TimeSpan.FromDays(365 * 10),
            [DataCategory.UserCredentials] = null,
            [DataCategory.OrderNotes] = TimeSpan.FromDays(365 * 5),
            [DataCategory.ProviderPayloads] = TimeSpan.FromDays(365 * 7),
            [DataCategory.AuditLogs] = TimeSpan.FromDays(365 * 10),
            [DataCategory.FiscalData] = null,
            [DataCategory.InvoiceData] = null,
            [DataCategory.SupplierData] = TimeSpan.FromDays(365 * 10),
            [DataCategory.DeviceData] = null,
        };

    public static DisposalAction ActionFor(DataCategory category) =>
        Actions.TryGetValue(category, out var action)
            ? action
            : throw new RetentionCategoryUnmappedException(category);

    public static TimeSpan? RetentionPeriodFor(DataCategory category) =>
        RetentionPeriods.TryGetValue(category, out var period)
            ? period
            : throw new RetentionCategoryUnmappedException(category);
}
