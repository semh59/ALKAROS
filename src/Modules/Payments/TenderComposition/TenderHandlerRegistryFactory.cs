namespace ALKAROS.Payments.TenderRouting;

/// <summary>
/// Builds the single, fail-closed <see cref="TenderHandlerRegistry"/> this
/// composition wires into DI (V13-PAY-003's Goal). Registers exactly Cash
/// (<see cref="CashTenderMethodAdapter"/>), BankCard
/// (<see cref="PendingBankCardTerminalIntegrationHandler"/>) and EFT/Havale
/// (V13-PAY-005's <c>ALKAROS.Payments.EftTender.EftTenderHandler</c>) —
/// never MealCard, which stays genuinely unregistered until a real provider
/// adapter and V13-MCD-004's durable workflow exist (the router's own
/// <see cref="TenderMethodNotRegistered"/> already gives it the correct
/// "typed unavailable" behaviour with zero code here).
/// <see cref="TenderHandlerRegistry.Register"/> already throws on a
/// duplicate method (V13-PAY-002) — this factory relies on that, it does
/// not re-implement duplicate detection.
/// </summary>
public static class TenderHandlerRegistryFactory
{
    public static ITenderHandlerRegistry Build(
        ITenderHandler cashHandler, ITenderHandler bankCardHandler, ITenderHandler eftHandler)
    {
        ArgumentNullException.ThrowIfNull(cashHandler);
        ArgumentNullException.ThrowIfNull(bankCardHandler);
        ArgumentNullException.ThrowIfNull(eftHandler);

        var registry = new TenderHandlerRegistry();
        registry.Register(cashHandler);
        registry.Register(bankCardHandler);
        registry.Register(eftHandler);
        return registry;
    }
}
