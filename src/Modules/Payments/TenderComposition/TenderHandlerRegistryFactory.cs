namespace ALKAROS.Payments.TenderRouting;

/// <summary>
/// Builds the single, fail-closed <see cref="TenderHandlerRegistry"/> this
/// composition wires into DI (V13-PAY-003's Goal). Registers exactly Cash
/// (<see cref="CashTenderMethodAdapter"/>) and BankCard
/// (<see cref="PendingBankCardTerminalIntegrationHandler"/>) — never
/// MealCard, which stays genuinely unregistered until a real provider
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
        ITenderHandler cashHandler, ITenderHandler bankCardHandler)
    {
        ArgumentNullException.ThrowIfNull(cashHandler);
        ArgumentNullException.ThrowIfNull(bankCardHandler);

        var registry = new TenderHandlerRegistry();
        registry.Register(cashHandler);
        registry.Register(bankCardHandler);
        return registry;
    }
}
