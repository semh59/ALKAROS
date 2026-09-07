namespace ALKAROS.QrRelay.PublicGateway.Tests;

using System.Net;
using System.Net.Http.Json;
using ALKAROS.QrRelay.PublicGateway;
using Xunit;

/// <summary>
/// V12-QRT-001. No network call ever leaves this process — a fake handler
/// captures exactly what CloudflareApiClient sends and returns a canned
/// Cloudflare-shaped response, so these tests prove the request/response
/// shapes verified against Cloudflare's own API reference are what the
/// code actually produces and consumes.
/// </summary>
public sealed class CloudflareApiClientTests
{
    [Fact]
    public async Task CreateTunnelSendsTheDocumentedRequestShapeAndParsesTheResult()
    {
        var handler = new FakeHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://api.cloudflare.com/client/v4/accounts/acc-1/cfd_tunnel", request.RequestUri!.ToString());
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("test-token", request.Headers.Authorization!.Parameter);
            return JsonResponse(new
            {
                success = true,
                errors = Array.Empty<object>(),
                result = new { id = "tunnel-123", name = "restaurant-42" },
            });
        });
        var client = new CloudflareApiClient(new HttpClient(handler));

        var tunnel = await client.CreateTunnelAsync("test-token", "acc-1", "restaurant-42");

        Assert.Equal("tunnel-123", tunnel.Id);
        Assert.Equal("restaurant-42", tunnel.Name);
        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains("\"config_src\":\"cloudflare\"", handler.LastRequestBody);
    }

    [Fact]
    public async Task GetTunnelTokenReturnsTheBareStringResult()
    {
        var handler = new FakeHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://api.cloudflare.com/client/v4/accounts/acc-1/cfd_tunnel/tunnel-123/token", request.RequestUri!.ToString());
            return JsonResponse(new { success = true, errors = Array.Empty<object>(), result = "opaque-run-token-value" });
        });
        var client = new CloudflareApiClient(new HttpClient(handler));

        var token = await client.GetTunnelTokenAsync("test-token", "acc-1", "tunnel-123");

        Assert.Equal("opaque-run-token-value", token);
    }

    [Fact]
    public async Task CreateDnsRecordSendsACnameThatIsNeverProxied()
    {
        var handler = new FakeHandler((request, _) =>
        {
            Assert.Equal("https://api.cloudflare.com/client/v4/zones/zone-1/dns_records", request.RequestUri!.ToString());
            return JsonResponse(new { success = true, errors = Array.Empty<object>(), result = new { id = "record-1" } });
        });
        var client = new CloudflareApiClient(new HttpClient(handler));

        await client.CreateDnsRecordAsync("test-token", "zone-1", "restaurant-42", "tunnel-123.cfargotunnel.com");

        Assert.Contains("\"type\":\"CNAME\"", handler.LastRequestBody);
        Assert.Contains("\"content\":\"tunnel-123.cfargotunnel.com\"", handler.LastRequestBody);
        // V0-ARC-009: Cloudflare terminates public TLS but must not also
        // proxy/cache tunnel traffic — proxied must always be false.
        Assert.Contains("\"proxied\":false", handler.LastRequestBody);
    }

    [Fact]
    public async Task SetTunnelConfigurationSendsAHostnameRuleAndTheMandatoryCatchAll()
    {
        var handler = new FakeHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("https://api.cloudflare.com/client/v4/accounts/acc-1/cfd_tunnel/tunnel-123/configurations", request.RequestUri!.ToString());
            return JsonResponse(new { success = true, errors = Array.Empty<object>(), result = new { tunnel_id = "tunnel-123" } });
        });
        var client = new CloudflareApiClient(new HttpClient(handler));

        await client.SetTunnelConfigurationAsync("test-token", "acc-1", "tunnel-123", "restaurant-42.alkaros.app", "http://localhost:5080");

        Assert.Contains("\"hostname\":\"restaurant-42.alkaros.app\"", handler.LastRequestBody);
        Assert.Contains("\"service\":\"http://localhost:5080\"", handler.LastRequestBody);
        // The mandatory final catch-all rule — no hostname, service only.
        Assert.Contains("\"service\":\"http_status:404\"", handler.LastRequestBody);
    }

    [Fact]
    public async Task DeleteTunnelSendsADeleteToTheTunnelResource()
    {
        var handler = new FakeHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Delete, request.Method);
            Assert.Equal("https://api.cloudflare.com/client/v4/accounts/acc-1/cfd_tunnel/tunnel-123", request.RequestUri!.ToString());
            return JsonResponse(new { success = true, errors = Array.Empty<object>(), result = new { id = "tunnel-123" } });
        });
        var client = new CloudflareApiClient(new HttpClient(handler));

        await client.DeleteTunnelAsync("test-token", "acc-1", "tunnel-123");
    }

    [Fact]
    public async Task ARealCloudflareErrorEnvelopeSurfacesCloudflaresOwnMessage()
    {
        var handler = new FakeHandler((_, _) => JsonResponse(new
        {
            success = false,
            errors = new[] { new { code = 1003, message = "Invalid or missing zone id." } },
            result = (object?)null,
        }));
        var client = new CloudflareApiClient(new HttpClient(handler));

        var exception = await Assert.ThrowsAsync<CloudflareApiException>(
            () => client.CreateDnsRecordAsync("test-token", "bad-zone", "x", "y"));
        Assert.Contains("Invalid or missing zone id.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnparsableResponseBodyFailsClosedInsteadOfThrowingARawDeserializationError()
    {
        var handler = new FakeHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not json at all"),
        });
        var client = new CloudflareApiClient(new HttpClient(handler));

        await Assert.ThrowsAsync<CloudflareApiException>(
            () => client.GetTunnelTokenAsync("test-token", "acc-1", "tunnel-123"));
    }

    private static HttpResponseMessage JsonResponse(object body)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _respond;

        public string? LastRequestBody { get; private set; }

        public FakeHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return _respond(request, cancellationToken);
        }
    }
}
