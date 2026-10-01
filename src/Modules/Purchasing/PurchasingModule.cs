using ALKAROS.ModuleComposition;
using ALKAROS.Purchasing.OrdersAndReceipts;
using ALKAROS.Purchasing.PurchaseInvoices;
using ALKAROS.Purchasing.Suppliers;

namespace ALKAROS.Purchasing;

/// <summary>
/// Composition module for supplier master data, purchase orders and goods
/// receipt (V1.1: purchasing).
/// </summary>
public sealed class PurchasingModule : IModule
{
    public string Id => "Purchasing";

    public string DisplayName => "Purchasing";

    // Goods receipt posts a stock movement (and its balance effect) into
    // Inventory's own schema within the same transaction, so a receipt and
    // its stock effect commit or roll back together — a direct-call edge
    // (V0-ARC-001 §2.1: same-transaction consistency boundary).
    public IReadOnlyCollection<string> DependsOn => ["Inventory"];

    public void Register(ModuleContext context)
    {
        context.RegisterTransient<ISupplierRepository, PostgresSupplierRepository>();
        context.RegisterTransient<ISupplierService, SupplierService>();

        context.RegisterTransient<IPurchaseOrderRepository, PostgresPurchaseOrderRepository>();
        context.RegisterTransient<IGoodsReceiptRepository, PostgresGoodsReceiptRepository>();
        context.RegisterTransient<IPurchasingService, PurchasingService>();

        context.RegisterTransient<IPurchaseInvoiceRepository, PostgresPurchaseInvoiceRepository>();
        context.RegisterTransient<IPurchaseInvoiceService, PurchaseInvoiceService>();
    }
}
