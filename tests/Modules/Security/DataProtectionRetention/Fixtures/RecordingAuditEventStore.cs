using ALKAROS.Audit.EventStore;
using Npgsql;

namespace ALKAROS.Security.DataProtectionRetention.Tests.Fixtures;

/// <summary>In-memory IAuditEventStore test double — PostgresAuditEventStore's own correctness is V1-OPS-001's concern, not this task's.</summary>
public sealed class RecordingAuditEventStore : IAuditEventStore
{
    private readonly List<AuditEvent> _events = [];

    public IReadOnlyList<AuditEvent> Events => _events;

    public Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        auditEvent.Validate();
        _events.Add(auditEvent);
        return Task.CompletedTask;
    }

    public Task AppendAsync(
        AuditEvent auditEvent, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
        => AppendAsync(auditEvent, cancellationToken);

    public Task AppendBatchAsync(IEnumerable<AuditEvent> auditEvents, CancellationToken cancellationToken = default)
    {
        foreach (var auditEvent in auditEvents)
        {
            auditEvent.Validate();
            _events.Add(auditEvent);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEvent>> GetByAggregateAsync(string aggregateType, Guid aggregateId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AuditEvent>>(
            _events.Where(e => e.AggregateType == aggregateType && e.AggregateId == aggregateId).ToList());

    public Task<IReadOnlyList<AuditEvent>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AuditEvent>>(
            _events.Where(e => e.CorrelationId == correlationId).ToList());
}

/// <summary>An audit store whose every write fails, as a broken audit write would.</summary>
public sealed class ThrowingAuditEventStore : IAuditEventStore
{
    public Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("audit write failed");

    public Task AppendAsync(
        AuditEvent auditEvent, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("audit write failed");

    public Task AppendBatchAsync(IEnumerable<AuditEvent> auditEvents, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("audit write failed");

    public Task<IReadOnlyList<AuditEvent>> GetByAggregateAsync(string aggregateType, Guid aggregateId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<IReadOnlyList<AuditEvent>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
