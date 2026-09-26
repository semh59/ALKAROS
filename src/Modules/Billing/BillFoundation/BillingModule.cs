using ALKAROS.ModuleComposition;
using ALKAROS.Billing.Adjustments;
using ALKAROS.Billing.Integration;
using ALKAROS.Billing.SplitDesign;
using ALKAROS.IntegrationContracts;

namespace ALKAROS.Billing.BillFoundation;

/// <summary>
/// Composition module for the Billing bounded context (PDF:II.2.5).
/// Registers Billing repositories and services with the host composition root.
/// </summary>
public sealed class BillingModule : IModule
{
    public string Id => "Billing";

    public string DisplayName => "Billing";

    public IReadOnlyCollection<string> DependsOn => new[] { "Orders" };

    public void Register(ModuleContext context)
    {
        context.RegisterTransient<IBillRepository, PostgresBillRepository>();
        context.RegisterTransient<ISplitDesignRepository, PostgresSplitDesignRepository>();
        // V1-RMD-298: needed by Payments.Allocations.Persistence and Billing.PaymentClosure (both already
        // depend on "Billing") to compute the real, adjustment-aware payable ceiling - registered once here
        // rather than in each dependent module, matching IBillRepository's own single-owner precedent above.
        context.RegisterTransient<IBillAdjustmentRepository, PostgresBillAdjustmentRepository>();

        // Reacts to Table Management merge/transfer/unmerge events by moving
        // Bill's own rows to the new table (V0-ARC-001 row 3).
        context.RegisterTransient<IIntegrationEventConsumer, TableEventBillConsumer>();
    }
}
