namespace ALKAROS.Payments.TenderRouting;

/// <summary>
/// A plain dictionary-backed <see cref="ITenderHandlerRegistry"/> — the
/// reusable data structure every real composition (V13-PAY-003) populates
/// with its concrete handlers, rather than each caller hand-rolling its own.
/// Registering the same method twice fails closed at startup (PDF:II.5's
/// "failures create recoverable state or ... terminal states cannot
/// silently reopen" spirit — a silent second registration would leave it
/// undefined which handler actually runs).
/// </summary>
public sealed class TenderHandlerRegistry : ITenderHandlerRegistry
{
    private readonly Dictionary<TenderMethod, ITenderHandler> _handlers = [];

    public void Register(ITenderHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (!_handlers.TryAdd(handler.Method, handler))
            throw new InvalidOperationException(
                $"A handler for tender method '{handler.Method}' is already registered.");
    }

    public bool TryGet(TenderMethod method, out ITenderHandler handler)
        => _handlers.TryGetValue(method, out handler!);
}
