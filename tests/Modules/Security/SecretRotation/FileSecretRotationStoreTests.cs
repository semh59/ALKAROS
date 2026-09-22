using Xunit;

namespace ALKAROS.Security.SecretRotation.Tests;

public sealed class FileSecretRotationStoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "alkaros-secret-rotation-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void FindReturnsNullWhenSecretHasNeverBeenSaved()
    {
        var store = new FileSecretRotationStore(_directory);

        Assert.Null(store.Find("db-password"));
    }

    [Fact]
    public void SaveThenFindRoundTripsAllVersionMetadata()
    {
        var store = new FileSecretRotationStore(_directory);
        var record = SecretRotationRecord.Initialize("db-password", Now)
            .Rotate(Now.AddDays(1), TimeSpan.FromHours(6))
            .Revoke(1, Now.AddDays(1).AddHours(7));

        store.Save(record);
        var restored = store.Find("db-password");

        Assert.NotNull(restored);
        Assert.Equal("db-password", restored!.SecretName);
        Assert.Equal(2, restored.Versions.Count);
        Assert.Equal(2, restored.ActiveVersion!.Version);
        var revoked = Assert.Single(restored.Versions, v => v.Version == 1);
        Assert.Equal(SecretVersionStatus.Revoked, revoked.Status);
        Assert.Equal(Now.AddDays(1).AddHours(7), revoked.RevokedAtUtc);
    }

    [Fact]
    public void SaveOverwritesPreviousStateForTheSameSecretName()
    {
        var store = new FileSecretRotationStore(_directory);
        store.Save(SecretRotationRecord.Initialize("db-password", Now));
        store.Save(SecretRotationRecord.Initialize("db-password", Now).Rotate(Now.AddDays(1), TimeSpan.FromHours(1)));

        var restored = store.Find("db-password");

        Assert.Equal(2, restored!.ActiveVersion!.Version);
    }

    [Fact]
    public void RejectsSecretNameThatIsNotSafeAsAFileName()
    {
        var store = new FileSecretRotationStore(_directory);

        Assert.Throws<ArgumentException>(() => store.Find("../escape"));
        Assert.Throws<ArgumentException>(() => store.Save(SecretRotationRecord.Initialize("Has Space", Now)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
