namespace ALKAROS.Payments.TenderRouting;

/// <summary>
/// Routes a <see cref="TenderRequest"/> to its registered handler, or a
/// typed rejection when none exists — never a raw exception and never a
/// fabricated success. <see cref="TenderMethod.CustomerAccount"/> is
/// rejected unconditionally (V13-PAY-002's own scope: a version-not-enabled
/// result, regardless of whether a registry happens to hold a handler for
/// it — none ever should, until V14-ACC-008).
/// </summary>
public sealed class TenderRouter
{
    private readonly ITenderHandlerRegistry _registry;

    public TenderRouter(ITenderHandlerRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public async Task<TenderRoutingResult> RouteAsync(
        TenderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        if (request.Method == TenderMethod.CustomerAccount)
            return new TenderVersionNotEnabled(request.Method);

        if (!_registry.TryGet(request.Method, out var handler))
            return new TenderMethodNotRegistered(request.Method);

        var result = await handler.HandleAsync(request, cancellationToken);
        return new TenderRoutingHandled(result);
    }
}
