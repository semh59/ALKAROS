using ALKAROS.Privacy.RetentionExecution;

namespace ALKAROS.Privacy.Anonymization;

/// <summary>
/// Turns the pending retention work items of one data class into anonymized records, store by store. Each finished store is
/// a checkpoint, so a run that stops halfway (an error, a crash) continues at the first store that is not done; a record is
/// only marked done in the retention list after every store was checked clean. Running it again changes nothing.
/// </summary>
public interface IAnonymizationWorkflow
{
    /// <param name="skip">Records to leave alone for now, for example orders an operator excluded.</param>
    Task<AnonymizationRunResult> RunAsync(
        RetentionClass dataClass,
        string actor,
        IReadOnlySet<Guid>? skip = null,
        int limit = 10_000,
        CancellationToken cancellationToken = default);
}
