namespace ALKAROS.Settings.CashDrawer;

using System.Globalization;
using ALKAROS.Settings.TypedSettings;

/// <summary>Where the drawer pulse goes: the receipt printer the cash drawer is plugged into.</summary>
public sealed record CashDrawerTarget(string Host, int Port);

/// <summary>
/// The physical cash drawer is off by default and a business turns it on, with the address of the printer the
/// drawer is cabled to, once it has tried it on real hardware. The drawer is active only when the toggle is on AND the
/// address is valid; one printer serves the whole business (there is no per-terminal drawer).
/// </summary>
public static class CashDrawerSettings
{
    public const string EnabledKey = "cash.drawer_kick_enabled";
    public const string PrinterAddressKey = "cash.drawer_printer_address";
    private const string ModuleOwner = "cash";
    private const int MaxHostLength = 253;

    public static async Task EnsureRegisteredAsync(ISettingsService settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await RegisterAsync(
            settings, EnabledKey, "false", SettingDataType.Toggle,
            "Opens the cash drawer through the receipt printer when a cash payment is taken and lets a cashier open it " +
            "without a sale. Off by default: turn it on only after trying it on the real printer and drawer.",
            cancellationToken);
        await RegisterAsync(
            settings, PrinterAddressKey, string.Empty, SettingDataType.Text,
            "Address and port of the printer the cash drawer is plugged into, host:port (for example 192.168.1.50:9100). " +
            "Empty means no drawer.",
            cancellationToken);
    }

    /// <summary>The drawer target when the drawer is on and the address is valid; null otherwise. Registers the settings (off) the first time it is asked.</summary>
    public static async Task<CashDrawerTarget?> GetActiveTargetAsync(ISettingsService settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (await settings.GetRecordAsync(EnabledKey, cancellationToken) is null)
            await EnsureRegisteredAsync(settings, cancellationToken);
        var enabled = await settings.GetValueOrDefaultAsync(EnabledKey, false, cancellationToken);
        var address = await settings.GetValueOrDefaultAsync(PrinterAddressKey, string.Empty, cancellationToken);
        return ResolveTarget(enabled, address);
    }

    public static CashDrawerTarget? ResolveTarget(bool enabled, string? address)
        => enabled && TryParseAddress(address, out var host, out var port) ? new CashDrawerTarget(host, port) : null;

    public static bool TryParseAddress(string? address, out string host, out int port)
    {
        host = string.Empty;
        port = 0;
        var parts = address?.Split(':');
        if (parts is not { Length: 2 } || parts[0].Length is 0 or > MaxHostLength
            || !parts[0].All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-')
            || parts[0].Split('.').Any(label => label.Length == 0 || label[0] == '-' || label[^1] == '-')
            || !parts[1].All(char.IsAsciiDigit)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out port) || port is < 1 or > 65535)
            return false;
        host = parts[0];
        return true;
    }

    private static async Task RegisterAsync(
        ISettingsService settings, string key, string value, SettingDataType type, string description, CancellationToken cancellationToken)
    {
        try
        {
            await settings.RegisterSettingAsync(
                new RegisterSettingRequest(key, value, type, SettingScope.Global, ModuleOwner, Description: description),
                cancellationToken);
        }
        catch (DuplicateSettingKeyException)
        {
            // Already registered; a deployment's chosen value must never be silently reset.
        }
    }
}
