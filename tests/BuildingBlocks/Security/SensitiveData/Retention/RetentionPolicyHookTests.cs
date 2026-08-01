using ALKAROS.SensitiveData;
using Xunit;

namespace ALKAROS.SensitiveData.Tests.Retention;

/// <summary>
/// Tests for the retention-policy hook: the boundary exposes the stored
/// timestamp and the hook decides expiry from the configured policy.
/// </summary>
public static class RetentionPolicyHookTests
{
    private static readonly SensitiveEnvelope Envelope = new(
        new Dictionary<string, SensitiveCategory>
        {
            ["order-id"] = SensitiveCategory.Public,
        },
        new EnvelopeCiphertext(
            "Test/EnvelopeKey",
            new byte[12],
            new byte[16],
            new byte[16]),
        DateTimeOffset.UtcNow.AddDays(-30));

    [Fact]
    public static void FreshEnvelopeIsNotExpired()
    {
        var envelope = Envelope with { CreatedAt = DateTimeOffset.UtcNow.AddDays(-5) };
        var hook = new MaxAgeRetentionPolicyHook(TimeSpan.FromDays(10));

        Assert.False(hook.IsExpired(envelope, DateTimeOffset.UtcNow));
    }

    [Fact]
    public static void OldEnvelopeIsExpired()
    {
        var envelope = Envelope with { CreatedAt = DateTimeOffset.UtcNow.AddDays(-100) };
        var hook = new MaxAgeRetentionPolicyHook(TimeSpan.FromDays(90));

        Assert.True(hook.IsExpired(envelope, DateTimeOffset.UtcNow));
    }

    [Fact]
    public static void EnvelopeAtExactlyTheMaxAgeIsNotExpired()
    {
        var createdAt = DateTimeOffset.UtcNow.AddDays(-30);
        var envelope = Envelope with { CreatedAt = createdAt };
        var hook = new MaxAgeRetentionPolicyHook(TimeSpan.FromDays(30));

        // Exactly at the max age: only strictly older counts as expired.
        Assert.False(hook.IsExpired(envelope, createdAt + TimeSpan.FromDays(30)));
    }

    [Fact]
    public static void MaxAgeRejectsNegativeDuration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MaxAgeRetentionPolicyHook(TimeSpan.FromDays(-1)));
    }
}
