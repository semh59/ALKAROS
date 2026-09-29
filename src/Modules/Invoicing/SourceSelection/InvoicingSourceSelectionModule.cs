namespace ALKAROS.Invoicing.SourceSelection;

using ALKAROS.ModuleComposition;

/// <summary>
/// V14-INV-001: registers periodic invoice source selection. module-dependency-rules.md row 33: it reads the
/// customer account ledger by plain SQL (the read-model pattern of Reconciliation.Payments) and writes only the
/// invoicing schema, so it declares no module dependency.
/// </summary>
public sealed class InvoicingSourceSelectionModule : IModule
{
    public string Id => "Invoicing.SourceSelection";
    public string DisplayName => "Invoicing - Source Selection";

    public IReadOnlyCollection<string> DependsOn => [];

    public void Register(ModuleContext context)
        => context.RegisterTransient<IInvoiceSourceSelectionService, PostgresInvoiceSourceSelectionService>();
}
