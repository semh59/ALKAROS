namespace ALKAROS.Security.DataProtectionRetention;

public sealed record RetentionCoverageReport(bool IsComplete, IReadOnlyList<DataCategory> UnmappedCategories);
