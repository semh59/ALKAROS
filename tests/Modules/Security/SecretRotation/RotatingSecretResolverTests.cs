using ALKAROS.Secrets;
using Xunit;

namespace ALKAROS.Security.SecretRotation.Tests;

public sealed class RotatingSecretResolverTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    private sealed class AllowAllAccessPolicy : ISecretAccessPolicy
    {
        public bool IsAllowed(string accessor, SecretReference reference) => true;
    }

    private sealed class DenyAccessorPolicy : ISecretAccessPolicy
    {
        private readonly string _deniedAccessor;
        public DenyAccessorPolicy(string deniedAccessor) => _deniedAccessor = deniedAccessor;
        public bool IsAllowed(string accessor, SecretReference reference) => accessor != _deniedAccessor;
    }

    private static (RotatingSecretResolver Resolver, InMemorySecretRotationStore Store, InMemorySecretProvider Provider) Build(
        ISecretAccessPolicy? policy = null)
    {
        var store = new InMemorySecretRotationStore();
        var provider = new InMemorySecretProvider();
        var resolver = new SecretResolver(provider, policy ?? new AllowAllAccessPolicy());
        return (new RotatingSecretResolver(store, resolver), store, provider);
    }

    [Fact]
    public void ResolvesActiveVersionWhenProviderHasIt()
    {
        var (resolver, store, provider) = Build();
        store.Save(SecretRotationRecord.Initialize("db-password", Now));
        provider.Set(SecretVersionReferenceNaming.ReferenceFor("db-password", 1), "s3cr3t-v1");

        using var value = resolver.Resolve("db-password", "billing-module");

        Assert.Equal("s3cr3t-v1", value.Value);
    }

    [Fact]
    public void FallsBackToNewestOverlapVersionWhenActiveValueIsMissingFromProvider()
    {
        var (resolver, store, provider) = Build();
        var rotation = SecretRotationRecord.Initialize("db-password", Now)
            .Rotate(Now.AddDays(1), TimeSpan.FromHours(6));
        store.Save(rotation);
        // Only the overlap (previously active) version has a real value in
        // the provider - simulates the new version's credential not having
        // been injected into the environment yet.
        provider.Set(SecretVersionReferenceNaming.ReferenceFor("db-password", 1), "s3cr3t-v1");

        using var value = resolver.Resolve("db-password", "billing-module");

        Assert.Equal("s3cr3t-v1", value.Value);
    }

    [Fact]
    public void ThrowsUnavailableWhenNoCandidateVersionResolves()
    {
        var (resolver, store, _) = Build();
        store.Save(SecretRotationRecord.Initialize("db-password", Now));

        Assert.Throws<SecretRotationUnavailableException>(() => resolver.Resolve("db-password", "billing-module"));
    }

    [Fact]
    public void ThrowsUnavailableWhenSecretHasNoRotationState()
    {
        var (resolver, _, _) = Build();

        Assert.Throws<SecretRotationUnavailableException>(() => resolver.Resolve("unknown-secret", "billing-module"));
    }

    [Fact]
    public void AccessDeniedIsNeverRetriedAgainstAnotherVersion()
    {
        var (resolver, store, provider) = Build(new DenyAccessorPolicy("blocked-module"));
        var rotation = SecretRotationRecord.Initialize("db-password", Now)
            .Rotate(Now.AddDays(1), TimeSpan.FromHours(6));
        store.Save(rotation);
        provider.Set(SecretVersionReferenceNaming.ReferenceFor("db-password", 1), "s3cr3t-v1");
        provider.Set(SecretVersionReferenceNaming.ReferenceFor("db-password", 2), "s3cr3t-v2");

        Assert.Throws<SecretAccessDeniedException>(() => resolver.Resolve("db-password", "blocked-module"));
    }
}
