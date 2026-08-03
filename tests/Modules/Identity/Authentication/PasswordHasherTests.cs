using System.Globalization;
using ALKAROS.Identity.Authentication;
using Xunit;

namespace ALKAROS.Identity.Authentication.Tests;

public sealed class PasswordHasherTests
{
    private static readonly PasswordHasher Hasher = new();

    [Fact]
    public void HashProducesSelfDescribingSaltedFormat()
    {
        var encoded = Hasher.Hash("correct horse battery staple");

        var parts = encoded.Split('$');
        Assert.Equal(4, parts.Length);
        Assert.Equal("pbkdf2-sha256", parts[0]);
        Assert.Equal(PasswordHasher.DefaultIterations.ToString(CultureInfo.InvariantCulture), parts[1]);
        Assert.Equal(16, Convert.FromBase64String(parts[2]).Length);
        Assert.Equal(32, Convert.FromBase64String(parts[3]).Length);
    }

    [Fact]
    public void HashIsUniquePerCallForTheSamePassword()
    {
        var first = Hasher.Hash("same-password");
        var second = Hasher.Hash("same-password");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void VerifyAcceptsTheCorrectPassword()
    {
        var encoded = Hasher.Hash("correct-password");

        Assert.True(PasswordHasher.Verify("correct-password", encoded));
    }

    [Fact]
    public void VerifyRejectsTheWrongPassword()
    {
        var encoded = Hasher.Hash("correct-password");

        Assert.False(PasswordHasher.Verify("wrong-password", encoded));
    }

    [Fact]
    public void VerifyRejectsAnEmptyPassword()
    {
        var encoded = Hasher.Hash("correct-password");

        Assert.False(PasswordHasher.Verify("", encoded));
    }

    [Fact]
    public void VerifyRejectsMalformedEncodedHash()
    {
        Assert.False(PasswordHasher.Verify("password", "not-a-hash"));
        Assert.False(PasswordHasher.Verify("password", "pbkdf2-sha256$abc$salt$hash"));
        Assert.False(PasswordHasher.Verify("password", "pbkdf2-sha256$1000$AAAA$AAAA"));
    }

    [Fact]
    public void VerifyRejectsExcessiveIterationCount()
    {
        var encoded = $"pbkdf2-sha256${PasswordHasher.MaximumIterations + 1}$"
            + $"{Convert.ToBase64String(new byte[16])}${Convert.ToBase64String(new byte[32])}";

        Assert.False(PasswordHasher.Verify("password", encoded));
    }

    [Fact]
    public void VerifyAcceptsTheDummyHash()
    {
        Assert.True(PasswordHasher.Verify(PasswordHasher.DummyPassword, PasswordHasher.DummyHash));
    }

    [Fact]
    public void VerifyRejectsTamperedSalt()
    {
        var encoded = Hasher.Hash("correct-password");
        var parts = encoded.Split('$');
        var tampered = $"{parts[0]}${parts[1]}${parts[2][..^1]}A${parts[3]}";

        Assert.False(PasswordHasher.Verify("correct-password", tampered));
    }

    [Fact]
    public void VerifyRejectsTamperedHash()
    {
        var encoded = Hasher.Hash("correct-password");
        var parts = encoded.Split('$');
        var tampered = $"{parts[0]}${parts[1]}${parts[2]}${parts[3][..^1]}A";

        Assert.False(PasswordHasher.Verify("correct-password", tampered));
    }

    [Fact]
    public void VerifyUsesTheIterationCountEmbeddedInTheHash()
    {
        var lowIterationHasher = new PasswordHasher(iterations: 10_000);
        var encoded = lowIterationHasher.Hash("correct-password");

        Assert.True(PasswordHasher.Verify("correct-password", encoded));
        Assert.StartsWith("pbkdf2-sha256$10000$", encoded);
    }

    [Fact]
    public void ConstructorRejectsTooFewIterations()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PasswordHasher(iterations: 1_000));
    }

    [Fact]
    public void HashRejectsNullOrEmptyPassword()
    {
        Assert.Throws<ArgumentNullException>(() => Hasher.Hash(null!));
        Assert.Throws<ArgumentException>(() => Hasher.Hash(""));
    }
}
