namespace ALKAROS.Invoicing.Qnb.Draft;

/// <summary>
/// V13-GOV-007 DRAFT — mirrors the style of
/// `ALKAROS.QrRelay.PublicGateway.CloudflareApiException` and
/// `ALKAROS.Payments.Token.Draft.TokenApiException` (this codebase's two
/// existing external-provider exception types). Carries the raw SOAP
/// fault string when one was present.
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
