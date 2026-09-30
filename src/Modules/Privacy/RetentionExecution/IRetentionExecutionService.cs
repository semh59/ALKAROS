using Npgsql;

namespace ALKAROS.Privacy.RetentionExecution;

/// <summary>
/// Selects the business records whose retention window has elapsed and turns them into work items for the anonymization
/// workflow. Planning (the dry run) and executing use the same selection, so they always agree; a legal hold takes a
/// record out of both. Planning never writes; executing records the run, its items and one work item per record.
/// </summary>
public interface IRetentionExecutionService
{
    Task<RetentionPlan> PlanAsync(
        DateTimeOffset asOf, IReadOnlySet<Guid>? extraHeld = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the run and creates the work items of everything <see cref="PlanAsync"/> selects. A record that already has
    /// a work item is not selected again, so repeating a run adds nothing.
    /// </summary>
    Task<RetentionRunResult> ExecuteAsync(
        DateTimeOffset asOf, string requestedBy, IReadOnlySet<Guid>? extraHeld = null, CancellationToken cancellationToken = default);

    /// <summary>Publishes the next policy version; every data class needs a window.</summary>
    Task<int> PublishPolicyAsync(
        IReadOnlyDictionary<RetentionClass, int> years, string note, Guid publishedBy, CancellationToken cancellationToken = default);

    Task<Guid> PlaceHoldAsync(
        RetentionClass dataClass, Guid subjectId, string reason, Guid placedBy, CancellationToken cancellationToken = default);

    Task ReleaseHoldAsync(Guid holdId, Guid releasedBy, CancellationToken cancellationToken = default);

    /// <summary>The oldest pending work items of a class, leaving out records that are under a legal hold.</summary>
    Task<IReadOnlyList<RetentionWorkItem>> PendingAsync(
        RetentionClass dataClass, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks pending work items done, in <paramref name="transaction"/> when given so the anonymization and its completion
    /// commit together. Returns how many moved; refuses a record that is under a legal hold.
    /// </summary>
    Task<int> CompleteAsync(
        RetentionClass dataClass,
        IReadOnlyCollection<Guid> subjectIds,
        string completedBy,
        NpgsqlTransaction? transaction = null,
        CancellationToken cancellationToken = default);
}
