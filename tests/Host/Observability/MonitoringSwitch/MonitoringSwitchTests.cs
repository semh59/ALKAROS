using System.Net;
using System.Net.Http.Json;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Observability.MonitoringSwitch;
using ALKAROS.Host.Observability.TraceCorrelation;
using ALKAROS.Observability.ErrorEvents;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Observability.MonitoringSwitch.Tests;

public sealed class MonitoringSwitchTests : IDisposable
{
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"alkaros-switch-{Guid.NewGuid():N}");
    private readonly string? _password = Environment.GetEnvironmentVariable("ALKAROS_DB_PASSWORD");

    public MonitoringSwitchTests()
    {
        Directory.CreateDirectory(_webRoot);
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<!doctype html><html><body>Test</body></html>");
        Environment.SetEnvironmentVariable("ALKAROS_DB_PASSWORD", "test-password");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ALKAROS_DB_PASSWORD", _password);
        Environment.SetEnvironmentVariable("Monitoring__Enabled", null);
        Directory.Delete(_webRoot, recursive: true);
    }

    private static async Task<HttpResponseMessage> SecureAsync(HttpClient client, HttpMethod method, string path, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "198.51.100.20");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        return await client.SendAsync(request);
    }

    private async Task<WebApplication> StartAsync(string? setting)
    {
        Environment.SetEnvironmentVariable("Monitoring__Enabled", setting);
        var app = DualScreenApplication.Build(new DualScreenOptions(
            "Host=localhost;Database=unused;Username=unused;Password=unused", _webRoot, "http://127.0.0.1:0", [System.Net.IPAddress.Loopback]));
        await app.StartAsync();
        return app;
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("nonsense", true)]
    [InlineData("", true)]
    public void SettingIsOnUnlessItIsExplicitlyFalse(string? value, bool expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(value is null ? [] : [new KeyValuePair<string, string?>("Monitoring:Enabled", value)])
            .Build();

        configuration.MonitoringEnabled().Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("true")]
    public async Task MonitoringIsOnByDefaultAndTagsEveryResponseWithACorrelationId(string? setting)
    {
        await using var app = await StartAsync(setting);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        using var response = await SecureAsync(client, HttpMethod.Get, "/api/not-found");
        using var browser = await SecureAsync(client, HttpMethod.Post, "/api/v1/telemetry/browser-errors", JsonContent.Create(new { eventId = "e1" }));

        app.Services.GetService<ErrorEventWriter>().Should().NotBeNull();
        app.Services.GetService<TraceSpanBuffer>().Should().NotBeNull();
        response.Headers.Contains("X-Correlation-Id").Should().BeTrue();
        browser.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SwitchedOffNoMonitoringServiceListenerOrEndpointExistsAndTheRestOfTheHostStillAnswers()
    {
        await using var app = await StartAsync("false");
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        using var response = await SecureAsync(client, HttpMethod.Get, "/api/not-found");
        using var browser = await SecureAsync(client, HttpMethod.Post, "/api/v1/telemetry/browser-errors", JsonContent.Create(new { eventId = "e1" }));
        using var offline = await SecureAsync(client, HttpMethod.Post, "/api/v1/telemetry/offline-queue", JsonContent.Create(new { length = 50 }));
        using var journeys = await SecureAsync(client, HttpMethod.Get, "/api/v1/management/observability/journeys/requests");
        using var errors = await SecureAsync(client, HttpMethod.Get, "/api/v1/management/observability/errors");

        app.Services.GetService<ErrorEventWriter>().Should().BeNull();
        app.Services.GetService<TraceSpanBuffer>().Should().BeNull();
        app.Services.GetService<ALKAROS.Host.Observability.ServerDetectorsDeep.DbActivityTap>().Should().BeNull();
        app.Services.GetService<ALKAROS.Host.Observability.OfflineQueueReport.ExternalCallTap>().Should().BeNull();
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.Contains("X-Correlation-Id").Should().BeFalse();
        new[] { browser, offline, journeys, errors }.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.NotFound);
    }
}
