using ALKAROS.Secrets;
using ALKAROS.Secrets.Tests.Fixtures;
using Xunit;

namespace ALKAROS.Secrets.Tests.Resolution;

/// <summary>
/// Tests for the secret resolution boundary: authorized reads return the
/// value, missing secrets produce typed failures, and access is enforced
/// fail-closed before the provider is consulted.
/// </summary>
public static class SecretResolutionTests
{
    private const string SecretName = "Integration/ApiKey";
    private const string Accessor = "Integration";
    private const string SecretValue = "RESOLVE-VALUE-9f2c";

    [Fact]
    public static void ResolvesSecretForAllowedAccessor()
    {
        var (resolver, provider) = CreateResolver(allowedNames: new[] { SecretName });
        provider.Set(new SecretReference(SecretName), SecretValue);

        using var value = resolver.Resolve(new SecretReference(SecretName), Accessor);

        Assert.Equal(SecretValue, value.Value);
    }

    [Fact]
    public static void MissingSecretThrowsSecretNotFoundException()
    {
        var (resolver, _) = CreateResolver(allowedNames: new[] { SecretName });

        var ex = Assert.Throws<SecretNotFoundException>(() =>
            resolver.Resolve(new SecretReference(SecretName), Accessor));

        Assert.Contains(SecretName, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(SecretValue, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public static void DeniedAccessThrowsSecretAccessDeniedException()
    {
        var (resolver, _) = CreateResolver(allowedNames: Array.Empty<string>());

        var ex = Assert.Throws<SecretAccessDeniedException>(() =>
            resolver.Resolve(new SecretReference(SecretName), Accessor));

        Assert.Contains(Accessor, ex.Message, StringComparison.Ordinal);
        Assert.Contains(SecretName, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(SecretValue, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public static void AccessIsCheckedBeforeProviderLookup()
    {
        var provider = new InMemorySecretProvider();
        provider.Set(new SecretReference(SecretName), SecretValue);
        var resolver = new SecretResolver(
            provider,
            new AllowListSecretAccessPolicy(new Dictionary<string, IReadOnlyCollection<string>>
            {
                [Accessor] = Array.Empty<string>(),
            }));

        // The secret exists, yet the denied accessor must fail with the
        // access failure, proving the policy gates the provider lookup.
        Assert.Throws<SecretAccessDeniedException>(() =>
            resolver.Resolve(new SecretReference(SecretName), Accessor));
    }

    [Fact]
    public static void AccessIsPerSecretAndPerAccessor()
    {
        const string otherAccessor = "OtherModule";
        var provider = new InMemorySecretProvider();
        provider.Set(new SecretReference(SecretName), SecretValue);
        var resolver = new SecretResolver(
            provider,
            new AllowListSecretAccessPolicy(new Dictionary<string, IReadOnlyCollection<string>>
            {
                [Accessor] = new[] { SecretName },
                [otherAccessor] = Array.Empty<string>(),
            }));

        using var value = resolver.Resolve(new SecretReference(SecretName), Accessor);

        Assert.Equal(SecretValue, value.Value);
        Assert.Throws<SecretAccessDeniedException>(() =>
            resolver.Resolve(new SecretReference(SecretName), otherAccessor));
    }

    [Fact]
    public static void ResolveRejectsBlankAccessor()
    {
        var (resolver, _) = CreateResolver(allowedNames: new[] { SecretName });

        Assert.Throws<ArgumentException>(() =>
            resolver.Resolve(new SecretReference(SecretName), "   "));
    }

    private static (SecretResolver Resolver, InMemorySecretProvider Provider) CreateResolver(
        IReadOnlyCollection<string> allowedNames)
    {
        var provider = new InMemorySecretProvider();
        var resolver = new SecretResolver(
            provider,
            new AllowListSecretAccessPolicy(new Dictionary<string, IReadOnlyCollection<string>>
            {
                [Accessor] = allowedNames,
            }));
        return (resolver, provider);
    }
}
