using System.Net;
using System.Net.Http.Json;
using ALKAROS.Invoicing.Qnb.CredentialRegistration;
using ALKAROS.Secrets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.QnbCredentialSettings.Tests;

/// <summary>
/// V14-QNB-006: mirrors `ALKAROS.Host.Experience.TokenTerminalSettings.Tests.
/// TokenTerminalSettingsHttpTests`'s exact shape.
/// </summary>
[Collection("QNB credential settings PostgreSQL HTTP")]
public sealed class QnbCredentialSettingsHttpTests : IAsyncLifetime
{
    private readonly QnbCredentialSettingsTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    private static SaveQnbCredentialHttpRequest SampleRequest(string password = "cs-test-secret") =>
        new("UserID", password, "3250566851");

    [Fact]
    public async Task NoSessionCookieIsUnauthorized()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.PostAsJsonAsync(CredentialPath(Guid.NewGuid()), SampleRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ACashierWithoutIntegrationsManageIsForbidden()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionWithPermissionsAsync(terminalId, "cashier", "orders.create");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(CredentialPath(terminalId), cookie, SampleRequest()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AManagerCanSaveACredentialAndStatusReflectsIt()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionWithPermissionsAsync(terminalId, "manager", "integrations.manage");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var beforeStatus = await client.SendAsync(JsonRequest(StatusPath(terminalId), cookie, method: HttpMethod.Get));
        var before = await beforeStatus.Content.ReadFromJsonAsync<QnbCredentialStatusResponse>();
        Assert.False(before!.Configured);

        using var saveResponse = await client.SendAsync(JsonRequest(CredentialPath(terminalId), cookie, SampleRequest("cs-real-secret-value")));
        Assert.Equal(HttpStatusCode.NoContent, saveResponse.StatusCode);

        using var afterStatus = await client.SendAsync(JsonRequest(StatusPath(terminalId), cookie, method: HttpMethod.Get));
        var after = await afterStatus.Content.ReadFromJsonAsync<QnbCredentialStatusResponse>();
        Assert.True(after!.Configured);
        Assert.NotNull(after.UpdatedAt);
        Assert.Equal("UserID", after.UserId);
        Assert.Equal("3250566851", after.VergiTcKimlikNo);
    }

    [Fact]
    public async Task AnEmptyPasswordIsRejected()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionWithPermissionsAsync(terminalId, "manager", "integrations.manage");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(CredentialPath(terminalId), cookie, SampleRequest("")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // V1-RMD-342 (independent 2026-09-26 audit, orta seviye bulgu): before this, any non-empty
    // string was accepted as the VKN with zero format check, client or server.
    [Theory]
    [InlineData("abc1234567")]
    [InlineData("123")]
    [InlineData("123456789012")]
    [InlineData("32505668-1")]
    public async Task AMalformedVergiTcKimlikNoIsRejected(string malformed)
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionWithPermissionsAsync(terminalId, "manager", "integrations.manage");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            CredentialPath(terminalId), cookie, new SaveQnbCredentialHttpRequest("UserID", "cs-test-secret", malformed)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var status = await client.SendAsync(JsonRequest(StatusPath(terminalId), cookie, method: HttpMethod.Get));
        Assert.False((await status.Content.ReadFromJsonAsync<QnbCredentialStatusResponse>())!.Configured);
    }

    // 11 haneli (gerçek kişi mükellef, TCKN) de geçerli kabul edilmeli.
    [Fact]
    public async Task AnElevenDigitVergiTcKimlikNoIsAccepted()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionWithPermissionsAsync(terminalId, "manager", "integrations.manage");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            CredentialPath(terminalId), cookie, new SaveQnbCredentialHttpRequest("UserID", "cs-test-secret", "32505668511")));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task TheSavedPasswordIsNeverReturnedByAnyEndpoint()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionWithPermissionsAsync(terminalId, "manager", "integrations.manage");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        const string secretValue = "cs-must-never-be-echoed-back";

        using var saveResponse = await client.SendAsync(JsonRequest(CredentialPath(terminalId), cookie, SampleRequest(secretValue)));
        var saveBody = await saveResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(secretValue, saveBody, StringComparison.Ordinal);

        using var statusResponse = await client.SendAsync(JsonRequest(StatusPath(terminalId), cookie, method: HttpMethod.Get));
        var statusBody = await statusResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(secretValue, statusBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestConnectionWithoutASavedCredentialAsksForOneFirstRatherThanCallingQnb()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionWithPermissionsAsync(terminalId, "manager", "integrations.manage");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(TestConnectionPath(terminalId), cookie, method: HttpMethod.Post));

        var body = await response.Content.ReadFromJsonAsync<QnbConnectionTestResponse>();
        Assert.False(body!.Success);
        Assert.Contains("kaydedilmelidir", body.Message);
    }

    [Fact]
    public async Task TestConnectionWithoutIntegrationsManageIsForbidden()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionWithPermissionsAsync(terminalId, "cashier", "orders.create");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(TestConnectionPath(terminalId), cookie, method: HttpMethod.Post));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// This test makes a REAL network call to QNB's own live test server
    /// (same endpoint `QnbSoapClientTests` proved the wire format against
    /// with a real `curl`). It asserts only on the SANITIZATION contract —
    /// never the raw QNB fault text — so it passes identically whether the
    /// real server answers "login failed" or the network is unreachable
    /// from wherever this runs; either way, the endpoint must never leak
    /// QNB's own error text (docs/UI_STYLE_GUIDE.md §3).
    /// </summary>
    [Fact]
    public async Task TestConnectionWithARealSavedCredentialCallsTheRealQnbServerAndNeverLeaksItsRawErrorText()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionWithPermissionsAsync(terminalId, "manager", "integrations.manage");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var saveResponse = await client.SendAsync(JsonRequest(CredentialPath(terminalId), cookie, SampleRequest()));
        Assert.Equal(HttpStatusCode.NoContent, saveResponse.StatusCode);

        using var response = await client.SendAsync(JsonRequest(TestConnectionPath(terminalId), cookie, method: HttpMethod.Post));

        var body = await response.Content.ReadFromJsonAsync<QnbConnectionTestResponse>();
        Assert.False(body!.Success);
        Assert.True(
            body.Message.Contains("hatalı olabilir") || body.Message.Contains("ulaşılamıyor"),
            $"Unexpected message: {body.Message}");
        Assert.DoesNotContain("EF0003", body.Message);
        Assert.DoesNotContain("Oturum açma", body.Message);
    }

    /// <summary>
    /// V1-RMD-347 (independent 2026-09-26 audit, orta seviye bulgu): before this, the production
    /// cutover for "Bağlantıyı Test Et"'s target URL existed only as a code comment - there was
    /// no mechanical way to point it anywhere but the hardcoded test-tenant URL. Proves the new
    /// ALKAROS_QNB_USER_SERVICE_URL override is actually wired end to end: a real local HTTP
    /// listener stands in for QNB (no fault in its response, so wsLogin/logout both succeed),
    /// and success is only possible if the override URL was genuinely used - the real QNB test
    /// server always answers placeholder credentials with a SOAP fault (see the sibling test
    /// above), never a bare success.
    /// </summary>
    [Fact]
    public async Task TestConnectionUsesTheEnvironmentOverrideUrlWhenOneIsSet()
    {
        var fakeQnbBuilder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        fakeQnbBuilder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var fakeQnb = fakeQnbBuilder.Build();
        fakeQnb.MapPost("/", () => Results.Text("<response/>", "text/xml"));
        await fakeQnb.StartAsync();
        var fakeQnbAddress = fakeQnb.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();

        Environment.SetEnvironmentVariable("ALKAROS_QNB_USER_SERVICE_URL", fakeQnbAddress);
        try
        {
            var terminalId = Guid.NewGuid();
            var cookie = await _database.SeedCashierSessionWithPermissionsAsync(terminalId, "manager", "integrations.manage");
            await using var app = await StartAsync();
            using var client = CreateClient(app);

            using var saveResponse = await client.SendAsync(JsonRequest(CredentialPath(terminalId), cookie, SampleRequest()));
            Assert.Equal(HttpStatusCode.NoContent, saveResponse.StatusCode);

            using var response = await client.SendAsync(JsonRequest(TestConnectionPath(terminalId), cookie, method: HttpMethod.Post));
            var body = await response.Content.ReadFromJsonAsync<QnbConnectionTestResponse>();

            Assert.True(body!.Success, $"Expected the override URL to be used (fake server, no fault -> success); got: {body.Message}");
            Assert.Equal("Bağlantı başarılı.", body.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ALKAROS_QNB_USER_SERVICE_URL", null);
            await fakeQnb.StopAsync();
        }
    }

    private static string CredentialPath(Guid terminalId) => $"/api/v1/terminals/{terminalId:D}/qnb-credential/";

    private static string StatusPath(Guid terminalId) => $"/api/v1/terminals/{terminalId:D}/qnb-credential/status";

    private static string TestConnectionPath(Guid terminalId) => $"/api/v1/terminals/{terminalId:D}/qnb-credential/test-connection";

    private static HttpRequestMessage JsonRequest<T>(string path, string cookie, T body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return request;
    }

    private static HttpRequestMessage JsonRequest(string path, string cookie, HttpMethod method)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return request;
    }

    private async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);

        var secretProvider = new InMemorySecretProvider();
        secretProvider.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
        builder.Services.AddSingleton<ISecretProvider>(secretProvider);
        builder.Services.AddQnbCredentialSettingsExperience();

        var app = builder.Build();
        app.MapQnbCredentialSettingsApi();
        await app.StartAsync();
        return app;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient { BaseAddress = new Uri(address) };
    }
}

[CollectionDefinition("QNB credential settings PostgreSQL HTTP", DisableParallelization = true)]
public sealed class QnbCredentialSettingsPostgresqlDefinition;
