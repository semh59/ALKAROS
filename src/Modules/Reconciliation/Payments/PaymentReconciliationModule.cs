using ALKAROS.ModuleComposition;
using ALKAROS.Reconciliation.CaseFoundation;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ALKAROS.Reconciliation.Payments;

/// <summary>
/// Registers <see cref="PaymentReconciliationScanner"/> with its full,
/// fixed list of source pairs (V13-REC-001's own In-scope list — 4 real,
/// 3 disabled pending an external contract). Separate from
/// <see cref="ReconciliationModule"/> so this task never has to write to
/// that shared module file (same reasoning as
/// <c>Cash.TenderHandlerModule</c>'s own separation from the rest of Cash);
/// depends on it for <see cref="IReconciliationService"/>. Registered in
/// <c>ModuleRegistry.DefaultCatalog</c>.
/// </summary>
public sealed class PaymentReconciliationModule : IModule
{
    public string Id => "Reconciliation.Payments";
    public string DisplayName => "Payment Reconciliation";

    public IReadOnlyCollection<string> DependsOn => ["Reconciliation"];

    public void Register(ModuleContext context)
    {
        context.RegisterSingleton<IReadOnlyList<IReconciliationSourcePair>>(provider =>
        {
            var dataSource = provider.GetRequiredService<NpgsqlDataSource>();
            return new List<IReconciliationSourcePair>
            {
                new PaymentUnknownSourcePair(dataSource),
                new ApprovedWithoutAllocationSourcePair(dataSource),
                new CardSettlementAllocationMismatchSourcePair(dataSource),
                new CashSessionDifferenceSourcePair(dataSource),

                // Disabled pending a real external contract (V13-GOV-008's
                // own reasoning, extended here to the source pairs the
                // task text did not spell out a conditional NotApplicable
                // clause for — see IReconciliationSourcePair's doc comment).
                new DisabledReconciliationSourcePair(
                    "FiscalMismatch",
                    "V13-FSC-001/V13-FSC-002 bloke: gerçek fiscal document lifecycle henüz yok (GATE-V13-FSC-STRATEGY)."),
                new DisabledReconciliationSourcePair(
                    "MealCardSettlementMismatch",
                    "V13-MCD-002/V13-MCD-004 bloke: gerçek yemek kartı sağlayıcı sözleşmesi/adaptörü henüz yok (V0-MCD-001)."),
                new DisabledReconciliationSourcePair(
                    "TerminalTotalsMismatch",
                    "V13-HUG-004 bloke: gerçek Token/Beko terminal toplamları beslemesi henüz yok (V0-HUG-001)."),
            };
        });
        context.RegisterTransient<PaymentReconciliationScanner, PaymentReconciliationScanner>();
    }
}
