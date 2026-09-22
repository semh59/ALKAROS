using Xunit;

namespace ALKAROS.Security.SecretRotation.Tests;

public sealed class InMemorySecretRotationStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FindReturnsNullForAnUnsavedSecretName()
    {
        var store = new InMemorySecretRotationStore();

        Assert.Null(store.Find("db-password"));
    }

    [Fact]
    public void SaveThenFindReturnsTheSameRecord()
    {
        var store = new InMemorySecretRotationStore();
        var record = SecretRotationRecord.Initialize("db-password", Now);

        store.Save(record);

        Assert.Same(record, store.Find("db-password"));
    }

    [Fact]
    public void SecondSaveReplacesTheFirstForTheSameSecretName()
    {
        var store = new InMemorySecretRotationStore();
        store.Save(SecretRotationRecord.Initialize("db-password", Now));
        var rotated = SecretRotationRecord.Initialize("db-password", Now).Rotate(Now.AddDays(1), TimeSpan.FromHours(1));

        store.Save(rotated);

        Assert.Same(rotated, store.Find("db-password"));
    }
}
