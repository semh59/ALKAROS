using ALKAROS.ModuleComposition;

namespace ALKAROS.Billing.PaymentClosure;

/// <summary>
/// Registers the Bill payment-closure projector (V13-ALC-002), separate
/// from `BillingModule` so this task never has to write to that shared
/// file — same reasoning as
/// <see cref="ALKAROS.Cash.TransactionLedger.CashTransactionLedgerModule"/>.
/// Not yet added to <c>ModuleRegistry.DefaultCatalog</c> — none of this
/// task's own dependencies (<c>PaymentAggregateModule</c> is there, but
/// <c>PaymentAllocationPersistenceModule</c> is not) are fully registered
/// there either; wiring the full V1.3 composition into the live Host is a
/// later composition task's own job (V13-FSC-002/V13-PAY-003).
/// </summary>
public sealed class BillPaymentClosureModule : IModule
{
    public string Id => "Billing.PaymentClosure";
    public string DisplayName => "Bill Payment Closure Projection";
    public IReadOnlyCollection<string> DependsOn =>
        ["Billing", "Payments", "Payments.Allocations.Persistence"];

    public void Register(ModuleContext context)
        => context.RegisterTransient<IBillPaymentClosureProjector, BillPaymentClosureProjector>();
}
