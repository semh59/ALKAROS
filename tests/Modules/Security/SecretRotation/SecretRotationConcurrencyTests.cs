using Xunit;

namespace ALKAROS.Security.SecretRotation.Tests;

/// <summary>
/// Covers the optimistic-concurrency guard added to <see cref="ISecretRotationStore.Save"/>
/// after an independent audit found that a naive last-write-wins <c>Save</c>
/// let a concurrent Revoke be silently overwritten by a racing Rotate that
/// had read the same stale state (the leaked-secret-still-Overlap
/// scenario). Exercises both store implementations, since each enforces
/// the guard independently (in-memory dictionary vs. on-disk JSON file).
/// </summary>
public sealed class SecretRotationConcurrencyTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "alkaros-secret-rotation-concurrency-tests-" + Guid.NewGuid().ToString("N"));

    public static IEnumerable<object[]> Stores()
    {
        yield return new object[] { "in-memory" };
        yield return new object[] { "file" };
    }

    private ISecretRotationStore CreateStore(string kind) =>
        kind == "file" ? new FileSecretRotationStore(_directory) : new InMemorySecretRotationStore();

    [Theory]
    [MemberData(nameof(Stores))]
    public void SecondSaveOfAConcurrentFindMutateSaveRaceThrowsConcurrencyException(string kind)
    {
        var store = CreateStore(kind);
        var baseline = SecretRotationRecord.Initialize("db-password", Now);
        store.Save(baseline);

        // Two "sessions" both Find the same version-1 state, then each
        // mutates it independently and tries to Save. This is the race
        // shape the audit flagged: Find -> mutate -> Save with no version
        // check in between.
        var sessionA = store.Find("db-password")!;
        var sessionB = store.Find("db-password")!;

        var mutatedA = sessionA.Rotate(Now.AddMinutes(1), TimeSpan.FromHours(1));
        var mutatedB = sessionB.Revoke(1, Now.AddMinutes(1));

        store.Save(mutatedA); // first writer wins, moves store to version 2

        var exception = Assert.Throws<SecretRotationConcurrencyException>(() => store.Save(mutatedB));
        Assert.Equal("db-password", exception.SecretName);
    }

    [Theory]
    [MemberData(nameof(Stores))]
    public void ARevokeIsNeverSilentlyLostToAConcurrentRotateThatReadTheSameStaleState(string kind)
    {
        var store = CreateStore(kind);
        store.Save(SecretRotationRecord.Initialize("leaked-api-key", Now));

        // Admin opens the record to revoke the leaked version...
        var adminView = store.Find("leaked-api-key")!;
        // ...meanwhile an automated rotation job reads the same state...
        var rotationJobView = store.Find("leaked-api-key")!;

        // The admin's incident response lands first: kill the leaked
        // version immediately.
        var revoked = adminView.Revoke(1, Now.AddSeconds(5));
        store.Save(revoked);

        // The rotation job, still holding the pre-revoke state, tries to
        // save its own (unrelated) Rotate result. It must be rejected
        // rather than silently overwriting the revoke.
        var rotated = rotationJobView.Rotate(Now.AddSeconds(10), TimeSpan.FromHours(1));
        Assert.Throws<SecretRotationConcurrencyException>(() => store.Save(rotated));

        // The revoke must still be in effect - not clobbered by the
        // rejected concurrent write.
        var current = store.Find("leaked-api-key")!;
        var version1 = Assert.Single(current.Versions, v => v.Version == 1);
        Assert.Equal(SecretVersionStatus.Revoked, version1.Status);
        Assert.Null(current.ActiveVersion);
    }

    [Theory]
    [MemberData(nameof(Stores))]
    public void ManyTrueConcurrentSavesFromTheSameBaselineLeaveExactlyOneWinner(string kind)
    {
        var store = CreateStore(kind);
        store.Save(SecretRotationRecord.Initialize("db-password", Now));
        var baseline = store.Find("db-password")!;

        const int attempts = 8;
        var barrier = new Barrier(attempts);
        var exceptions = new SecretRotationConcurrencyException?[attempts];
        var threads = new Thread[attempts];

        for (var i = 0; i < attempts; i++)
        {
            var index = i;
            threads[index] = new Thread(() =>
            {
                var mutated = baseline.Revoke(1, Now.AddSeconds(index + 1));
                barrier.SignalAndWait();
                try
                {
                    store.Save(mutated);
                }
                catch (SecretRotationConcurrencyException ex)
                {
                    exceptions[index] = ex;
                }
            });
        }

        foreach (var thread in threads)
            thread.Start();
        foreach (var thread in threads)
            thread.Join();

        var failureCount = exceptions.Count(e => e is not null);
        Assert.Equal(attempts - 1, failureCount);

        // The store must still be in a valid, readable state - no
        // corruption from interleaved writes (this is what the
        // unique-temp-file-name fix on FileSecretRotationStore protects).
        var final = store.Find("db-password")!;
        Assert.Equal(2, final.Version);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
