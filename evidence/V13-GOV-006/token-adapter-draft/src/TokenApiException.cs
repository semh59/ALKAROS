namespace ALKAROS.Payments.Token.Draft;

/// <summary>
/// V13-GOV-006 DRAFT — mirrors the style of
/// <c>ALKAROS.QrRelay.PublicGateway.CloudflareApiException</c> (the only
/// existing external-HTTP-provider exception in this codebase). Carries the
/// raw Token `status` code (see <c>docs</c> in README.md for the known
/// subset: 6, 1006, 1018, 1100, 1104, 1105, 403, 404 — all taken from the
/// real Postman collection's saved example responses, not guessed).
/// </summary>
public sealed class TokenApiException(string message, int? tokenStatusCode = null) : Exception(message)
{
    public int? TokenStatusCode { get; } = tokenStatusCode;
}
