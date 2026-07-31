using ALKAROS.Secrets;
using ALKAROS.Secrets.Tests.Fixtures;
using Xunit;

namespace ALKAROS.Secrets.Tests.Validation;

/// <summary>
/// Tests for the startup validation gate: every required secret must be
/// resolvable by the accessor before an integration starts, and failures
/// are typed and leak no values.
/// </summary>
public static class SecretStartupValidatorTests
{
    private const string Accessor = "Integration";
    private static readonly SecretReference ApiKey = new("Integration/ApiKey");
    private static readonly SecretReference ClientSecret = new("Integration/ClientSecret");

    [Fact]
    public static void ValidateSucceedsWhenAllRequiredSecretsAreResolvable()
    {
        var (validator, _, provider) = CreateValidator(denied: Array.Empty<string>());
        provider.Set(ApiKey, "API-KEY-VALUE-2d1f");
        provider.Set(ClientSecret, "CLIENT-SECRET-VALUE-7a03");

        validator.Validate(new[] { ApiKey, ClientSecret }, Accessor);
    }

    [Fact]
    public static void ValidateThrowsWhenRequiredSecretIsMissing()
    {
        var (validator, _, provider) = CreateValidator(denied: Array.Empty<string>());
        provider.Set(ApiKey, "API-KEY-VALUE-2d1f");

        var ex = Assert.Throws<SecretNotFoundException>(() =>
            validator.Validate(new[] { ApiKey, ClientSecret }, Accessor));

        Assert.Contains(ClientSecret.Name, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("API-KEY-VALUE-2d1f", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public static void ValidateThrowsWhenRequiredSecretIsDenied()
    {
        var (validator, _, provider) = CreateValidator(denied: new[] { ClientSecret.Name });
        provider.Set(ApiKey, "API-KEY-VALUE-2d1f");
        provider.Set(ClientSecret, "CLIENT-SECRET-VALUE-7a03");

        var ex = Assert.Throws<SecretAccessDeniedException>(() =>
            validator.Validate(new[] { ApiKey, ClientSecret }, Accessor));

        Assert.Contains(Accessor, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("CLIENT-SECRET-VALUE-7a03", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public static void ValidateIsRepeatableAndChangesNothingInTheProvider()
    {
        var (validator, resolver, provider) = CreateValidator(denied: Array.Empty<string>());
        provider.Set(ApiKey, "API-KEY-VALUE-2d1f");

        validator.Validate(new[] { ApiKey }, Accessor);
        validator.Validate(new[] { ApiKey }, Accessor);

        // The provider still serves the same value afterwards; validation
        // consumes and disposes values without side effects.
        using var value = resolver.Resolve(ApiKey, Accessor);
        Assert.Equal("API-KEY-VALUE-2d1f", value.Value);
    }

    private static (SecretStartupValidator Validator, SecretResolver Resolver, InMemorySecretProvider Provider) CreateValidator(
        IReadOnlyCollection<string> denied)
    {
        var provider = new InMemorySecretProvider();
        var allowed = new List<string> { ApiKey.Name, ClientSecret.Name };
        allowed.RemoveAll(denied.Contains);
        var resolver = new SecretResolver(
            provider,
            new AllowListSecretAccessPolicy(new Dictionary<string, IReadOnlyCollection<string>>
            {
                [Accessor] = allowed,
            }));
        return (new SecretStartupValidator(resolver), resolver, provider);
    }
}
