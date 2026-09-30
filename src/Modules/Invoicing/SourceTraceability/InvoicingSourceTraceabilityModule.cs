namespace ALKAROS.Invoicing.SourceTraceability;

using ALKAROS.ModuleComposition;

/// <summary>
/// Registers invoice line traceability. It reuses Invoicing.Generation's KDV split so the links reproduce the lines
/// exactly, and reads the source set, ledger, payments and billing by plain SQL; it writes only the invoicing schema.
/// </summary>
public sealed class InvoicingSourceTraceabilityModule : IModule
{
    public string Id => "Invoicing.SourceTraceability";
    public string DisplayName => "Invoicing - Source Traceability";

    public IReadOnlyCollection<string> DependsOn => ["Invoicing.Generation"];

    public void Register(ModuleContext context)
        => context.RegisterTransient<IInvoiceTraceabilityService, PostgresInvoiceTraceabilityService>();
}
