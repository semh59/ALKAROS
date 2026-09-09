using ALKAROS.QrOrdering.RelayCredential;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ALKAROS.QrRelay.PublicGateway.Tests;

/// <summary>
/// V12-QRT-001: the CreateTunnel -&gt; GetTunnelToken -&gt; CreateDnsRecord -&gt;
/// SetTunnelConfiguration orchestration, exercised against a fake <see cref="ICloudflareApiClient"/>
/// (no real network call, no real Cloudflare token needed) and in-memory
/// fakes of the two config stores it reads. Persistence into
/// <see cref="IRelayTunnelStore"/> is exercised with a real fake too — the
/// real Postgres-backed store already has its own round-trip coverage in
/// <see cref="PostgresRelayTunnelStoreTests"/>.
/// </summary>
public sealed class RelayProvisioningServiceTests
{
    private static readonly RelayProviderConfig Config = new("account-abc", "zone-xyz", "alkaros.app", DateTimeOffset.UtcNow);

    [Fact]
    public async Task ChainsAllThreeCallsAndPersistsTheResultingTunnel()
    {
        var client = new FakeCloudflareApiClient();
        var credentials = new FakeCredentialStore("cf-real-token");
        var configStore = new FakeConfigStore(Config);
        var tunnelStore = new FakeTunnelStore();
        var service = new RelayProvisioningService(client, credentials, configStore, tunnelStore, NullLogger<RelayProvisioningService>.Instance);

        var result = await service.ProvisionAsync("sube1");

        Assert.Equal("sube1.alkaros.app", result.Hostname);
        Assert.Equal("cf-real-token", client.LastApiToken);
        Assert.Equal("account-abc", client.LastAccountId);
        Assert.Equal("alkaros-sube1", client.LastTunnelName);
        Assert.Equal("zone-xyz", client.LastZoneId);
        Assert.Equal("sube1.alkaros.app", client.LastDnsName);
        Assert.Equal("tunnel-id-1.cfargotunnel.com", client.LastDnsTarget);
        Assert.Equal("tunnel-id-1", client.LastConfiguredTunnelId);
        Assert.Equal("sube1.alkaros.app", client.LastConfiguredHostname);
        Assert.Equal("http://localhost:5080", client.LastConfiguredOriginService);
        Assert.Equal(("tunnel-id-1", "tunnel-run-token", "sube1.alkaros.app"), tunnelStore.Saved);
    }

    [Fact]
    public async Task WithNoApiTokenConfiguredThrowsARecognizableError()
    {
        var service = new RelayProvisioningService(
            new FakeCloudflareApiClient(), new FakeCredentialStore(null), new FakeConfigStore(Config), new FakeTunnelStore(), NullLogger<RelayProvisioningService>.Instance);

        var exception = await Assert.ThrowsAsync<RelayProvisioningException>(() => service.ProvisionAsync("sube1"));
        Assert.Contains("bağlantı anahtarı", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WithNoAccountConfigConfiguredThrowsARecognizableError()
    {
        var service = new RelayProvisioningService(
            new FakeCloudflareApiClient(), new FakeCredentialStore("cf-real-token"), new FakeConfigStore(null), new FakeTunnelStore(), NullLogger<RelayProvisioningService>.Instance);

        var exception = await Assert.ThrowsAsync<RelayProvisioningException>(() => service.ProvisionAsync("sube1"));
        Assert.Contains("hesap kimliği", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// V1-RMD-138: found by an independent audit (2026-09-09) — this test
    /// used to assert the exact opposite of what RelayProvisioningException's
    /// own doc comment promises ("never a raw Cloudflare error"): it
    /// required Cloudflare's raw English message to appear verbatim in the
    /// manager-facing exception. Now asserts the fix instead: a generic,
    /// actionable Turkish message that never contains the raw detail.
    /// </summary>
    [Fact]
    public async Task ACloudflareApiFailureSurfacesAsAGenericTurkishMessageNeverTheRawDetail()
    {
        var client = new FakeCloudflareApiClient { FailWith = "Invalid access token" };
        var service = new RelayProvisioningService(
            client, new FakeCredentialStore("cf-real-token"), new FakeConfigStore(Config), new FakeTunnelStore(),
            NullLogger<RelayProvisioningService>.Instance);

        var exception = await Assert.ThrowsAsync<RelayProvisioningException>(() => service.ProvisionAsync("sube1"));
        Assert.Contains("Cloudflare tünel kurulumu başarısız oldu", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Invalid access token", exception.Message, StringComparison.Ordinal);
    }

    private sealed class FakeCredentialStore : IRelayCredentialStore
    {
        private readonly string? _token;

        public FakeCredentialStore(string? token) => _token = token;

        public Task SaveCloudflareApiTokenAsync(string rawToken, Guid? updatedBy, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RelayCredentialStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string?> ResolveCloudflareApiTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_token);
    }

    private sealed class FakeConfigStore : IRelayProviderConfigStore
    {
        private readonly RelayProviderConfig? _config;

        public FakeConfigStore(RelayProviderConfig? config) => _config = config;

        public Task SaveAsync(string accountId, string zoneId, string baseDomain, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RelayProviderConfig?> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_config);
    }

    private sealed class FakeTunnelStore : IRelayTunnelStore
    {
        public (string TunnelId, string Token, string Hostname)? Saved { get; private set; }

        public Task SaveAsync(string tunnelId, string tunnelToken, string hostname, CancellationToken cancellationToken = default)
        {
            Saved = (tunnelId, tunnelToken, hostname);
            return Task.CompletedTask;
        }

        public Task<RelayTunnelInfo?> GetInfoAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string?> ResolveTunnelTokenAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeCloudflareApiClient : ICloudflareApiClient
    {
        public string? FailWith { get; set; }
        public string? LastApiToken { get; private set; }
        public string? LastAccountId { get; private set; }
        public string? LastTunnelName { get; private set; }
        public string? LastZoneId { get; private set; }
        public string? LastDnsName { get; private set; }
        public string? LastDnsTarget { get; private set; }
        public string? LastConfiguredTunnelId { get; private set; }
        public string? LastConfiguredHostname { get; private set; }
        public string? LastConfiguredOriginService { get; private set; }

        public Task<CloudflareTunnel> CreateTunnelAsync(string apiToken, string accountId, string name, CancellationToken cancellationToken = default)
        {
            if (FailWith is not null)
                throw new CloudflareApiException(FailWith);

            LastApiToken = apiToken;
            LastAccountId = accountId;
            LastTunnelName = name;
            return Task.FromResult(new CloudflareTunnel("tunnel-id-1", name));
        }

        public Task<string> GetTunnelTokenAsync(string apiToken, string accountId, string tunnelId, CancellationToken cancellationToken = default) =>
            Task.FromResult("tunnel-run-token");

        public Task CreateDnsRecordAsync(string apiToken, string zoneId, string subdomainLabel, string target, CancellationToken cancellationToken = default)
        {
            LastZoneId = zoneId;
            LastDnsName = subdomainLabel;
            LastDnsTarget = target;
            return Task.CompletedTask;
        }

        public Task SetTunnelConfigurationAsync(string apiToken, string accountId, string tunnelId, string hostname, string originService, CancellationToken cancellationToken = default)
        {
            LastConfiguredTunnelId = tunnelId;
            LastConfiguredHostname = hostname;
            LastConfiguredOriginService = originService;
            return Task.CompletedTask;
        }

        public Task DeleteTunnelAsync(string apiToken, string accountId, string tunnelId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
