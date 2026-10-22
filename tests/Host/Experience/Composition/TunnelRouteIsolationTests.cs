using System.Net;
using System.Net.Sockets;
using ALKAROS.Host.DualScreen;
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;
using Xunit;

namespace ALKAROS.Host.Experience.Composition.Tests;

/// <summary>
/// A tunnel edge reports the visitor's address and https in forwarded headers; the request must still be
/// recognised as relay traffic by its TCP peer and confined to the QR/NFC routes.
/// </summary>
public sealed class TunnelRouteIsolationTests
{
    private const string ConnectionString = "Host=127.0.0.1;Port=5432;Database=alkaros;Username=alkaros;Password=not-used";
    private static readonly IPNetwork LoopbackRelayNetwork = new(IPAddress.Parse("127.0.0.0"), 8);

    static TunnelRouteIsolationTests()
    {
        Environment.SetEnvironmentVariable(DualScreenApplication.KitchenStationEnvironmentVariable, "grill-1");
    }

    private static int FreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static DualScreenOptions Options(int port, IReadOnlyList<IPNetwork>? relayNetworks, string? qrWebRoot = null)
        => new(
            ConnectionString,
            WebRoot: string.Empty,
            $"http://127.0.0.1:{port}",
            TrustedProxies: [IPAddress.Loopback],
            ApiOnly: true,
            NfcTrustedNetworks: relayNetworks,
            QrWebRoot: qrWebRoot);

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, int port, string path, bool forwarded = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}{path}");
        if (forwarded)
        {
            request.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.9");
            request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        }

        return await client.SendAsync(request);
    }

    [Theory]
    [InlineData("/api/v1/terminals/00000000-0000-0000-0000-000000000001/catalog")]
    [InlineData("/api/v1/auth/session/current")]
    [InlineData("/hubs/customer-display")]
    [InlineData("/hubs/help-requests")]
    [InlineData("/hubs/waiter-order-status")]
    public async Task ARequestFromTheRelayNetworkCannotReachManagementOrCashierRoutesWhateverAddressTheEdgeForwards(string path)
    {
        var port = FreeLoopbackPort();
        await using var app = DualScreenApplication.Build(Options(port, [LoopbackRelayNetwork]));
        await app.StartAsync();
        using var client = new HttpClient();

        using var response = await GetAsync(client, port, path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/qr/menu")]
    [InlineData("/api/v1/nfc/tables/00000000-0000-0000-0000-000000000001/catalog")]
    public async Task TheQrAndNfcRoutesStillReachTheirEndpointsFromTheRelayNetwork(string path)
    {
        var port = FreeLoopbackPort();
        await using var app = DualScreenApplication.Build(Options(port, [LoopbackRelayNetwork]));
        await app.StartAsync();
        using var client = new HttpClient();

        using var response = await GetAsync(client, port, path);

        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ThePlainHttpHopFromTheRelayNetworkIsAcceptedForQrRoutesOnly()
    {
        var port = FreeLoopbackPort();
        await using var app = DualScreenApplication.Build(Options(port, [LoopbackRelayNetwork]));
        await app.StartAsync();
        using var client = new HttpClient();

        using var qr = await GetAsync(client, port, "/api/v1/qr/menu", forwarded: false);
        using var cashier = await GetAsync(client, port, "/api/v1/auth/session/current", forwarded: false);

        Assert.NotEqual(HttpStatusCode.BadRequest, qr.StatusCode);
        Assert.NotEqual(HttpStatusCode.NotFound, qr.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, cashier.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/auth/session/current", HttpStatusCode.Unauthorized)]
    [InlineData("/api/v1/qr/menu", HttpStatusCode.NotFound)]
    public async Task APeerOutsideTheRelayNetworkKeepsTheNormalRoutesAndHasNoQrRoutes(string path, HttpStatusCode expected)
    {
        var port = FreeLoopbackPort();
        var elsewhere = new IPNetwork(IPAddress.Parse("10.99.0.0"), 16);
        await using var app = DualScreenApplication.Build(Options(port, [elsewhere]));
        await app.StartAsync();
        using var client = new HttpClient();

        using var response = await GetAsync(client, port, path);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task ARefusalCarriesTheBaselineSecurityHeaders()
    {
        var port = FreeLoopbackPort();
        await using var app = DualScreenApplication.Build(Options(port, relayNetworks: null));
        await app.StartAsync();
        using var client = new HttpClient();

        using var response = await GetAsync(client, port, "/api/v1/auth/session/current", forwarded: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertBaselineHeaders(response);
    }

    [Fact]
    public async Task ThePublicQrPageCarriesTheBaselineSecurityHeaders()
    {
        var qrWebRoot = Path.Combine(Path.GetTempPath(), "alkaros-tunnel-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(qrWebRoot);
        File.WriteAllText(Path.Combine(qrWebRoot, "index.html"), "<!doctype html><title>qr</title>");
        try
        {
            var port = FreeLoopbackPort();
            await using var app = DualScreenApplication.Build(Options(port, [LoopbackRelayNetwork], qrWebRoot));
            await app.StartAsync();
            using var client = new HttpClient();

            using var response = await GetAsync(client, port, "/qr/");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            AssertBaselineHeaders(response);
        }
        finally
        {
            Directory.Delete(qrWebRoot, recursive: true);
        }
    }

    private static void AssertBaselineHeaders(HttpResponseMessage response)
    {
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.Contains("frame-ancestors 'none'", Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
    }
}
