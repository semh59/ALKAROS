using ALKAROS.ModuleComposition;

namespace ALKAROS.Payments.Allocations.Persistence;

/// <summary>
/// Registers PaymentAllocation persistence (V13-ALC-001), separate from
/// <c>PaymentAggregateModule</c> (owned by V13-PAY-001) so this task never
/// has to write to that shared file.
/// </summary>
public sealed class PaymentAllocationPersistenceModule : IModule
{
    public string Id => "Payments.Allocations.Persistence";
    public string DisplayName => "Payment Allocation Persistence";
    public IReadOnlyCollection<string> DependsOn => ["Payments", "Billing"];

    public void Register(ModuleContext context)
        => context.RegisterTransient<IPaymentAllocationRepository, PostgresPaymentAllocationRepository>();
}
