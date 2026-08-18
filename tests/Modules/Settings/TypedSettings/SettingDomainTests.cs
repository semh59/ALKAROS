using FluentAssertions;
using Xunit;

namespace ALKAROS.Settings.TypedSettings.Tests;

public sealed class SettingDomainTests
{
    private readonly SettingValidator _validator = new();

    [Theory]
    [InlineData("table_management.auto_release_timeout", "00:15:00", SettingDataType.Duration)]
    [InlineData("billing.default_tax_rate", "0.20", SettingDataType.PreciseNumber)]
    [InlineData("orders.max_items_per_order", "100", SettingDataType.WholeNumber)]
    [InlineData("kitchen.sound_alerts_enabled", "true", SettingDataType.Toggle)]
    [InlineData("pos.company_header", "Alkaros Restaurant", SettingDataType.Text)]
    [InlineData("catalog.category_display_order", "{\"appetizers\": 1, \"mains\": 2}", SettingDataType.Json)]
    public void ValidatorAcceptsValidTypedValues(string key, string value, SettingDataType type)
    {
        var actKey = () => _validator.ValidateKey(key);
        var actVal = () => _validator.ValidateValue(key, value, type);

        actKey.Should().NotThrow();
        actVal.Should().NotThrow();
    }

    [Theory]
    [InlineData("billing.default_tax_rate", "not_a_decimal", SettingDataType.PreciseNumber)]
    [InlineData("orders.max_items", "12.34", SettingDataType.WholeNumber)]
    [InlineData("orders.max_items", "abc", SettingDataType.WholeNumber)]
    [InlineData("kitchen.alerts", "yes", SettingDataType.Toggle)]
    [InlineData("catalog.json", "{ invalid json }", SettingDataType.Json)]
    [InlineData("table.timeout", "invalid_duration", SettingDataType.Duration)]
    public void ValidatorRejectsInvalidTypedValues(string key, string value, SettingDataType type)
    {
        var act = () => _validator.ValidateValue(key, value, type);

        var ex = act.Should().Throw<SettingTypeValidationException>().Which;
        ex.SettingKey.Should().Be(key);
        ex.ExpectedType.Should().Be(type);
        ex.ActualValue.Should().Be(value);
    }

    [Theory]
    [InlineData("integrations.qnb.api_key")]
    [InlineData("integrations.qnb.client_secret")]
    [InlineData("integrations.hugin.device_password")]
    [InlineData("identity.jwt.token_signing_key")]
    [InlineData("auth.credentials.admin")]
    [InlineData("integrations.yemeksepeti.auth_token")]
    public void ValidatorRejectsSecretKeysUnderSecretStorageBan(string bannedKey)
    {
        var act = () => _validator.ValidateKey(bannedKey);

        var ex = act.Should().Throw<SecretSettingsStorageBanException>().Which;
        ex.SettingKey.Should().Be(bannedKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ValidatorRejectsNullOrWhitespaceKey(string? invalidKey)
    {
        var act = () => _validator.ValidateKey(invalidKey!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegisterSettingRequestValidation()
    {
        var valid = new RegisterSettingRequest("app.name", "Alkaros", SettingDataType.Text, SettingScope.Global, "Core");
        valid.Validate();

        var invalidKey = new RegisterSettingRequest("", "Alkaros", SettingDataType.Text, SettingScope.Global, "Core");
        var actInvalidKey = () => invalidKey.Validate();
        actInvalidKey.Should().Throw<ArgumentException>().WithParameterName("Key");

        var invalidOwner = new RegisterSettingRequest("app.name", "Alkaros", SettingDataType.Text, SettingScope.Global, "");
        var actInvalidOwner = () => invalidOwner.Validate();
        actInvalidOwner.Should().Throw<ArgumentException>().WithParameterName("ModuleOwner");
    }

    [Fact]
    public void UpdateSettingRequestValidation()
    {
        var valid = new UpdateSettingRequest("app.name", "NewName", 1);
        valid.Validate();

        var invalidVersion = new UpdateSettingRequest("app.name", "NewName", 0);
        var actInvalidVersion = () => invalidVersion.Validate();
        actInvalidVersion.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("ExpectedRowVersion");
    }

    [Fact]
    public void SettingRecordConstructsAndCapturesProperties()
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var record = new SettingRecord(
            id,
            "table.auto_release",
            "00:30:00",
            SettingDataType.Duration,
            SettingScope.Module,
            "Tables",
            "Auto release timeout for idle tables",
            RequiresRestart: false,
            Active: true,
            now,
            RowVersion: 1);

        record.SettingId.Should().Be(id);
        record.Key.Should().Be("table.auto_release");
        record.Value.Should().Be("00:30:00");
        record.DataType.Should().Be(SettingDataType.Duration);
        record.Scope.Should().Be(SettingScope.Module);
        record.ModuleOwner.Should().Be("Tables");
        record.Description.Should().Be("Auto release timeout for idle tables");
        record.RequiresRestart.Should().BeFalse();
        record.Active.Should().BeTrue();
        record.IsActive.Should().BeTrue();
        record.UpdatedAt.Should().Be(now);
        record.RowVersion.Should().Be(1);
    }
}
