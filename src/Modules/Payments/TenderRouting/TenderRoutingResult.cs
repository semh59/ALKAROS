namespace ALKAROS.Payments.TenderRouting;

/// <summary>
/// The router's own outcome — one layer above <see cref="TenderHandlerResult"/>,
/// since a request can fail before any handler ever runs (V0-ARC-004's
/// error-envelope spirit: a stable code plus a Turkish user-facing message,
/// even though this module has no HTTP surface of its own yet).
/// </summary>
public abstract record TenderRoutingResult;

/// <summary>A registered handler ran and produced <paramref name="Result"/>.</summary>
public sealed record TenderRoutingHandled(TenderHandlerResult Result) : TenderRoutingResult;

/// <summary>
/// <see cref="TenderRouting.TenderMethod"/> is a real, known method, but no
/// handler is registered for it in this composition (V13-PAY-003 wires the
/// real ones) — fails closed rather than fabricating success.
/// </summary>
public sealed record TenderMethodNotRegistered(TenderMethod Method) : TenderRoutingResult
{
    public const string Code = "TENDER_METHOD_NOT_REGISTERED";
    public string Message => $"'{Method}' ödeme yöntemi için kayıtlı bir işleyici yok.";
}

/// <summary>
/// The method is recognized by name but is not enabled in this version
/// (today: only <see cref="TenderMethod.CustomerAccount"/>, until
/// V14-ACC-008 wires its V1.4 handler) — never a generic "unknown method",
/// a distinct, typed signal that this is deliberate, versioned scope.
/// </summary>
public sealed record TenderVersionNotEnabled(TenderMethod Method) : TenderRoutingResult
{
    public const string Code = "TENDER_VERSION_NOT_ENABLED";
    public string Message => $"'{Method}' ödeme yöntemi bu sürümde etkin değil.";
}
