using ALKAROS.Settings.BusinessIdentity.Tests.Fixtures;
using ALKAROS.Settings.TypedSettings;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Settings.BusinessIdentity.Tests;

/// <summary>
/// One fresh isolated database per test (not a shared class/collection
/// fixture) — same reasoning as BusinessNameSettingTests.
/// </summary>
public sealed class BusinessAccentThemeSettingTests : IAsyncLifetime
{
    private readonly BusinessIdentityTestDatabase _db = new();
    private SettingsService _service = null!;

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();
        _service = new SettingsService(new PostgresSettingsRepository(_db.DataSource, new SettingValidator()));
    }

    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task GetThemeRegistersTheSettingAtThePaletteDefaultTheFirstTimeItIsAsked()
    {
        (await _service.GetRecordAsync(BusinessAccentThemeSetting.Key)).Should().BeNull("not yet registered");

        var theme = await BusinessAccentThemeSetting.GetThemeAsync(_service);

        theme.Key.Should().Be(BusinessAccentPalette.DefaultKey);
        var record = await _service.GetRecordAsync(BusinessAccentThemeSetting.Key);
        record.Should().NotBeNull();
        record!.Scope.Should().Be(SettingScope.Global);
        record.DataType.Should().Be(SettingDataType.Text);
        record.Value.Should().Be(BusinessAccentPalette.DefaultKey);
    }

    [Fact]
    public async Task GetThemeReflectsAnOperatorChoosingAPaletteColor()
    {
        await BusinessAccentThemeSetting.EnsureRegisteredAsync(_service);
        var record = await _service.GetRecordAsync(BusinessAccentThemeSetting.Key);

        await _service.SetValueAsync(BusinessAccentThemeSetting.Key, "lacivert", record!.RowVersion);

        var theme = await BusinessAccentThemeSetting.GetThemeAsync(_service);
        theme.Key.Should().Be("lacivert");
        theme.Hex.Should().Be("#1B4D7B");
    }

    [Fact]
    public async Task AStoredKeyOutsideThePaletteFallsBackToTheDefaultInsteadOfAnUnvettedColor()
    {
        await BusinessAccentThemeSetting.EnsureRegisteredAsync(_service);
        var record = await _service.GetRecordAsync(BusinessAccentThemeSetting.Key);
        // Simulates a stale value from a since-shrunk palette or a manual
        // DB edit — never something GetThemeAsync's own caller could write
        // through the palette-validated path.
        await _service.SetValueAsync(BusinessAccentThemeSetting.Key, "#FF00FF", record!.RowVersion);

        var theme = await BusinessAccentThemeSetting.GetThemeAsync(_service);

        theme.Key.Should().Be(BusinessAccentPalette.DefaultKey, "an unrecognized stored value must never reach a customer page");
    }
}
