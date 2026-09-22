namespace ALKAROS.Operations.RestoreVerification.Tests.Fixtures;

/// <summary>Wraps a real factory and counts <see cref="ProvisionAsync"/> calls, to prove a corrupted artifact never reaches database provisioning.</summary>
public sealed class CountingIsolatedRestoreDatabaseFactory : IIsolatedRestoreDatabaseFactory
{
    private readonly IIsolatedRestoreDatabaseFactory _inner;

    public int ProvisionCallCount { get; private set; }

    public CountingIsolatedRestoreDatabaseFactory(IIsolatedRestoreDatabaseFactory inner)
    {
        _inner = inner;
    }

    public async Task<IIsolatedRestoreDatabase> ProvisionAsync(CancellationToken cancellationToken = default)
    {
        ProvisionCallCount++;
        return await _inner.ProvisionAsync(cancellationToken);
    }
}
