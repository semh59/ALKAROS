using Microsoft.Extensions.Configuration;

namespace ALKAROS.Host.Observability.MonitoringSwitch;

public static class MonitoringSwitchExtensions
{
    public const string SettingName = "Monitoring:Enabled";

    /// <summary>Monitoring is on unless the setting is explicitly false; a missing or unreadable value keeps it on.</summary>
    public static bool MonitoringEnabled(this IConfiguration configuration) =>
        !bool.TryParse(configuration[SettingName], out var enabled) || enabled;
}
