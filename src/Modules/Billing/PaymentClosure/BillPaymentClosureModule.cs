using ALKAROS.ModuleComposition;

namespace ALKAROS.Billing.PaymentClosure;

/// <summary>
/// Registers the Bill payment-closure projector, separate from
/// <c>BillingModule</c> so that module's file stays untouched — same reasoning as
/// <see cref="ALKAROS.Cash.TransactionLedger.CashTransactionLedgerModule"/>.
/// </summary>
public sealed class BillPaymentClosureModule : IModule
{
    public string Id => "Billing.PaymentClosure";
    public string DisplayName => "Bill Payment Closure Projection";
    public IReadOnlyCollection<string> DependsOn =>
        ["Billing", "Payments", "Payments.Allocations.Persistence"];

    public void Register(ModuleContext context)
    {
        context.RegisterTransient<IBillPaymentClosureProjector, BillPaymentClosureProjector>();
        context.RegisterTransient<IBillClosureService, BillClosureService>();
    }
}
