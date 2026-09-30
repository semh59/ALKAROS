namespace ALKAROS.Privacy.RetentionExecution;

/// <summary>The kinds of business record a retention policy sets a window for; the names are stored as text.</summary>
public enum RetentionClass
{
    StaffAccount,
    OrderNotes,
    ReservationReason,
    CustomerProfile,
    Supplier,
}

public sealed record RetentionCandidate(RetentionClass DataClass, Guid SubjectId, DateTimeOffset DueSince);

public sealed record RetentionPlan(int PolicyVersion, DateTimeOffset AsOf, IReadOnlyList<RetentionCandidate> Candidates);

public sealed record RetentionRunResult(Guid RunId, RetentionPlan Plan);

public sealed record RetentionWorkItem(RetentionClass DataClass, Guid SubjectId, DateTimeOffset DueSince, int PolicyVersion);

public sealed class RetentionPolicyIncompleteException(IReadOnlyCollection<RetentionClass> missing)
    : InvalidOperationException($"A retention policy needs a window for every data class; missing: {string.Join(", ", missing)}.");

public sealed class RetentionHoldAlreadyActiveException(RetentionClass dataClass, Guid subjectId)
    : InvalidOperationException($"{dataClass} record {subjectId} is already under a legal hold.");

public sealed class RetentionHoldNotFoundException(Guid holdId)
    : InvalidOperationException($"Legal hold {holdId} does not exist or is already released.");

public sealed class RetentionSubjectHeldException(RetentionClass dataClass, Guid subjectId)
    : InvalidOperationException($"{dataClass} record {subjectId} is under a legal hold and cannot be completed.");
