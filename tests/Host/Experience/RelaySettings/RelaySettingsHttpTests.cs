using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ALKAROS.QrOrdering.RelayCredential;
using ALKAROS.Secrets;
using ALKAROS.SensitiveData;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.RelaySettings.Tests;

/// <summary>
/// V14-QRT-003: only a manager may configure the relay provider credential
/// from the interface — supervisor/cashier/waiter are all refused, matching
/// `ApplicationPermissions.IntegrationsManage` being the first
/// manager-exclusive grant in the catalog.
/// </summary>
[Collection("Relay settings PostgreSQL HTTP")]
public sealed class RelaySettingsHttpTests : IAsyncLifetime
{
    private readonly RelaySettingsTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task NoSessionCookieIsUnauthorized()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.PostAsJsonAsync(CredentialPath(Guid.NewGuid()), new SaveRelayCredentialRequest("cf-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ACashierWithoutIntegrationsManageIsForbidden()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionWithPermissionsAsync(terminalId, "cashier", "orders.create");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            CredentialPath(terminalId), cookie, new SaveRelayCredentialRequest("cf-token")));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ASupervisorWithoutIntegrationsManageIsAlsoForbidden()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionWithPermissionsAsync(terminalId, "supervisor", "bills.void", "bills.comp");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            CredentialPath(terminalId), cookie, new SaveRelayCredentialRequest("cf-token")));

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
        var before = await beforeStatus.Content.ReadFromJsonAsync<RelayCredentialStatusResponse>();
        Assert.False(before!.Configured);

        using var saveResponse = await client.SendAsync(JsonRequest(
            CredentialPath(terminalId), cookie, new SaveRelayCredentialRequest("cf-real-token-value")));
        Assert.Equal(HttpStatusCode.NoContent, saveResponse.StatusCode);

        using var afterStatus = await client.SendAsync(JsonRequest(StatusPath(terminalId), cookie, method: HttpMethod.Get));
        var after = await afterStatus.Content.ReadFromJsonAsync<RelayCredentialStatusResponse>();
        Assert.True(after!.Configured);
        Assert.NotNull(after.UpdatedAt);
    }

    [Fact]
    public async Task AnEmptyTokenIsRejected()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionWithPermissionsAsync(terminalId, "manager", "integrations.manage");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            CredentialPath(terminalId), cookie, new SaveRelayCredentialRequest("")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TheSavedTokenIsNeverReturnedByAnyEndpoint()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionWithPermissionsAsync(terminalId, "manager", "integrations.manage");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        const string secretValue = "cf-must-never-be-echoed-back";

        using var saveResponse = await client.SendAsync(JsonRequest(
            CredentialPath(terminalId), cookie, new SaveRelayCredentialRequest(secretValue)));
        var saveBody = await saveResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(secretValue, saveBody, StringComparison.Ordinal);

        using var statusResponse = await client.SendAsync(JsonRequest(StatusPath(terminalId), cookie, method: HttpMethod.Get));
        var statusBody = await statusResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(secretValue, statusBody, StringComparison.Ordinal);
    }

    private static string CredentialPath(Guid terminalId) => $"/api/v1/terminals/{terminalId:D}/relay-credential/";

    private static string StatusPath(Guid terminalId) => $"/api/v1/terminals/{terminalId:D}/relay-credential/status";

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
        builder.Services.AddRelaySettingsExperience();

        // The production wiring (QrOrderingModule) uses EnvironmentVariableSecretProvider;
        // this standalone test host uses the in-memory one instead, exactly
        // as that provider's own doc comment intends.
        var secretProvider = new InMemorySecretProvider();
        secretProvider.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        builder.Services.AddSingleton<ISecretProvider>(secretProvider);
        var accessPolicy = new RelayCredentialAccessPolicy();
        builder.Services.AddSingleton<ISecretAccessPolicy>(accessPolicy);
        builder.Services.AddSingleton<ISensitiveDataAccessPolicy>(accessPolicy);
        builder.Services.AddSingleton<ISecretResolver, SecretResolver>();
        builder.Services.AddSingleton<IEnvelopeCipher, AesGcmEnvelopeCipher>();
        builder.Services.AddSingleton<SensitivePayloadProtector>();
        builder.Services.AddSingleton<IRelayCredentialStore, PostgresRelayCredentialStore>();

        var app = builder.Build();
        app.MapRelaySettingsApi();
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

[CollectionDefinition("Relay settings PostgreSQL HTTP", DisableParallelization = true)]
public sealed class RelaySettingsPostgresqlDefinition;
