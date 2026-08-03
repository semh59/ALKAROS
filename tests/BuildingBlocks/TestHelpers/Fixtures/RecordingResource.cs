using System.Data.Common;
using ALKAROS.Transactions;

namespace ALKAROS.TestHelpers;

/// <summary>
/// Records CommitAsync/RollbackAsync invocations and can fail the commit at
/// a configurable call index to simulate crash windows. Commit counters
/// persist across retry attempts of the same instance, so a transient
/// commit failure succeeds on the next attempt.
/// </summary>
public sealed class RecordingResource : ITransactionResource
{
    private readonly int _commitFailureAt;
    private readonly bool _transientFailure;
    private readonly bool _rollbackFails;
    private readonly List<string> _log;
    private int _commitCalls;

    public RecordingResource(
        string name,
        int commitFailureAt = 0,
        bool transientFailure = false,
        bool rollbackFails = false,
        List<string>? sharedLog = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Resource name must be non-empty.", nameof(name));
        if (commitFailureAt < 0)
            throw new ArgumentOutOfRangeException(
                nameof(commitFailureAt),
                "Commit failure index must be non-negative.");

        Name = name;
        _commitFailureAt = commitFailureAt;
        _transientFailure = transientFailure;
        _rollbackFails = rollbackFails;
        _log = sharedLog ?? new List<string>();
    }

    public string Name { get; }

    public bool CommitSucceeded { get; private set; }

    public bool RolledBack { get; private set; }

    public IReadOnlyList<string> Log => _log;

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        _commitCalls++;
        if (_commitCalls == _commitFailureAt)
        {
            _log.Add($"{Name}:commit-failed");
            throw _transientFailure
                ? new SimulatedTransientException($"{Name} commit failed")
                : new SimulatedFailureException($"{Name} commit failed");
        }

        CommitSucceeded = true;
        _log.Add($"{Name}:committed");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Runs the same recorded commit when the transaction scope owns a
    /// database session, so tests can exercise database-backed scopes.
    /// </summary>
    public Task CommitAsync(
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken)
        => CommitAsync(cancellationToken);

    public Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (_rollbackFails)
            throw new SimulatedFailureException($"{Name} rollback failed");

        RolledBack = true;
        _log.Add($"{Name}:rolled-back");
        return Task.CompletedTask;
    }
}