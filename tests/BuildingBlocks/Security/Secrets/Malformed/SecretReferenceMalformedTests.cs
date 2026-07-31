using ALKAROS.Secrets;
using Xunit;

namespace ALKAROS.Secrets.Tests.Malformed;

/// <summary>
/// Tests for malformed secret references and malformed seed values: the
/// boundary rejects them before they can reach a provider or a resolver.
/// </summary>
public static class SecretReferenceMalformedTests
{
    [Fact]
    public static void SecretReferenceRejectsEmptyName()
    {
        Assert.Throws<ArgumentException>(() => new SecretReference(string.Empty));
    }

    [Fact]
    public static void SecretReferenceRejectsWhitespaceOnlyName()
    {
        Assert.Throws<ArgumentException>(() => new SecretReference("   "));
    }

    [Fact]
    public static void SecretReferenceRejectsEmbeddedWhitespace()
    {
        Assert.Throws<ArgumentException>(() => new SecretReference("Integration/Api Key"));
    }

    [Fact]
    public static void SecretReferenceKeepsGivenNameForStringOutput()
    {
        var reference = new SecretReference("Integration/ApiKey");

        Assert.Equal("Integration/ApiKey", reference.ToString());
        Assert.Equal("Integration/ApiKey", reference.Name);
    }

    [Fact]
    public static void SecretReferencesWithSameNameAreEqual()
    {
        var first = new SecretReference("Integration/ApiKey");
        var second = new SecretReference("Integration/ApiKey");

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public static void TestProviderRejectsBlankSecretValue()
    {
        var provider = new InMemorySecretProvider();

        Assert.Throws<ArgumentException>(() =>
            provider.Set(new SecretReference("Integration/ApiKey"), "   "));
    }
}
