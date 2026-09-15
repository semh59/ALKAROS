using ALKAROS.Settings.WaiterMaxActiveTables.Tests.Fixtures;
using ALKAROS.Settings.TypedSettings;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Settings.WaiterMaxActiveTables.Tests;

/// <summary>
/// One fresh isolated database per test (not a shared class/collection
/// fixture): <see cref="WaiterMaxActiveTablesSetting.Key"/> is a fixed
/// constant, so sharing one database across test methods would make the
/// tests order-dependent — same reasoning as KitchenDenseModeThresholdSettingTests
/// (V1-SET-005).
/// </summary>
public sealed class WaiterMaxActiveTablesSettingTests : IAsyncLifetime
{
    private readonly WaiterMaxActiveTablesTestDatabase _db = new();
    private SettingsService _service = null!;

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();
        _service = new SettingsService(new PostgresSettingsRepository(_db.DataSource, new SettingValidator()));
    }

    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task GetLimitRegistersTheSettingAtZeroTheFirstTimeItIsAsked()
    {
        (await _service.GetRecordAsync(WaiterMaxActiveTablesSetting.Key)).Should().BeNull("not yet registered");

        var limit = await WaiterMaxActiveTablesSetting.GetLimitAsync(_service);

        limit.Should().Be(0);
        var record = await _service.GetRecordAsync(WaiterMaxActiveTablesSetting.Key);
        record.Should().NotBeNull();
        record!.Scope.Should().Be(SettingScope.Global);
        record.DataType.Should().Be(SettingDataType.WholeNumber);
        record.Value.Should().Be("0");
    }

    [Fact]
    public async Task EnsureRegisteredIsIdempotent()
    {
        await WaiterMaxActiveTablesSetting.EnsureRegisteredAsync(_service);
        var first = await _service.GetRecordAsync(WaiterMaxActiveTablesSetting.Key);

        await FluentActions.Invoking(() => WaiterMaxActiveTablesSetting.EnsureRegisteredAsync(_service))
            .Should().NotThrowAsync("a second registration must be a no-op, not an error");

        var second = await _service.GetRecordAsync(WaiterMaxActiveTablesSetting.Key);
        second!.RowVersion.Should().Be(first!.RowVersion, "the existing row must not be touched");
    }

    [Fact]
    public async Task GetLimitReflectsAnOperatorChangingIt()
    {
        await WaiterMaxActiveTablesSetting.EnsureRegisteredAsync(_service);
        var record = await _service.GetRecordAsync(WaiterMaxActiveTablesSetting.Key);

        await _service.SetValueAsync(WaiterMaxActiveTablesSetting.Key, 5, record!.RowVersion);

        (await WaiterMaxActiveTablesSetting.GetLimitAsync(_service)).Should().Be(5);
    }

    [Fact]
    public async Task GetLimitDefaultsToZeroWhenTheSettingWasNeverTouched()
        => (await WaiterMaxActiveTablesSetting.GetLimitAsync(_service)).Should().Be(0);
}
