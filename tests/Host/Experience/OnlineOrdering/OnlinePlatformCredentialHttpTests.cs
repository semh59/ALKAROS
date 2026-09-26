using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ALKAROS.Host.Composition;
using ALKAROS.ModuleComposition;
using ALKAROS.OnlineOrdering;
using ALKAROS.OnlineOrdering.Credentials;
using ALKAROS.OnlineOrdering.Yemeksepeti.StatusSync;
using ALKAROS.OnlineOrdering.Yemeksepeti.WebhookInbox;
using ALKAROS.Secrets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.OnlineOrdering.Tests;

/// <summary>
/// V12-OUI-003: online platform settings over real HTTP and Postgres — manager-only, secrets sealed and never
/// returned, every change audited without values, and the Yemeksepeti webhook and partner client reading a
/// stored setting before the environment variable.
/// </summary>
[Collection("Online ordering PostgreSQL")]
public sealed class OnlinePlatformCredentialHttpTests : IAsyncLifetime, IDisposable
{
    private const string ClientSecret = "cs-Zq81-very-secret";
    private const string WebhookSecret = "Bearer stored-webhook-token";
    private static readonly string[] ClientSecretTwice = ["client-secret", "client-secret"];
    private static readonly string[] ChainAndWebhook = ["chain-id", "webhook-secret"];
    private static readonly string[] ClientSecretOnly = ["client-secret"];

