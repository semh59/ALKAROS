using ALKAROS.ModuleComposition;

namespace ALKAROS.Payments.PaymentAggregate;

public sealed class PaymentAggregateModule : IModule
{
    public string Id => "Payments";
    public string DisplayName => "Payments";
    public IReadOnlyCollection<string> DependsOn => [];

    public void Register(ModuleContext context)
        => context.RegisterTransient<IPaymentRepository, PostgresPaymentRepository>();
}
