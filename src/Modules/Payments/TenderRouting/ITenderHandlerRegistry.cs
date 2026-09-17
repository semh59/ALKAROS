namespace ALKAROS.Payments.TenderRouting;

/// <summary>
/// Looks up the registered handler for a tender method. Which concrete
/// handlers actually get registered (Cash/BankCard/MealCard, wired into DI)
/// is V13-PAY-003's composition — out of scope here; this is only the
/// lookup shape the router depends on.
/// </summary>
public interface ITenderHandlerRegistry
{
    bool TryGet(TenderMethod method, out ITenderHandler handler);
}
