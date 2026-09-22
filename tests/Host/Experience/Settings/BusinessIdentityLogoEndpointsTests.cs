using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ALKAROS.Settings.BusinessIdentity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Settings.Tests;

/// <summary>
/// V1-SET-008: the business's own QR-page logo, gated by the same
/// `settings.manage` manager surface V1-RMD-246 built for
/// business.name/business.accent_theme — reuses
/// <see cref="SettingsManagementTestDatabase"/> (ManagerToken/DeniedToken
/// already seeded with/without settings.manage) rather than duplicating it.
/// </summary>
[Collection("Business identity logo PostgreSQL HTTP")]
public sealed class BusinessIdentityLogoEndpointsTests : IAsyncLifetime
{
    private readonly SettingsManagementTestDatabase _database = new();
    private WebApplication? _application;
    private Uri? _baseAddress;
    private BusinessLogoStore _store = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _store = new BusinessLogoStore(_database.DataSource);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddBusinessIdentityLogoExperience();

        _application = builder.Build();
        _application.MapBusinessIdentityLogoApi();
        await _application.StartAsync();
        var addresses = _application.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>();
        _baseAddress = new Uri(Assert.Single(addresses!.Addresses), UriKind.Absolute);
    }

    public async Task DisposeAsync()
    {
        if (_application is not null)
            await _application.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task PutWithoutAManagerSessionIsUnauthorized()
    {
        using var client = CreateClient(null);

        using var response = await client.PutAsync("/api/v1/management/business-identity/logo", BuildPngContent());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(await _store.GetAsync(CancellationToken.None));
    }

    [Fact]
    public async Task PutWithoutSettingsManagePermissionIsForbidden()
    {
        using var client = CreateClient(SettingsManagementTestDatabase.DeniedToken);

        using var response = await client.PutAsync("/api/v1/management/business-identity/logo", BuildPngContent());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(await _store.GetAsync(CancellationToken.None));
    }

    [Fact]
    public async Task AManagerCanUploadAndThenRemoveARealLogo()
    {
        using var client = CreateClient(SettingsManagementTestDatabase.ManagerToken);

        using var putResponse = await client.PutAsync("/api/v1/management/business-identity/logo", BuildPngContent());
        Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);

        var stored = await _store.GetAsync(CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("image/png", stored!.ContentType);

        using var deleteResponse = await client.DeleteAsync("/api/v1/management/business-identity/logo");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Null(await _store.GetAsync(CancellationToken.None));
    }

    [Fact]
    public async Task PutRejectsADisallowedContentType()
    {
        using var client = CreateClient(SettingsManagementTestDatabase.ManagerToken);
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([1, 2, 3]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "logo.pdf");

        using var response = await client.PutAsync("/api/v1/management/business-identity/logo", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(await _store.GetAsync(CancellationToken.None));
    }

    [Fact]
    public async Task PutRejectsAFileOverFiveMegabytes()
    {
        using var client = CreateClient(SettingsManagementTestDatabase.ManagerToken);
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[5 * 1024 * 1024 + 1]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "too-big.png");

        using var response = await client.PutAsync("/api/v1/management/business-identity/logo", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(await _store.GetAsync(CancellationToken.None));
    }

    // The declared Content-Type header is entirely client-controlled and
    // already passes the allowlist check here — this proves the file's own
    // first bytes (magic number) are checked independently.
    [Fact]
    public async Task PutRejectsAFileWhoseContentDoesNotMatchItsDeclaredContentType()
    {
        using var client = CreateClient(SettingsManagementTestDatabase.ManagerToken);
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent("this is not actually a png file"u8.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "fake.png");

        using var response = await client.PutAsync("/api/v1/management/business-identity/logo", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(await _store.GetAsync(CancellationToken.None));
    }

    [Fact]
    public async Task AccentPaletteReturnsAllEightColorsAndTheRealDefaultKey()
    {
        using var client = CreateClient(SettingsManagementTestDatabase.ManagerToken);

        using var response = await client.GetAsync("/api/v1/management/business-identity/accent-palette");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AccentPaletteResponseV1>();
        Assert.Equal(8, body!.Entries.Count);
        Assert.Equal(BusinessAccentPalette.DefaultKey, body.DefaultKey);
        Assert.Contains(body.Entries, entry => entry.Key == body.DefaultKey);
        // PosTerminal's own settings screen never invents/copies these — it
        // renders exactly what this endpoint returns, so every entry here
        // must match the server's own palette record byte-for-byte.
        foreach (var serverEntry in BusinessAccentPalette.All)
        {
            var wireEntry = Assert.Single(body.Entries, entry => entry.Key == serverEntry.Key);
            Assert.Equal(serverEntry.Label, wireEntry.Label);
            Assert.Equal(serverEntry.Hex, wireEntry.Hex);
        }
    }

    [Fact]
    public async Task AccentPaletteWithoutAManagerSessionIsUnauthorized()
    {
        using var client = CreateClient(null);

        using var response = await client.GetAsync("/api/v1/management/business-identity/accent-palette");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static MultipartFormDataContent BuildPngContent()
    {
        var content = new MultipartFormDataContent();
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];
        var fileContent = new ByteArrayContent(png);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "logo.png");
        return content;
    }

    private HttpClient CreateClient(string? token)
    {
        var client = new HttpClient { BaseAddress = _baseAddress };
        if (token is not null)
            client.DefaultRequestHeaders.Add("Cookie", $"{SettingsManagementEndpoints.ManagerCookieName}={token}");
        return client;
    }
}

[CollectionDefinition("Business identity logo PostgreSQL HTTP", DisableParallelization = true)]
public sealed class BusinessIdentityLogoPostgresqlDefinition;
