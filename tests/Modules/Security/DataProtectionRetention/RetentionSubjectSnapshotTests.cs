using System.Reflection;
using ALKAROS.Security.DataProtectionRetention.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Security.DataProtectionRetention.Tests;

public sealed class RetentionSubjectSnapshotTests
{
    [Fact]
    public void SnapshotTypeCarriesNoEnvelopeOrCiphertextShapedProperty()
    {
        var properties = typeof(RetentionSubjectSnapshot).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        properties.Should().NotContain(p => p.PropertyType == typeof(byte[]));
        properties.Should().NotContain(p => p.Name.Contains("Envelope", StringComparison.Ordinal));
        properties.Should().NotContain(p => p.Name.Contains("Ciphertext", StringComparison.Ordinal));
    }

    [Fact]
    public void ToSnapshotCarriesOnlyMetadataFields()
    {
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(
            RetentionCryptoFixtures.CreateProtector(), RetentionCryptoFixtures.OldKey);
        var id = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        var record = new RetentionSubjectRecord(id, DataCategory.ProviderPayloads, envelope, createdAt, true, null, null, 1);

        var snapshot = record.ToSnapshot();

        snapshot.Id.Should().Be(id);
        snapshot.Category.Should().Be(DataCategory.ProviderPayloads);
        snapshot.CreatedAt.Should().Be(createdAt);
        snapshot.LegalHold.Should().BeTrue();
        snapshot.DisposedAt.Should().BeNull();
        snapshot.Action.Should().BeNull();
        snapshot.ToString().Should().NotContain("provider-response-body");
    }
}
