using ALKAROS.Settings.KitchenDenseModeThreshold.Tests.Fixtures;
using ALKAROS.Settings.TypedSettings;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Settings.KitchenDenseModeThreshold.Tests;

/// <summary>
/// One fresh isolated database per test (not a shared class/collection
/// fixture): <see cref="KitchenDenseModeThresholdSetting.Key"/> is a fixed
/// constant, so sharing one database across test methods would make the
/// tests order-dependent (whichever runs first registers the row for the
/// rest) — same reasoning as KitchenLiveSyncSettingTests (V1-SET-002).
/// </summary>
public sealed class KitchenDenseModeThresholdSettingTests : IAsyncLifetime
{
    private readonly KitchenDenseModeThresholdTestDatabase _db = new();
    private SettingsService _service = null!;

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();
        _service = new SettingsService(new PostgresSettingsRepository(_db.DataSource, new SettingValidator()));
    }

    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task GetThresholdRegistersTheSettingAtNineTheFirstTimeItIsAsked()
    {
        (await _service.GetRecordAsync(KitchenDenseModeThresholdSetting.Key)).Should().BeNull("not yet registered");

        var threshold = await KitchenDenseModeThresholdSetting.GetThresholdAsync(_service);

        threshold.Should().Be(9);
        var record = await _service.GetRecordAsync(KitchenDenseModeThresholdSetting.Key);
        record.Should().NotBeNull();
        record!.Scope.Should().Be(SettingScope.Global);
        record.DataType.Should().Be(SettingDataType.WholeNumber);
        record.Value.Should().Be("9");
    }

    [Fact]
    public async Task EnsureRegisteredIsIdempotent()
    {
        await KitchenDenseModeThresholdSetting.EnsureRegisteredAsync(_service);
        var first = await _service.GetRecordAsync(KitchenDenseModeThresholdSetting.Key);

        await FluentActions.Invoking(() => KitchenDenseModeThresholdSetting.EnsureRegisteredAsync(_service))
            .Should().NotThrowAsync("a second registration must be a no-op, not an error");

        var second = await _service.GetRecordAsync(KitchenDenseModeThresholdSetting.Key);
        second!.RowVersion.Should().Be(first!.RowVersion, "the existing row must not be touched");
    }

    [Fact]
    public async Task GetThresholdReflectsAnOperatorChangingIt()
    {
        await KitchenDenseModeThresholdSetting.EnsureRegisteredAsync(_service);
        var record = await _service.GetRecordAsync(KitchenDenseModeThresholdSetting.Key);

        await _service.SetValueAsync(KitchenDenseModeThresholdSetting.Key, 15, record!.RowVersion);

        (await KitchenDenseModeThresholdSetting.GetThresholdAsync(_service)).Should().Be(15);
    }

    [Fact]
    public async Task GetThresholdDefaultsToNineWhenTheSettingWasNeverTouched()
        => (await KitchenDenseModeThresholdSetting.GetThresholdAsync(_service)).Should().Be(9);
}