    private readonly OnlineOrderingTestDatabase _database = new();
    private readonly InMemorySecretProvider _secrets = new();
    private readonly Guid _terminalId = Guid.NewGuid();
    private WebApplication? _app;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        await _database.ExecuteAsync("DELETE FROM online_ordering.platform_credentials;");
        _secrets.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddSingleton<ISecretProvider>(_secrets);
        builder.Services.AddOnlinePlatformCredentialExperience();
        builder.Services.AddRateLimiter(limiter =>
            limiter.AddPolicy("terminal-write", _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("w")));
        _app = builder.Build();
        _app.UseRateLimiter();
        _app.MapOnlinePlatformCredentialApi();
        await _app.StartAsync();
        var address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        _client = new HttpClient { BaseAddress = new Uri(address) };
    }

    public void Dispose() => _client?.Dispose();

    public async Task DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
        await _database.DisposeAsync();
    }

    private string Root => $"/api/v1/terminals/{_terminalId:D}/online-platform-credentials";

    private async Task<HttpResponseMessage> GetAsync(string? cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Root + "/");
        if (cookie is not null)
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return await _client!.SendAsync(request);
    }

    private async Task<HttpResponseMessage> PutAsync(string? cookie, string provider, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"{Root}/{provider}") { Content = JsonContent.Create(body) };
        if (cookie is not null)
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return await _client!.SendAsync(request);
    }

    private Task<string> ManagerAsync() => _database.SeedStaffSessionAsync(_terminalId, "integrations.manage");

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private static JsonElement Field(JsonElement platform, string name) =>
        platform.GetProperty("fields").EnumerateArray().Single(f => f.GetProperty("name").GetString() == name);

    private static JsonElement Platform(JsonElement list, string provider) =>
        list.GetProperty("platforms").EnumerateArray().Single(p => p.GetProperty("provider").GetString() == provider);

    private Task<long> AuditCountAsync() =>
        _database.ScalarAsync<long>("SELECT count(*) FROM audit.audit_events WHERE event_name = 'OnlinePlatform.CredentialsChanged';");

    private PostgresOnlinePlatformCredentialStore Store() => new(_database.DataSource, _secrets);

    [Fact]
    public async Task OnlyASignedInManagerReadsOrChangesPlatformSettings()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(null)).StatusCode);
        var cashier = await _database.SeedStaffSessionAsync(_terminalId, "orders.create");
        using var read = await GetAsync(cashier);
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        Assert.Equal("Bu işlem için yetkiniz yok.", (await JsonAsync(read)).GetProperty("error").GetProperty("message").GetString());
        using var write = await PutAsync(cashier, "yemeksepeti", new { values = new Dictionary<string, string> { ["chain-id"] = "c1" } });
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        Assert.Equal(0, await _database.CountAsync("online_ordering.platform_credentials"));
        Assert.Equal(0, await AuditCountAsync());
    }

    [Fact]
    public async Task ASecretIsSealedNeverReturnedAndTheChangeIsAuditedWithoutValues()
    {
        var manager = await ManagerAsync();
        using var saved = await PutAsync(manager, "yemeksepeti", new
        {
            values = new Dictionary<string, string>
            {
                ["api-base-url"] = " https://partner.example.test/ \n",
                ["chain-id"] = "chain-7",
                ["client-secret"] = ClientSecret,
            },
        });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var savedText = await saved.Content.ReadAsStringAsync();
        Assert.DoesNotContain(ClientSecret, savedText, StringComparison.Ordinal);

        using var listed = await GetAsync(manager);
        var listText = await listed.Content.ReadAsStringAsync();
        Assert.DoesNotContain(ClientSecret, listText, StringComparison.Ordinal);
        var list = JsonDocument.Parse(listText).RootElement;
        Assert.Equal(["yemeksepeti", "trendyol-go"], list.GetProperty("platforms").EnumerateArray().Select(p => p.GetProperty("provider").GetString()));
        var ysp = Platform(list, "yemeksepeti");
        Assert.Equal("https://partner.example.test/", Field(ysp, "api-base-url").GetProperty("value").GetString());
        Assert.Equal("chain-7", Field(ysp, "chain-id").GetProperty("value").GetString());
        var secret = Field(ysp, "client-secret");
        Assert.True(secret.GetProperty("isSecret").GetBoolean());
        Assert.True(secret.GetProperty("configured").GetBoolean());
        Assert.Equal(JsonValueKind.Null, secret.GetProperty("value").ValueKind);
        Assert.False(Field(ysp, "webhook-secret").GetProperty("configured").GetBoolean());
        Assert.False(Field(ysp, "vendor-id").GetProperty("configured").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, ysp.GetProperty("updatedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, Platform(list, "trendyol-go").GetProperty("updatedAt").ValueKind);

        // Only ciphertext reaches the table.
        await using (var command = _database.DataSource.CreateCommand(
            "SELECT plain_fields::text, secret_fields, envelope_bytes, updated_by FROM online_ordering.platform_credentials WHERE provider = 'yemeksepeti';"))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync());
            Assert.DoesNotContain(ClientSecret, reader.GetString(0), StringComparison.Ordinal);
            Assert.Equal(["client-secret"], reader.GetFieldValue<string[]>(1));
            var envelope = reader.GetFieldValue<byte[]>(2);
            Assert.Equal(-1, envelope.AsSpan().IndexOf(Encoding.UTF8.GetBytes(ClientSecret)));
            Assert.False(reader.IsDBNull(3));
        }

        Assert.Equal(ClientSecret, Store().ResolveValue("yemeksepeti", "client-secret"));

        await using (var command = _database.DataSource.CreateCommand(
            "SELECT aggregate_type, aggregate_id, actor_id, actor_type, metadata_json::text FROM audit.audit_events WHERE event_name = 'OnlinePlatform.CredentialsChanged';"))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync());
            Assert.Equal("OnlinePlatform", reader.GetString(0));
            Assert.Equal(PostgresOnlinePlatformCredentialStore.AggregateId("yemeksepeti"), reader.GetGuid(1));
            Assert.False(reader.IsDBNull(2));
            Assert.Equal("User", reader.GetString(3));
            var metadata = JsonDocument.Parse(reader.GetString(4)).RootElement;
            Assert.Equal("yemeksepeti", metadata.GetProperty("provider").GetString());
            Assert.Equal(["api-base-url", "chain-id", "client-secret"], metadata.GetProperty("set").EnumerateArray().Select(e => e.GetString()));
            Assert.Empty(metadata.GetProperty("cleared").EnumerateArray());
            Assert.DoesNotContain(ClientSecret, reader.GetString(4), StringComparison.Ordinal);
            Assert.DoesNotContain("chain-7", reader.GetString(4), StringComparison.Ordinal);
            Assert.False(await reader.ReadAsync());
        }
    }

    [Fact]
    public async Task ChangingOneFieldKeepsTheOthersAndClearingTheLastFieldRemovesThePlatformRow()
    {
        var manager = await ManagerAsync();
        (await PutAsync(manager, "yemeksepeti", new
        {
            values = new Dictionary<string, string> { ["chain-id"] = "chain-1", ["client-secret"] = ClientSecret, ["webhook-secret"] = WebhookSecret },
        })).EnsureSuccessStatusCode();

        (await PutAsync(manager, "yemeksepeti", new { values = new Dictionary<string, string> { ["chain-id"] = "chain-2" } })).EnsureSuccessStatusCode();
        var store = Store();
        Assert.Equal("chain-2", store.ResolveValue("yemeksepeti", "chain-id"));
        Assert.Equal(ClientSecret, store.ResolveValue("yemeksepeti", "client-secret"));
        Assert.Equal(WebhookSecret, store.ResolveValue("yemeksepeti", "webhook-secret"));

        using var cleared = await PutAsync(manager, "yemeksepeti", new { cleared = ClientSecretTwice });
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        var status = await JsonAsync(cleared);
        Assert.False(Field(status, "client-secret").GetProperty("configured").GetBoolean());
        Assert.True(Field(status, "webhook-secret").GetProperty("configured").GetBoolean());
        Assert.Null(store.ResolveValue("yemeksepeti", "client-secret"));
        Assert.Equal(WebhookSecret, store.ResolveValue("yemeksepeti", "webhook-secret"));
        Assert.Equal(
            ["webhook-secret"],
            await _database.ScalarAsync<string[]>("SELECT secret_fields FROM online_ordering.platform_credentials WHERE provider = 'yemeksepeti';"));

        (await PutAsync(manager, "yemeksepeti", new { cleared = ChainAndWebhook })).EnsureSuccessStatusCode();
        Assert.Equal(0, await _database.CountAsync("online_ordering.platform_credentials"));
        Assert.Null(store.ResolveValue("yemeksepeti", "webhook-secret"));
        Assert.Equal(4, await AuditCountAsync());
        Assert.Equal(
            "[\"client-secret\"]",
            await _database.ScalarAsync<string>(
                "SELECT metadata_json->>'cleared' FROM audit.audit_events WHERE event_name = 'OnlinePlatform.CredentialsChanged' AND metadata_json->'cleared' ? 'client-secret';"));
    }

    [Theory]
    [InlineData("yemeksepeti", "{\"values\":{\"api-base-url\":\"http://partner.example.test\"}}", "api-base-url", "Adres https:// ile başlayan geçerli bir adres olmalıdır.")]
    [InlineData("yemeksepeti", "{\"values\":{\"api-base-url\":\"https://user:pw@partner.example.test\"}}", "api-base-url", "Adres https:// ile başlayan geçerli bir adres olmalıdır.")]
    [InlineData("yemeksepeti", "{\"values\":{\"supplier-id\":\"s1\"}}", "supplier-id", "Bu platformda böyle bir alan yok.")]
    [InlineData("yemeksepeti", "{\"cleared\":[\"api-key\"]}", "api-key", "Bu platformda böyle bir alan yok.")]
    [InlineData("yemeksepeti", "{\"values\":{\"chain-id\":\"   \"}}", "chain-id", "Alan boş bırakılamaz; silmek için Kaldır'ı kullanın.")]
    [InlineData("yemeksepeti", "{\"values\":{\"chain-id\":\"a\\u0007b\"}}", "chain-id", "Değer geçersiz karakter içeriyor.")]
    [InlineData("yemeksepeti", "{\"values\":{\"chain-id\":\"c\"},\"cleared\":[\"chain-id\"]}", "chain-id", "Aynı alan hem girilip hem silinemez.")]
    [InlineData("yemeksepeti", "{}", null, "Kaydedilecek bir değişiklik yok.")]
    [InlineData("yemeksepeti", "{\"cleared\":[\"vendor-id\"]}", null, "Kaydedilecek bir değişiklik yok.")]
    [InlineData("trendyol-go", "{\"values\":{\"client-secret\":\"x\"}}", "client-secret", "Bu platformda böyle bir alan yok.")]
    public async Task ARefusedChangeStoresAndAuditsNothing(string provider, string body, string? field, string message)
    {
        var manager = await ManagerAsync();
        using var request = new HttpRequestMessage(HttpMethod.Put, $"{Root}/{provider}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("Cookie", manager);
        using var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await JsonAsync(response)).GetProperty("error");
        Assert.Equal("VALIDATION_FAILED", error.GetProperty("code").GetString());
        Assert.Equal(message, error.GetProperty("message").GetString());
        Assert.Equal(field, error.GetProperty("field").ValueKind == JsonValueKind.Null ? null : error.GetProperty("field").GetString());
        Assert.Equal(0, await _database.CountAsync("online_ordering.platform_credentials"));
        Assert.Equal(0, await AuditCountAsync());
    }

    [Fact]
    public async Task AValueLongerThanTheLimitAndAnUnknownPlatformAreRefused()
    {
        var manager = await ManagerAsync();
        using var tooLong = await PutAsync(manager, "yemeksepeti", new
        {
            values = new Dictionary<string, string> { ["chain-id"] = new string('x', PostgresOnlinePlatformCredentialStore.MaxValueLength + 1) },
        });
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal("Değer çok uzun.", (await JsonAsync(tooLong)).GetProperty("error").GetProperty("message").GetString());
        using var longest = await PutAsync(manager, "yemeksepeti", new
        {
            values = new Dictionary<string, string> { ["chain-id"] = new string('x', PostgresOnlinePlatformCredentialStore.MaxValueLength) },
        });
        Assert.Equal(HttpStatusCode.OK, longest.StatusCode);

        using var unknown = await PutAsync(manager, "migros-yemek", new { values = new Dictionary<string, string> { ["chain-id"] = "c" } });
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        var error = (await JsonAsync(unknown)).GetProperty("error");
        Assert.Equal(("PLATFORM_NOT_FOUND", "Bu online platform tanınmıyor."), (error.GetProperty("code").GetString(), error.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task APlatformSettingComesFromTheStoreFirstAndFromTheEnvironmentOnlyWhenNotStored()
    {
        var reference = new SecretReference("yemeksepeti-client-secret");
        _secrets.Set(reference, "from-environment");
        var provider = new StoredOnlinePlatformSecretProvider(Store(), _secrets);
        Assert.Equal("from-environment", provider.GetValue(reference));

        var manager = await ManagerAsync();
        (await PutAsync(manager, "yemeksepeti", new { values = new Dictionary<string, string> { ["client-secret"] = ClientSecret } })).EnsureSuccessStatusCode();
        Assert.Equal(ClientSecret, provider.GetValue(reference));
        // Another field of the same platform is not stored, so it still comes from the environment.
        _secrets.Set(new SecretReference("yemeksepeti-chain-id"), "env-chain");
        Assert.Equal("env-chain", provider.GetValue(new SecretReference("yemeksepeti-chain-id")));
        // A secret that is not a platform setting never touches the store.
        _secrets.Set(new SecretReference("trendyol-go-api-key-extra"), "not-a-setting");
        Assert.Equal("not-a-setting", provider.GetValue(new SecretReference("trendyol-go-api-key-extra")));

        (await PutAsync(manager, "yemeksepeti", new { cleared = ClientSecretOnly })).EnsureSuccessStatusCode();
        Assert.Equal("from-environment", provider.GetValue(reference));
    }

    [Fact]
    public async Task TheRegisteredWebhookInboxAndPartnerClientReadStoredSettings()
    {
        var context = new ModuleContext();
        new OnlineOrderingModule().Register(context);
        var services = new ServiceCollection();
        HostComposition.ApplyComposedModuleServices(services, context.Services);
        services.AddSingleton(_database.DataSource);
        services.AddSingleton<ISecretProvider>(_secrets);
        await using var provider = services.BuildServiceProvider();

        var inbox = provider.GetRequiredService<YemeksepetiWebhookInbox>();
        Assert.Equal(WebhookReceiptOutcome.ChannelNotConfigured, inbox.Authenticate(WebhookSecret));
        var client = provider.GetRequiredService<IYemeksepetiPartnerClient>();
        await Assert.ThrowsAsync<SecretNotFoundException>(() => client.UpdateVendorCatalogAsync([new("S1", 10m, null, null)]));

        var manager = await ManagerAsync();
        (await PutAsync(manager, "yemeksepeti", new
        {
            values = new Dictionary<string, string>
            {
                ["webhook-secret"] = WebhookSecret,
                // Nothing listens on port 1, so reaching the network proves every setting was found.
                ["api-base-url"] = "https://127.0.0.1:1/",
                ["chain-id"] = "chain-1",
                ["vendor-id"] = "vendor-1",
                ["client-id"] = "client-1",
                ["client-secret"] = ClientSecret,
            },
        })).EnsureSuccessStatusCode();

        Assert.Null(provider.GetRequiredService<YemeksepetiWebhookInbox>().Authenticate(WebhookSecret));
        Assert.Equal(WebhookReceiptOutcome.Unauthenticated, provider.GetRequiredService<YemeksepetiWebhookInbox>().Authenticate("Bearer other"));
        await Assert.ThrowsAsync<HttpRequestException>(() => client.UpdateVendorCatalogAsync([new("S1", 10m, null, null)]));
    }
}
