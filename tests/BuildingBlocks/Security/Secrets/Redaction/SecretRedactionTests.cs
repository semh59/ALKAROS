using ALKAROS.Secrets;
using ALKAROS.Secrets.Tests.Fixtures;
using Xunit;

namespace ALKAROS.Secrets.Tests.Redaction;

/// <summary>
/// Tests for the redacted failure contract: a secret value must never appear
/// in string conversion, exception formatting or log-style interpolation.
/// </summary>
public static class SecretRedactionTests
{
    private const string SecretName = "Integration/ClientSecret";
    private const string SecretValue = "REDACTED-CLIENT-SECRET-c41d";
    private const string Accessor = "Integration";

    [Fact]
    public static void SecretValueToStringNeverContainsTheValue()
    {
        using var value = Resolve();

        Assert.Equal("<secret>", value.ToString());
        Assert.DoesNotContain(SecretValue, value.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public static void StringInterpolationOfSecretValueRendersRedacted()
    {
        using var value = Resolve();
        var logLine = $"authentication started with {value}";

        Assert.Equal("authentication started with <secret>", logLine);
        Assert.DoesNotContain(SecretValue, logLine, StringComparison.Ordinal);
    }

    [Fact]
    public static void MissingSecretExceptionFormattingNeverContainsTheValue()
    {
        var (resolver, provider) = CreateResolver();
        var ex = Assert.Throws<SecretNotFoundException>(() =>
            resolver.Resolve(new SecretReference(SecretName), Accessor));

        Assert.DoesNotContain(SecretValue, ex.ToString(), StringComparison.Ordinal);
        Assert.Contains(SecretName, ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public static void DeniedAccessExceptionFormattingNeverContainsTheValue()
    {
        var provider = new InMemorySecretProvider();
        provider.Set(new SecretReference(SecretName), SecretValue);
        var resolver = new SecretResolver(
            provider,
            new AllowListSecretAccessPolicy(new Dictionary<string, IReadOnlyCollection<string>>
            {
                [Accessor] = Array.Empty<string>(),
            }));

        var ex = Assert.Throws<SecretAccessDeniedException>(() =>
            resolver.Resolve(new SecretReference(SecretName), Accessor));

        Assert.DoesNotContain(SecretValue, ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public static void SecretValueValueAccessAfterDisposeThrows()
    {
        var value = Resolve();
        value.Dispose();

        Assert.Throws<ObjectDisposedException>(() => value.Value);
    }

    private static SecretValue Resolve()
    {
        var (resolver, provider) = CreateResolver();
        provider.Set(new SecretReference(SecretName), SecretValue);
        return resolver.Resolve(new SecretReference(SecretName), Accessor);
    }

    private static (SecretResolver Resolver, InMemorySecretProvider Provider) CreateResolver()
    {
        var provider = new InMemorySecretProvider();
        var resolver = new SecretResolver(
            provider,
            new AllowListSecretAccessPolicy(new Dictionary<string, IReadOnlyCollection<string>>
            {
                [Accessor] = new[] { SecretName },
            }));
        return (resolver, provider);
    }
}
