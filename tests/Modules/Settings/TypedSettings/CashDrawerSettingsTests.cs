using ALKAROS.Settings.CashDrawer;
using ALKAROS.Settings.TypedSettings;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Settings.TypedSettings.Tests;

public sealed class CashDrawerSettingsTests
{
    [Theory]
    [InlineData("192.168.1.50:9100", "192.168.1.50", 9100)]
    [InlineData("kasa-yazici.lan:9100", "kasa-yazici.lan", 9100)]
    [InlineData("printer:65535", "printer", 65535)]
    public void AHostAndPortAreParsed(string address, string host, int port)
    {
        CashDrawerSettings.TryParseAddress(address, out var parsedHost, out var parsedPort).Should().BeTrue();
        (parsedHost, parsedPort).Should().Be((host, port));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("192.168.1.50")]
    [InlineData("192.168.1.50:0")]
    [InlineData("192.168.1.50:65536")]
    [InlineData("192.168.1.50:abc")]
    [InlineData(":9100")]
    [InlineData("kasa yazici:9100")]
    [InlineData("http://192.168.1.50:9100")]
    [InlineData("a:1:2")]
    [InlineData(" 192.168.1.50:9100")]
    [InlineData("192.168.1.50:9100 ")]
    [InlineData("..:9100")]
    [InlineData("-a-:9100")]
    [InlineData("host.:9100")]
    public void ABadAddressIsRefused(string address)
        => CashDrawerSettings.TryParseAddress(address, out _, out _).Should().BeFalse();

    [Fact]
    public void AHostLongerThanTheNameLimitIsRefused()
        => CashDrawerSettings.TryParseAddress(new string('a', 254) + ":9100", out _, out _).Should().BeFalse();

    [Theory]
    [InlineData(false, "192.168.1.50:9100")]
    [InlineData(true, "")]
    [InlineData(true, "not-an-address")]
    public void TheDrawerIsInactiveWhenOffOrWhenThereIsNoValidAddress(bool enabled, string address)
        => CashDrawerSettings.ResolveTarget(enabled, address).Should().BeNull();

    [Fact]
    public void TheDrawerIsActiveOnlyWhenOnAndTheAddressIsValid()
        => CashDrawerSettings.ResolveTarget(true, "192.168.1.50:9100").Should().Be(new CashDrawerTarget("192.168.1.50", 9100));

    [Theory]
    [InlineData("")]
    [InlineData("192.168.1.50:9100")]
    public void TheValidatorAcceptsAnEmptyOrWellFormedPrinterAddress(string value)
        => new SettingValidator().Invoking(v => v.ValidateValue(CashDrawerSettings.PrinterAddressKey, value, SettingDataType.Text))
            .Should().NotThrow();

    [Theory]
    [InlineData("192.168.1.50")]
    [InlineData("192.168.1.50:70000")]
    [InlineData("bir iki:9100")]
    public void TheValidatorRefusesAMalformedPrinterAddressInTurkish(string value)
        => new SettingValidator().Invoking(v => v.ValidateValue(CashDrawerSettings.PrinterAddressKey, value, SettingDataType.Text))
            .Should().Throw<SettingValueRangeException>().Which.UserMessage.Should().Contain("adres:port");

    [Fact]
    public void NeitherKeyTripsTheSecretStorageBan()
    {
        var validator = new SettingValidator();
        validator.Invoking(v => v.ValidateKey(CashDrawerSettings.EnabledKey)).Should().NotThrow();
        validator.Invoking(v => v.ValidateKey(CashDrawerSettings.PrinterAddressKey)).Should().NotThrow();
    }
}
