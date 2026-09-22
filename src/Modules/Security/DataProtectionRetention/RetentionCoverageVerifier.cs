namespace ALKAROS.Security.DataProtectionRetention;

/// <summary>
/// Verifies every <see cref="DataCategory"/> V0-CMP-003 defines has an
/// explicit <see cref="DisposalMatrix"/> entry, so a category can never be
/// silently unhandled by the retention engine.
/// </summary>
public static class RetentionCoverageVerifier
{
    public static RetentionCoverageReport Verify()
    {
        var unmapped = Enum.GetValues<DataCategory>()
            .Where(category => !DisposalMatrix.Actions.ContainsKey(category)
                || !DisposalMatrix.RetentionPeriods.ContainsKey(category))
            .ToList();

        return new RetentionCoverageReport(unmapped.Count == 0, unmapped);
    }
}
