namespace ALKAROS.Support;

using ALKAROS.ModuleComposition;
using ALKAROS.Support.DiagnosticBundle;

public sealed class SupportModule : IModule
{
    public string Id => "Support";
    public string DisplayName => "Support Tooling";
    public IReadOnlyCollection<string> DependsOn => ["Observability", "Audit"];

    public void Register(ModuleContext context)
    {
        context.RegisterTransient<ISecretPatternScanner, SecretPatternScanner>();
        context.RegisterTransient<IDiagnosticBundleService, DiagnosticBundleService>();
    }
}
