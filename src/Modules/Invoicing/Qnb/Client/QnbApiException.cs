namespace ALKAROS.Invoicing.Qnb.Client;

/// <summary>
/// V14-QNB-007. Mirrors the style of `ALKAROS.Payments.Token.Draft.
/// TokenApiException` (and this class's own draft ancestor,
/// `evidence/V13-GOV-007/qnb-efatura-adapter-draft/`). Carries the raw
/// SOAP fault string when one was present — never shown to an end user
/// as-is (docs/UI_STYLE_GUIDE.md §3), only logged/wrapped.
/// </summary>
public sealed class QnbApiException : Exception
{
    public QnbApiException(string message) : base(message)
    {
    }

    public QnbApiException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
