using ALKAROS.SensitiveData;

namespace ALKAROS.Security.DataProtectionRetention.Tests.Fixtures;

/// <summary>
/// Delegates every call to a real <see cref="IRetentionSubjectStore"/> except
/// <see cref="PurgeAsync"/>, which always throws. Used to prove
/// <see cref="DeletionQueueProcessor"/> does not record an audit event for a
/// purge that failed.
/// </summary>
public sealed class PurgeThrowingRetentionSubjectStore : IRetentionSubjectStore
{
    private readonly IRetentionSubjectStore _inner;

    public PurgeThrowingRetentionSubjectStore(IRetentionSubjectStore inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public Task<Guid> InsertAsync(DataCategory category, SensitiveEnvelope envelope, bool legalHold, DateTimeOffset createdAt, CancellationToken cancellationToken)
        => _inner.InsertAsync(category, envelope, legalHold, createdAt, cancellationToken);

    public Task<RetentionSubjectRecord?> GetAsync(Guid id, CancellationToken cancellationToken)
        => _inner.GetAsync(id, cancellationToken);

    public Task<IReadOnlyList<RetentionSubjectRecord>> GetPendingAsync(CancellationToken cancellationToken, int limit = 1000)
        => _inner.GetPendingAsync(cancellationToken, limit);

    public Task<IReadOnlyList<Guid>> GetDeletionQueueAsync(CancellationToken cancellationToken, int limit = 1000)
        => _inner.GetDeletionQueueAsync(cancellationToken, limit);

    public Task MarkDisposedAsync(Guid id, DisposalAction action, int expectedRowVersion, CancellationToken cancellationToken)
        => _inner.MarkDisposedAsync(id, action, expectedRowVersion, cancellationToken);

    public Task ReplaceEnvelopeAsync(Guid id, SensitiveEnvelope envelope, int expectedRowVersion, CancellationToken cancellationToken)
        => _inner.ReplaceEnvelopeAsync(id, envelope, expectedRowVersion, cancellationToken);

    public Task SetLegalHoldAsync(Guid id, bool legalHold, int expectedRowVersion, CancellationToken cancellationToken)
        => _inner.SetLegalHoldAsync(id, legalHold, expectedRowVersion, cancellationToken);

    public Task PurgeAsync(Guid id, CancellationToken cancellationToken)
        => throw new InvalidOperationException("Simulated purge failure for test.");
}
