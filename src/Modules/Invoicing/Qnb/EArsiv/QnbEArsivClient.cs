using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using ALKAROS.Invoicing.Qnb.Client;

namespace ALKAROS.Invoicing.Qnb.EArsiv;

/// <summary>What one e-Arsiv invoice needs: the issuing taxpayer, the portal's branch/cash-desk pair, the integration code and the signed-to-be UBL.</summary>
public sealed record QnbEArsivInvoice(
    string TaxNumber, string Branch, string CashDesk, string ErpCode, Guid InvoiceUuid, string UblXml);

public sealed record QnbEArsivSendResult(string InvoiceNumber, string Uuid, string? ViewUrl);

/// <summary>
/// QNB eSolutions e-Arsiv: <c>faturaOlusturExt</c> on <c>EarsivWebService</c> (WSDL
/// <c>https://earsivtest.qnbesolutions.com.tr/earsiv/ws/EarsivWebService?wsdl</c>, namespace
/// <c>http://service.earsiv.uut.cs.com.tr/</c>) with login on the e-Arsiv server's own <c>userService</c>. The request is
/// <c>input</c> (JSON text) plus <c>fatura</c> (<c>belgeFormati</c> UBL, <c>belgeIcerigi</c> base64), as QNB's e-Arsiv send page and the service XSD describe it. UNVERIFIED against a live tenant: the shape comes from the
/// published WSDL/XSD and page only. A success is result code <c>AE00000</c>; the service is synchronous.
/// </summary>
public sealed class QnbEArsivClient
{
    public const string ServiceNamespace = "http://service.earsiv.uut.cs.com.tr/";
    public const string SuccessCode = "AE00000";

    private readonly QnbSoapClient _soap;
    private readonly string _serviceUrl;

    public QnbEArsivClient(HttpClient httpClient, string userServiceUrl, string serviceUrl)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(userServiceUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceUrl);
        _soap = new QnbSoapClient(httpClient, userServiceUrl);
        _serviceUrl = serviceUrl;
    }

    public Task LoginAsync(string userId, string password, CancellationToken cancellationToken = default) =>
        _soap.LoginAsync(userId, password, "tr", cancellationToken);

    /// <summary>Creates the e-Arsiv invoice (no document image is asked back: <c>donenBelgeFormati</c> 9).</summary>
    public async Task<QnbEArsivSendResult> SendInvoiceAsync(QnbEArsivInvoice invoice, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentException.ThrowIfNullOrWhiteSpace(invoice.TaxNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(invoice.Branch);
        ArgumentException.ThrowIfNullOrWhiteSpace(invoice.CashDesk);
        ArgumentException.ThrowIfNullOrWhiteSpace(invoice.ErpCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(invoice.UblXml);

        var input = JsonSerializer.Serialize(new
        {
            islemId = invoice.InvoiceUuid.ToString("D", CultureInfo.InvariantCulture),
            vkn = invoice.TaxNumber,
            sube = invoice.Branch,
            kasa = invoice.CashDesk,
            erpKodu = invoice.ErpCode,
            donenBelgeFormati = "9",
        });
        var body = $"<input>{QnbSoapClient.EscapeXml(input)}</input>"
            + $"<fatura><belgeFormati>UBL</belgeFormati><belgeIcerigi>{Convert.ToBase64String(Encoding.UTF8.GetBytes(invoice.UblXml))}</belgeIcerigi></fatura>";
        var document = await _soap.CallAsync(_serviceUrl, ServiceNamespace, "faturaOlusturExt", body, cancellationToken).ConfigureAwait(false);

        var result = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "return")
            ?? throw new QnbApiException("QNB e-Arşiv cevabı sonuç içermiyor.");
        string? Field(XElement parent, string name) => parent.Elements().FirstOrDefault(element => element.Name.LocalName == name)?.Value.Trim();

        var code = Field(result, "resultCode");
        if (code != SuccessCode)
            throw new QnbApiException($"QNB e-Arşiv fatura oluşturmadı: {code ?? "kodsuz"} {Field(result, "resultText")}".TrimEnd());

        var extra = result.Descendants().Where(element => element.Name.LocalName == "entry")
            .Select(entry => (Key: Field(entry, "key"), Value: Field(entry, "value")))
            .Where(pair => pair.Key is not null)
            .ToDictionary(pair => pair.Key!, pair => pair.Value, StringComparer.Ordinal);
        if (!extra.TryGetValue("faturaNo", out var number) || string.IsNullOrEmpty(number)
            || !extra.TryGetValue("uuid", out var uuid) || string.IsNullOrEmpty(uuid))
            throw new QnbApiException("QNB e-Arşiv cevabı fatura numarası ve UUID içermiyor.");

        return new QnbEArsivSendResult(number, uuid, extra.GetValueOrDefault("faturaURL"));
    }
}
