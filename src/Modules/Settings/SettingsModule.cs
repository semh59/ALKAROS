namespace ALKAROS.Settings;

using ALKAROS.ModuleComposition;
using ALKAROS.Settings.TypedSettings;

public sealed class SettingsModule : IModule
{
    public string Id => "Settings";
    public string DisplayName => "Settings Management";
    public IReadOnlyCollection<string> DependsOn => [];

    public void Register(ModuleContext context)
    {
        context.RegisterTransient<ISettingsRepository, PostgresSettingsRepository>();
        context.RegisterTransient<ISettingValidator, SettingValidator>();
        context.RegisterTransient<ISettingsService, SettingsService>();
    }
}
