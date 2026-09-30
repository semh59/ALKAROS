namespace ALKAROS.Invoicing.Generation;

using ALKAROS.Invoicing.Generation.OrderInvoices;
using ALKAROS.ModuleComposition;

/// <summary>
/// V14-INV-002: registers invoice generation. module-dependency-rules.md row 34: it reads the buyer through
/// CustomerData's profile store and the source set, ledger, payments and billing by plain SQL (the read-model pattern
/// of Invoicing.SourceSelection), and writes only the invoicing schema.
/// </summary>
public sealed class InvoicingGenerationModule : IModule
{
    public string Id => "Invoicing.Generation";
    public string DisplayName => "Invoicing - Generation";

    public IReadOnlyCollection<string> DependsOn => ["CustomerData"];

    public void Register(ModuleContext context)
    {
        context.RegisterTransient<IInvoiceGenerationService, PostgresInvoiceGenerationService>();
        context.RegisterTransient<ISellerProfileStore, PostgresSellerProfileStore>();
        context.RegisterTransient<IOrderInvoiceDraftService, PostgresOrderInvoiceDraftService>();
    }
}
