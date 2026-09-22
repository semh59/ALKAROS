using ALKAROS.Settings.BusinessIdentity.Tests.Fixtures;
using ALKAROS.Settings.TypedSettings;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Settings.BusinessIdentity.Tests;

/// <summary>
/// One fresh isolated database per test (not a shared class/collection
/// fixture): <see cref="BusinessNameSetting.Key"/> is a fixed constant, so
/// sharing one database across test methods would make the tests
/// order-dependent — same reasoning as WaiterMaxActiveTablesSettingTests
/// (V1-SET-006).
/// </summary>
public sealed class BusinessNameSettingTests : IAsyncLifetime
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
    public async Task GetNameRegistersTheSettingAsEmptyTheFirstTimeItIsAsked()
    {
        (await _service.GetRecordAsync(BusinessNameSetting.Key)).Should().BeNull("not yet registered");

        var name = await BusinessNameSetting.GetNameAsync(_service);

        name.Should().Be("");
        var record = await _service.GetRecordAsync(BusinessNameSetting.Key);
        record.Should().NotBeNull();
        record!.Scope.Should().Be(SettingScope.Global);
        record.DataType.Should().Be(SettingDataType.Text);
        record.Value.Should().Be("");
    }

    [Fact]
    public async Task EnsureRegisteredIsIdempotent()
    {
        await BusinessNameSetting.EnsureRegisteredAsync(_service);
        var first = await _service.GetRecordAsync(BusinessNameSetting.Key);

        await FluentActions.Invoking(() => BusinessNameSetting.EnsureRegisteredAsync(_service))
            .Should().NotThrowAsync("a second registration must be a no-op, not an error");

        var second = await _service.GetRecordAsync(BusinessNameSetting.Key);
        second!.RowVersion.Should().Be(first!.RowVersion, "the existing row must not be touched");
    }

    [Fact]
    public async Task GetNameReflectsAnOperatorSettingIt()
    {
        await BusinessNameSetting.EnsureRegisteredAsync(_service);
        var record = await _service.GetRecordAsync(BusinessNameSetting.Key);

        await _service.SetValueAsync(BusinessNameSetting.Key, "Sahil Cafe", record!.RowVersion);

        (await BusinessNameSetting.GetNameAsync(_service)).Should().Be("Sahil Cafe");
    }

    [Fact]
    public async Task GetNameDefaultsToEmptyWhenTheSettingWasNeverTouched()
        => (await BusinessNameSetting.GetNameAsync(_service)).Should().Be("");
}
