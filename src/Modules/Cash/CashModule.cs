namespace ALKAROS.Cash;

using ALKAROS.Cash.Contracts;
using ALKAROS.ModuleComposition;

public sealed class CashModule : IModule
{
    public string Id => "Cash";
    public string DisplayName => "Cash Management";
    public IReadOnlyCollection<string> DependsOn => [];

    public void Register(ModuleContext context)
        => context.RegisterTransient<ICashSessionPolicy, CashSessionPolicy>();
}
