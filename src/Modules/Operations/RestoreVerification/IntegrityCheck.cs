namespace ALKAROS.Operations.RestoreVerification;

/// <summary>
/// A single named integrity query run against a freshly restored database —
/// e.g. "the bills table has the expected row count", "the latest fiscal
/// document id matches the source". The predicate decides pass/fail from
/// the scalar result; it never inspects raw restored content beyond what
/// the query itself selects.
/// </summary>
public sealed record IntegrityCheck(string Name, string SqlQuery, Func<object?, bool> Predicate);

/// <summary>The outcome of one <see cref="IntegrityCheck"/> against a restored database.</summary>
public sealed record IntegrityCheckResult(string Name, bool Passed, string? ObservedValue);
