using System.Net;
using System.Net.Http.Json;
using ALKAROS.Payments.Token.TerminalCredential;
using ALKAROS.Secrets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.TokenTerminalSettings.Tests;

/// <summary>
/// V13-HUG-005: mirrors `ALKAROS.Host.Experience.RelaySettings.Tests.
/// RelaySettingsHttpTests`'s exact shape — only a manager
/// (`integrations.manage`) may register the Token/Beko terminal's
/// credential from the interface.
/// </summary>
[Collection("Token terminal settings PostgreSQL HTTP")]
public sealed class TokenTerminalSettingsHttpTests : IAsyncLifetime
{
    private readonly TokenTerminalSettingsTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    private static SaveTokenTerminalCredentialHttpRequest SampleRequest(string clientSecret = "cs-test-secret") =>
        new("13e5862b-1328-47dd-887c-d9ca6cb4375c", "b81bb869-d45c-43df-a078-9337900ff84e", "AV0000111044", "cid-example", clientSecret);

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
        var before = await beforeStatus.Content.ReadFromJsonAsync<TokenTerminalCredentialStatusResponse>();
        Assert.False(before!.Configured);

        using var saveResponse = await client.SendAsync(JsonRequest(CredentialPath(terminalId), cookie, SampleRequest("cs-real-secret-value")));
        Assert.Equal(HttpStatusCode.NoContent, saveResponse.StatusCode);

        using var afterStatus = await client.SendAsync(JsonRequest(StatusPath(terminalId), cookie, method: HttpMethod.Get));
        var after = await afterStatus.Content.ReadFromJsonAsync<TokenTerminalCredentialStatusResponse>();
        Assert.True(after!.Configured);
        Assert.NotNull(after.UpdatedAt);
        Assert.Equal("13e5862b-1328-47dd-887c-d9ca6cb4375c", after.MerchantId);
        Assert.Equal("b81bb869-d45c-43df-a078-9337900ff84e", after.BranchId);
        Assert.Equal("AV0000111044", after.TerminalId);
        Assert.Equal("cid-example", after.ClientId);
    }

    [Fact]
    public async Task AnEmptyClientSecretIsRejected()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionWithPermissionsAsync(terminalId, "manager", "integrations.manage");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(CredentialPath(terminalId), cookie, SampleRequest("")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // V1-RMD-342 (independent 2026-09-26 audit, orta seviye bulgu): before this, any non-empty
    // string was accepted as the terminal id with zero format check, client or server.
    [Theory]
    [InlineData("111044")]
    [InlineData("XY0000111044")]
    [InlineData("AV")]
    [InlineData("AV12A4")]
    public async Task AMalformedTerminalIdIsRejected(string malformed)
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionWithPermissionsAsync(terminalId, "manager", "integrations.manage");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            CredentialPath(terminalId), cookie,
            new SaveTokenTerminalCredentialHttpRequest(
                "13e5862b-1328-47dd-887c-d9ca6cb4375c", "b81bb869-d45c-43df-a078-9337900ff84e", malformed, "cid-example", "cs-test-secret")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var status = await client.SendAsync(JsonRequest(StatusPath(terminalId), cookie, method: HttpMethod.Get));
        Assert.False((await status.Content.ReadFromJsonAsync<TokenTerminalCredentialStatusResponse>())!.Configured);
    }

    // Lowercase 'av'/'at' prefixes are accepted too - a manager retyping the physical
    // label's own casing exactly is not something the check should depend on.
    [Fact]
    public async Task ALowercasePrefixTerminalIdIsAccepted()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionWithPermissionsAsync(terminalId, "manager", "integrations.manage");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            CredentialPath(terminalId), cookie,
            new SaveTokenTerminalCredentialHttpRequest(
                "13e5862b-1328-47dd-887c-d9ca6cb4375c", "b81bb869-d45c-43df-a078-9337900ff84e", "at0000111044", "cid-example", "cs-test-secret")));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task TheSavedClientSecretIsNeverReturnedByAnyEndpoint()
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

    private static string CredentialPath(Guid terminalId) => $"/api/v1/terminals/{terminalId:D}/token-credential/";

    private static string StatusPath(Guid terminalId) => $"/api/v1/terminals/{terminalId:D}/token-credential/status";

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

        // Registered BEFORE AddTokenTerminalSettingsExperience() so its own
        // TryAddSingleton<ISecretProvider, EnvironmentVariableSecretProvider>()
        // is a no-op here — exactly as RelaySettingsHttpTests does for its
        // own experience method.
        var secretProvider = new InMemorySecretProvider();
        secretProvider.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
        builder.Services.AddSingleton<ISecretProvider>(secretProvider);
        builder.Services.AddTokenTerminalSettingsExperience();

        var app = builder.Build();
        app.MapTokenTerminalSettingsApi();
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

[CollectionDefinition("Token terminal settings PostgreSQL HTTP", DisableParallelization = true)]
public sealed class TokenTerminalSettingsPostgresqlDefinition;
