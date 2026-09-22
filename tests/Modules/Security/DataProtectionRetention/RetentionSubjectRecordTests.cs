using ALKAROS.SensitiveData;
using ALKAROS.Security.DataProtectionRetention.Tests.Fixtures;
using Xunit;

namespace ALKAROS.Security.DataProtectionRetention.Tests;

public sealed class RetentionSubjectRecordTests
{
    private static SensitiveEnvelope SomeEnvelope() =>
        RetentionCryptoFixtures.ProtectTestPayload(RetentionCryptoFixtures.CreateProtector(), RetentionCryptoFixtures.OldKey);

    [Fact]
    public void EmptyIdIsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new RetentionSubjectRecord(Guid.Empty, DataCategory.ProviderPayloads, SomeEnvelope(), DateTimeOffset.UtcNow, false, null, null, 1));
    }

    [Fact]
    public void NonPositiveRowVersionIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RetentionSubjectRecord(Guid.NewGuid(), DataCategory.ProviderPayloads, SomeEnvelope(), DateTimeOffset.UtcNow, false, null, null, 0));
    }

    [Fact]
    public void DisposedAtWithoutActionIsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new RetentionSubjectRecord(Guid.NewGuid(), DataCategory.ProviderPayloads, SomeEnvelope(), DateTimeOffset.UtcNow, false, DateTimeOffset.UtcNow, null, 1));
    }

    [Fact]
    public void ActionWithoutDisposedAtIsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new RetentionSubjectRecord(Guid.NewGuid(), DataCategory.ProviderPayloads, SomeEnvelope(), DateTimeOffset.UtcNow, false, null, DisposalAction.Anonymize, 1));
    }

    [Fact]
    public void IsDisposedReflectsDisposedAt()
    {
        var live = new RetentionSubjectRecord(Guid.NewGuid(), DataCategory.ProviderPayloads, SomeEnvelope(), DateTimeOffset.UtcNow, false, null, null, 1);
        var disposed = new RetentionSubjectRecord(Guid.NewGuid(), DataCategory.ProviderPayloads, SomeEnvelope(), DateTimeOffset.UtcNow, false, DateTimeOffset.UtcNow, DisposalAction.Delete, 1);

        Assert.False(live.IsDisposed);
        Assert.True(disposed.IsDisposed);
    }
}
