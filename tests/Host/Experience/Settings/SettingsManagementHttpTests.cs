using ALKAROS.Host.DualScreen;
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
    public async Task TheListShowsEveryKnownSettingFromTheVeryFirstVisit()
    {
        using var client = CreateClient(SettingsManagementTestDatabase.ManagerToken);

        using var response = await client.GetAsync("/api/v1/management/settings/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var keys = (await response.Content.ReadFromJsonAsync<SettingRecordV1[]>())!.Select(record => record.Key).ToHashSet();
        foreach (var expected in new[]
        {
            "waiter.max_active_tables", "kitchen.dense_mode_threshold", "kitchen.live_sync_enabled",
            "qr_ordering.pending_confirmation_timeout", "reservations.dedicated_station_enabled",
            "business.name", "business.accent_theme", "garson.party_size_enabled",
            "cash.cash_out_approval_threshold", "approvals.manager_self_approval_enabled",
        })
            Assert.Contains(expected, keys);
    }

    [Fact]
    public async Task TheCashSettingsAreListedWithTheirTypeOwnerAndDefault()
    {
        using var client = CreateClient(SettingsManagementTestDatabase.ManagerToken);

        using var response = await client.GetAsync("/api/v1/management/settings/");

        var records = (await response.Content.ReadFromJsonAsync<SettingRecordV1[]>())!;
        var threshold = Assert.Single(records, r => r.Key == "cash.cash_out_approval_threshold");
        var selfApproval = Assert.Single(records, r => r.Key == "approvals.manager_self_approval_enabled");
        Assert.Equal(("500", "WholeNumber", "cash"), (threshold.Value, threshold.DataType, threshold.ModuleOwner));
        Assert.Equal(("false", "Toggle", "cash"), (selfApproval.Value, selfApproval.DataType, selfApproval.ModuleOwner));
    }

    [Theory]
    [InlineData("-1", false)]
    [InlineData("1000001", false)]
    [InlineData("5.5", false)]
    [InlineData("abc", false)]
    [InlineData("0", true)]
    [InlineData("1000000", true)]
    public async Task TheCashOutThresholdAcceptsWholeLiraFromZeroToOneMillionOnly(string value, bool accepted)
    {
        await _database.ExecuteAsync(
            "INSERT INTO settings.settings (setting_id, setting_key, setting_value, data_type, scope, module_owner) VALUES (@id, 'cash.cash_out_approval_threshold', '500', 'Integer', 'Global', 'cash');",
            ("id", Guid.NewGuid()));
        using var client = CreateClient(SettingsManagementTestDatabase.ManagerToken);

        using var response = await client.PutAsJsonAsync(
            "/api/v1/management/settings/cash.cash_out_approval_threshold", new UpdateSettingValueV1(value, ExpectedRowVersion: 1));

        Assert.Equal(accepted ? HttpStatusCode.OK : HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(accepted ? value : "500", await _database.GetRawValueAsync("cash.cash_out_approval_threshold"));
        if (!accepted && value is "-1" or "1000001")
        {
            var error = (await ReadErrorAsync(response)).Error;
            Assert.Equal("VALUE_OUT_OF_RANGE", error.Code);
            Assert.Contains("1.000.000", error.Message);
        }
    }

    [Fact]
    public async Task TheCloseToleranceIsListedWithItsDefaultOfFiftyLira()
    {
        using var client = CreateClient(SettingsManagementTestDatabase.ManagerToken);

        using var response = await client.GetAsync("/api/v1/management/settings/");

        var tolerance = Assert.Single((await response.Content.ReadFromJsonAsync<SettingRecordV1[]>())!, r => r.Key == "cash.close_variance_tolerance");
        Assert.Equal(("50", "WholeNumber", "cash"), (tolerance.Value, tolerance.DataType, tolerance.ModuleOwner));
    }

    [Fact]
    public async Task OnlyAManagerMayChangeTheCloseTolerance()
    {
        using var manager = CreateClient(SettingsManagementTestDatabase.ManagerToken);
        using var listing = await manager.GetAsync("/api/v1/management/settings/");
        using var denied = CreateClient(SettingsManagementTestDatabase.DeniedToken);

        using var response = await denied.PutAsJsonAsync(
            "/api/v1/management/settings/cash.close_variance_tolerance", new UpdateSettingValueV1("400", ExpectedRowVersion: 1));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("50", await _database.GetRawValueAsync("cash.close_variance_tolerance"));
    }

    [Theory]
    [InlineData("-1", false)]
    [InlineData("501", false)]
    [InlineData("5.5", false)]
    [InlineData("abc", false)]
    [InlineData("0", true)]
    [InlineData("500", true)]
    public async Task TheCloseToleranceAcceptsWholeLiraFromZeroToTheFiveHundredCeilingOnly(string value, bool accepted)
    {
        using var client = CreateClient(SettingsManagementTestDatabase.ManagerToken);
        using var listing = await client.GetAsync("/api/v1/management/settings/");

        using var response = await client.PutAsJsonAsync(
            "/api/v1/management/settings/cash.close_variance_tolerance", new UpdateSettingValueV1(value, ExpectedRowVersion: 1));

        Assert.Equal(accepted ? HttpStatusCode.OK : HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(accepted ? value : "50", await _database.GetRawValueAsync("cash.close_variance_tolerance"));
    }

    [Fact]
    public async Task ChangingAndDeactivatingASettingWriteAuditEventsWithTheOldAndNewValue()
    {
        // a change of the cash-out approval threshold, or of any other setting, is audited.
        using var client = CreateClient(SettingsManagementTestDatabase.ManagerToken);
        using var listing = await client.GetAsync("/api/v1/management/settings/");
        Assert.Equal(0L, await AuditCountAsync());

        using var changed = await client.PutAsJsonAsync(
            "/api/v1/management/settings/cash.cash_out_approval_threshold",
            new UpdateSettingValueV1("800", ExpectedRowVersion: 1, Reason: "tedarikçi ödemeleri arttı"));
        using var deactivated = await client.DeleteAsync(
            "/api/v1/management/settings/cash.cash_out_approval_threshold?expectedRowVersion=2&reason=kapat");

        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        await using var command = _database.DataSource.CreateCommand(
            """
            SELECT event_name, actor_id, actor_type, reason, before_state_json->>'key', before_state_json->>'value', after_state_json->>'value'
            FROM audit.audit_events WHERE aggregate_type = 'Setting' ORDER BY occurred_at;
            """);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(
            ("settings.value_changed", SettingsManagementTestDatabase.ManagerUserId, "User", "tedarikçi ödemeleri arttı", "cash.cash_out_approval_threshold", "500", "800"),
            (reader.GetString(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6)));
        Assert.True(await reader.ReadAsync());
        Assert.Equal(
            ("settings.deactivated", "kapat", "800", "[Deactivated]"),
            (reader.GetString(0), reader.GetString(3), reader.GetString(5), reader.GetString(6)));
        Assert.False(await reader.ReadAsync());
    }

    [Fact]
    public async Task ASystemChangeWithoutAnActorIsAuditedAsASystemEvent()
    {
        using var client = CreateClient(SettingsManagementTestDatabase.ManagerToken);
        using var listing = await client.GetAsync("/api/v1/management/settings/");
        await _database.ExecuteAsync(
            """
            INSERT INTO settings.setting_history (setting_history_id, setting_id, old_value, new_value, reason, changed_by, changed_at)
            SELECT gen_random_uuid(), setting_id, 'false', 'true', 'bakım', NULL, now()
            FROM settings.settings WHERE setting_key = 'approvals.manager_self_approval_enabled';
            """);

        await using var command = _database.DataSource.CreateCommand(
            "SELECT actor_type, actor_id IS NULL, reason FROM audit.audit_events WHERE aggregate_type = 'Setting';");
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(("System", true, "bakım"), (reader.GetString(0), reader.GetBoolean(1), reader.GetString(2)));
        Assert.False(await reader.ReadAsync());
    }

    private async Task<long> AuditCountAsync()
    {
        await using var command = _database.DataSource.CreateCommand("SELECT count(*) FROM audit.audit_events WHERE aggregate_type = 'Setting';");
        return (long)(await command.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task TheHistoryNamesWhoChangedTheSettingAndUsesTheCurrentName()
    {
        var key = await _database.SeedBooleanSettingAsync("test.feature.named", false);
        using var client = CreateClient(SettingsManagementTestDatabase.ManagerToken);
        using var update = await client.PutAsJsonAsync(
            $"/api/v1/management/settings/{key}", new UpdateSettingValueV1("true", ExpectedRowVersion: 1, Reason: "aç"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        await _database.ExecuteAsync(
            "UPDATE identity.users SET display_name = 'Zeynep Müdür' WHERE user_id = @id;",
            ("id", SettingsManagementTestDatabase.ManagerUserId));

        using var history = await client.GetAsync($"/api/v1/management/settings/{key}/history");

        var entries = await history.Content.ReadFromJsonAsync<SettingHistoryRecordV1[]>();
        Assert.Equal("Zeynep Müdür", Assert.Single(entries!).ChangedByName);
    }

    [Fact]
    public async Task AnOutOfRangeValueIsRefusedInTurkishAndTheSettingIsUnchanged()
    {
        await _database.ExecuteAsync(
            "INSERT INTO settings.settings (setting_id, setting_key, setting_value, data_type, scope, module_owner) VALUES (@id, 'waiter.max_active_tables', '0', 'Integer', 'Global', 'orders');",
            ("id", Guid.NewGuid()));
        using var client = CreateClient(SettingsManagementTestDatabase.ManagerToken);

        using var response = await client.PutAsJsonAsync(
            "/api/v1/management/settings/waiter.max_active_tables",
            new UpdateSettingValueV1("-5", ExpectedRowVersion: 1, Reason: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await ReadErrorAsync(response)).Error;
        Assert.Equal("VALUE_OUT_OF_RANGE", error.Code);
        Assert.Contains("0 ile 50 arasında", error.Message);
        Assert.Equal("0", await _database.GetRawValueAsync("waiter.max_active_tables"));
    }

    [Fact]
    public async Task AnAccentColourOutsideThePaletteIsRefused()
    {
        await _database.ExecuteAsync(
            "INSERT INTO settings.settings (setting_id, setting_key, setting_value, data_type, scope, module_owner) VALUES (@id, 'business.accent_theme', 'lacivert', 'String', 'Global', 'business');",
            ("id", Guid.NewGuid()));
        using var client = CreateClient(SettingsManagementTestDatabase.ManagerToken);

        using var refused = await client.PutAsJsonAsync(
            "/api/v1/management/settings/business.accent_theme", new UpdateSettingValueV1("pembe", ExpectedRowVersion: 1));
        using var accepted = await client.PutAsJsonAsync(
            "/api/v1/management/settings/business.accent_theme", new UpdateSettingValueV1("mor", ExpectedRowVersion: 1));

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("VALUE_OUT_OF_RANGE", (await ReadErrorAsync(refused)).Error.Code);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal("mor", await _database.GetRawValueAsync("business.accent_theme"));
    }

    [Fact]
    public async Task ADeactivatedSettingCannotBeChangedAnymore()
    {
        var key = await _database.SeedBooleanSettingAsync("test.feature.inactive", true);
        using var client = CreateClient(SettingsManagementTestDatabase.ManagerToken);
        using var deactivate = await client.DeleteAsync($"/api/v1/management/settings/{key}?expectedRowVersion=1");
        var record = await deactivate.Content.ReadFromJsonAsync<SettingRecordV1>();

        using var response = await client.PutAsJsonAsync(
            $"/api/v1/management/settings/{key}",
            new UpdateSettingValueV1("false", record!.RowVersion, Reason: null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("SETTING_INACTIVE", (await ReadErrorAsync(response)).Error.Code);
        Assert.Equal("true", await _database.GetRawValueAsync(key));
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
    public async Task TheHistoryRecordsTheManagerWhoMadeTheChange()
    {
        var key = await _database.SeedBooleanSettingAsync("test.feature.actor", false);
        using var client = CreateClient(SettingsManagementTestDatabase.ManagerToken);
        using var update = await client.PutAsJsonAsync(
            $"/api/v1/management/settings/{key}", new UpdateSettingValueV1("true", ExpectedRowVersion: 1, Reason: "aç"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        using var history = await client.GetAsync($"/api/v1/management/settings/{key}/history");

        var entry = Assert.Single((await history.Content.ReadFromJsonAsync<SettingHistoryRecordV1[]>())!);
        Assert.Equal(SettingsManagementTestDatabase.ManagerUserId, entry.ChangedBy);
    }

    [Fact]
    public async Task TheListCanBeNarrowedToOneModuleOwner()
    {
        await _database.ExecuteAsync(
            "INSERT INTO settings.settings (setting_id, setting_key, setting_value, data_type, scope, module_owner) VALUES (@a, 'test.owner.alpha', 'true', 'Boolean', 'Global', 'alpha'), (@b, 'test.owner.beta', 'true', 'Boolean', 'Global', 'beta');",
            ("a", Guid.NewGuid()), ("b", Guid.NewGuid()));
        using var client = CreateClient(SettingsManagementTestDatabase.ManagerToken);

        using var response = await client.GetAsync("/api/v1/management/settings/?moduleOwner=alpha");

        var records = await response.Content.ReadFromJsonAsync<SettingRecordV1[]>();
        Assert.Equal("test.owner.alpha", Assert.Single(records!).Key);
    }

    [Fact]
    public async Task DeactivatingWithAStaleVersionOrAnUnknownKeyIsRefusedAndChangesNothing()
    {
        var key = await _database.SeedBooleanSettingAsync("test.feature.stale-delete", true);
        using var client = CreateClient(SettingsManagementTestDatabase.ManagerToken);

        using var stale = await client.DeleteAsync($"/api/v1/management/settings/{key}?expectedRowVersion=99");
        using var unknown = await client.DeleteAsync($"/api/v1/management/settings/no.such.key.{Guid.NewGuid():N}?expectedRowVersion=1");

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("CONCURRENCY_CONFLICT", (await ReadErrorAsync(stale)).Error.Code);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        var current = await client.GetFromJsonAsync<SettingRecordV1>($"/api/v1/management/settings/{key}");
        Assert.True(current!.Active);
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

    private static async Task<ApiErrorEnvelope> ReadErrorAsync(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>()
            ?? throw new InvalidOperationException("Expected a settings management error response.");
}

[CollectionDefinition("Settings management PostgreSQL HTTP", DisableParallelization = true)]
public sealed class SettingsManagementPostgresqlDefinition;
