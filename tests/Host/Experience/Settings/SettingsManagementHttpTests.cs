using System.Net;
using System.Net.Http.Json;
using ALKAROS.Host.Experience.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Settings.Tests;

/// <summary>
/// V1-RMD-246: found by an independent audit (2026-09-18) —
/// ISettingsService.SetValueAsync/DeactivateAsync (V1-SET-001) had zero
/// HTTP surface; a setting could only ever change via direct database
/// access. Proves a real HTTP client can read, change and deactivate a
/// real setting, and that the manager gate/optimistic concurrency/type
/// validation this endpoint claims are real.
/// </summary>
[Collection("Settings management PostgreSQL HTTP")]
public sealed class SettingsManagementHttpTests : IAsyncLifetime
{
    private readonly SettingsManagementTestDatabase _database = new();
    private WebApplication? _application;
    private Uri? _baseAddress;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddSettingsManagementExperience();

        _application = builder.Build();
        _application.MapSettingsManagement();
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
    public async Task MissingAndUnpermissionedSessionsAreRejectedWithoutMutation()
    {
        var key = await _database.SeedBooleanSettingAsync("test.auth.flag", false);

        using var anonymous = CreateClient(null);
        using var unauthorized = await anonymous.GetAsync($"/api/v1/management/settings/{key}");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal("UNAUTHORIZED", (await ReadErrorAsync(unauthorized)).Error.Code);

        using var denied = CreateClient(SettingsManagementTestDatabase.DeniedToken);
        using var forbidden = await denied.GetAsync($"/api/v1/management/settings/{key}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("FORBIDDEN", (await ReadErrorAsync(forbidden)).Error.Code);

        Assert.Equal("false", await _database.GetRawValueAsync(key));
    }

    [Fact]
    public async Task AManagerCanReadAndUpdateARealSetting()
    {
        var key = await _database.SeedBooleanSettingAsync("test.feature.toggle", false);
        using var client = CreateClient(SettingsManagementTestDatabase.ManagerToken);

        using var initial = await client.GetAsync($"/api/v1/management/settings/{key}");
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        var initialRecord = await initial.Content.ReadFromJsonAsync<SettingRecordV1>();
        Assert.Equal("false", initialRecord!.Value);
        Assert.Equal(1, initialRecord.RowVersion);

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/v1/management/settings/{key}",
            new UpdateSettingValueV1("true", initialRecord.RowVersion, "manager turned it on"));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<SettingRecordV1>();
        Assert.Equal("true", updated!.Value);
        Assert.Equal("true", await _database.GetRawValueAsync(key));

        using var history = await client.GetAsync($"/api/v1/management/settings/{key}/history");
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        var entries = await history.Content.ReadFromJsonAsync<SettingHistoryRecordV1[]>();
        Assert.Single(entries!);
        Assert.Equal("manager turned it on", entries![0].Reason);
    }

    [Fact]
    public async Task AStaleRowVersionIsRejectedAndTheSettingIsUnchanged()
    {
        var key = await _database.SeedBooleanSettingAsync("test.feature.stale", false);
        using var client = CreateClient(SettingsManagementTestDatabase.ManagerToken);

        using var response = await client.PutAsJsonAsync(
            $"/api/v1/management/settings/{key}",
            new UpdateSettingValueV1("true", ExpectedRowVersion: 99, Reason: null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("CONCURRENCY_CONFLICT", (await ReadErrorAsync(response)).Error.Code);
        Assert.Equal("false", await _database.GetRawValueAsync(key));
    }

    [Fact]
    public async Task AValueThatDoesNotMatchTheDeclaredTypeIsRejected()
    {
        var key = await _database.SeedBooleanSettingAsync("test.feature.wrongtype", false);
        using var client = CreateClient(SettingsManagementTestDatabase.ManagerToken);

        using var response = await client.PutAsJsonAsync(
            $"/api/v1/management/settings/{key}",
            new UpdateSettingValueV1("not-a-boolean", ExpectedRowVersion: 1, Reason: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_FAILED", (await ReadErrorAsync(response)).Error.Code);
        Assert.Equal("false", await _database.GetRawValueAsync(key));
    }

    [Fact]
    public async Task DeactivatingASettingTurnsItInactiveWithoutDeletingIt()
    {
        var key = await _database.SeedBooleanSettingAsync("test.feature.deactivate", true);
        using var client = CreateClient(SettingsManagementTestDatabase.ManagerToken);

        using var response = await client.DeleteAsync(
            $"/api/v1/management/settings/{key}?expectedRowVersion=1&reason=no+longer+needed");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var deactivated = await response.Content.ReadFromJsonAsync<SettingRecordV1>();
        Assert.False(deactivated!.Active);
        // Still physically present — DeactivateAsync never deletes.
        Assert.Equal("true", await _database.GetRawValueAsync(key));
    }

    [Fact]
    public async Task AnUnknownKeyIsNotFound()
    {
        using var client = CreateClient(SettingsManagementTestDatabase.ManagerToken);

        using var response = await client.GetAsync($"/api/v1/management/settings/no.such.key.{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("NOT_FOUND", (await ReadErrorAsync(response)).Error.Code);
    }

    private HttpClient CreateClient(string? token)
    {
        var client = new HttpClient { BaseAddress = _baseAddress };
        if (token is not null)
            client.DefaultRequestHeaders.Add("Cookie", $"{SettingsManagementEndpoints.ManagerCookieName}={token}");
        return client;
    }

    private static async Task<SettingsApiErrorEnvelopeV1> ReadErrorAsync(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<SettingsApiErrorEnvelopeV1>()
            ?? throw new InvalidOperationException("Expected a settings management error response.");
}

[CollectionDefinition("Settings management PostgreSQL HTTP", DisableParallelization = true)]
public sealed class SettingsManagementPostgresqlDefinition;
