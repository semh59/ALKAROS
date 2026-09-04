using ALKAROS.Settings.ReservationStation.Tests.Fixtures;
using ALKAROS.Settings.TypedSettings;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Settings.ReservationStation.Tests;

/// <summary>
/// One fresh isolated database per test (not a shared class/collection
/// fixture): <see cref="ReservationStationSetting.Key"/> is a fixed
/// constant, so sharing one database across test methods would make the
/// tests order-dependent (whichever runs first registers the row for the
/// rest).
/// </summary>
public sealed class ReservationStationSettingTests : IAsyncLifetime
{
    private readonly ReservationStationTestDatabase _db = new();
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
        (await _service.GetRecordAsync(ReservationStationSetting.Key)).Should().BeNull("not yet registered");

        var enabled = await ReservationStationSetting.IsEnabledAsync(_service);

        enabled.Should().BeFalse();
        var record = await _service.GetRecordAsync(ReservationStationSetting.Key);
        record.Should().NotBeNull();
        record!.Scope.Should().Be(SettingScope.Global);
        record.DataType.Should().Be(SettingDataType.Toggle);
        record.Value.Should().Be("false");
    }

    [Fact]
    public async Task EnsureRegisteredIsIdempotent()
    {
        await ReservationStationSetting.EnsureRegisteredAsync(_service);
        var first = await _service.GetRecordAsync(ReservationStationSetting.Key);

        await FluentActions.Invoking(() => ReservationStationSetting.EnsureRegisteredAsync(_service))
            .Should().NotThrowAsync("a second registration must be a no-op, not an error");

        var second = await _service.GetRecordAsync(ReservationStationSetting.Key);
        second!.RowVersion.Should().Be(first!.RowVersion, "the existing row must not be touched");
    }

    [Fact]
    public async Task IsEnabledReflectsAnOperatorTurningItOn()
    {
        await ReservationStationSetting.EnsureRegisteredAsync(_service);
        var record = await _service.GetRecordAsync(ReservationStationSetting.Key);

        await _service.SetValueAsync(ReservationStationSetting.Key, true, record!.RowVersion);

        (await ReservationStationSetting.IsEnabledAsync(_service)).Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledDefaultsFalseWhenTheSettingWasNeverTouched()
        => (await ReservationStationSetting.IsEnabledAsync(_service)).Should().BeFalse();
}
