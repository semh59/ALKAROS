using ALKAROS.Settings.KitchenLiveSync.Tests.Fixtures;
using ALKAROS.Settings.TypedSettings;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Settings.KitchenLiveSync.Tests;

/// <summary>
/// One fresh isolated database per test (not a shared class/collection
/// fixture): <see cref="KitchenLiveSyncSetting.Key"/> is a fixed constant,
/// so sharing one database across test methods would make the tests
/// order-dependent (whichever runs first registers the row for the rest).
/// </summary>
public sealed class KitchenLiveSyncSettingTests : IAsyncLifetime
{
    private readonly KitchenLiveSyncTestDatabase _db = new();
    private SettingsService _service = null!;

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();
        _service = new SettingsService(new PostgresSettingsRepository(_db.DataSource, new SettingValidator()));
    }

    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task IsEnabledRegistersTheSettingAtFalseTheFirstTimeItIsAsked()
    {
        (await _service.GetRecordAsync(KitchenLiveSyncSetting.Key)).Should().BeNull("not yet registered");

        var enabled = await KitchenLiveSyncSetting.IsEnabledAsync(_service);

        enabled.Should().BeFalse();
        var record = await _service.GetRecordAsync(KitchenLiveSyncSetting.Key);
        record.Should().NotBeNull();
        record!.Scope.Should().Be(SettingScope.Global);
        record.DataType.Should().Be(SettingDataType.Toggle);
        record.Value.Should().Be("false");
    }

    [Fact]
    public async Task EnsureRegisteredIsIdempotent()
    {
        await KitchenLiveSyncSetting.EnsureRegisteredAsync(_service);
        var first = await _service.GetRecordAsync(KitchenLiveSyncSetting.Key);

        await FluentActions.Invoking(() => KitchenLiveSyncSetting.EnsureRegisteredAsync(_service))
            .Should().NotThrowAsync("a second registration must be a no-op, not an error");

        var second = await _service.GetRecordAsync(KitchenLiveSyncSetting.Key);
        second!.RowVersion.Should().Be(first!.RowVersion, "the existing row must not be touched");
    }

    [Fact]
    public async Task IsEnabledReflectsAnOperatorTurningItOn()
    {
        await KitchenLiveSyncSetting.EnsureRegisteredAsync(_service);
        var record = await _service.GetRecordAsync(KitchenLiveSyncSetting.Key);

        await _service.SetValueAsync(KitchenLiveSyncSetting.Key, true, record!.RowVersion);

        (await KitchenLiveSyncSetting.IsEnabledAsync(_service)).Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledDefaultsFalseWhenTheSettingWasNeverTouched()
        => (await KitchenLiveSyncSetting.IsEnabledAsync(_service)).Should().BeFalse();
}
