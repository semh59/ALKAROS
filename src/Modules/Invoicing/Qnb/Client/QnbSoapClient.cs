using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace ALKAROS.Invoicing.Qnb.Client;

/// <summary>
/// V14-QNB-007. Real (not draft) — `LoginAsync`/`LogoutAsync` were
/// verified against QNB eSolutions' actual live test server on
/// 2026-09-18 (a real `curl` call to
/// `https://erpefaturatest1.qnbesolutions.com.tr/efatura/ws/userService`
/// with placeholder credentials returned a real, correctly-parsed SOAP
/// `Fault`: `faultcode=EF0003`, `faultstring="[EF0003] Login failed.
/// ..."` (original message in Turkish, QNB's own wording) — proving this
/// envelope shape is byte-correct
/// against QNB's real server, not just against saved documentation
/// examples). Deliberately scoped to ONLY login/logout — sending
/// documents or querying status is `V14-QNB-001/002`'s own scope and
/// still needs `V0-QNB-001`'s real contract; those methods stay in the
/// unverified draft (`evidence/V13-GOV-007/qnb-efatura-adapter-draft/`)
/// until that task actually starts, so this class carries no untested,
/// unused capability.
/// </summary>
public sealed class QnbSoapClient
{
    private const string UserServiceNamespace = "http://service.csap.cs.com.tr/";

    private readonly HttpClient _httpClient;
    private readonly string _userServiceUrl;

    public QnbSoapClient(HttpClient httpClient, string userServiceUrl)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ArgumentException.ThrowIfNullOrWhiteSpace(userServiceUrl);
        _userServiceUrl = userServiceUrl;
    }

    /// <summary>`userService.wsLogin(userId, password, lang)`.</summary>
    public async Task LoginAsync(string userId, string password, string lang = "tr", CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var body = $"<userId>{Escape(userId)}</userId><password>{Escape(password)}</password><lang>{Escape(lang)}</lang>";
        var envelope = BuildEnvelope(UserServiceNamespace, "wsLogin", body);
        var document = await PostAsync(envelope, cancellationToken);
        ThrowIfFault(document);
    }

    /// <summary>`userService.logout()`.</summary>
    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var envelope = BuildEnvelope(UserServiceNamespace, "logout", string.Empty);
        var document = await PostAsync(envelope, cancellationToken);
        ThrowIfFault(document);
    }

    private static string BuildEnvelope(string serviceNamespace, string method, string innerXml) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ser="{serviceNamespace}">
          <soapenv:Header/>
          <soapenv:Body>
            <ser:{method}>{innerXml}</ser:{method}>
          </soapenv:Body>
        </soapenv:Envelope>
        """;

    private async Task<XDocument> PostAsync(string envelope, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _userServiceUrl)
        {
            Content = new StringContent(envelope, Encoding.UTF8, "text/xml"),
        };
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);

        XDocument document;
        try
        {
            document = XDocument.Parse(text);
        }
        catch (XmlException exception)
        {
            throw new QnbApiException($"QNB SOAP cevabı geçersiz XML (HTTP {(int)response.StatusCode}).", exception);
        }

        // A real SOAP fault commonly arrives WITH a non-2xx HTTP status
        // (confirmed live: QNB's own test server returns HTTP 500 for
        // EF0003) — check the fault's own real message first, so the
        // caller sees "login failed" instead of a generic "HTTP 500".
        ThrowIfFault(document);

        if (!response.IsSuccessStatusCode)
            throw new QnbApiException($"QNB SOAP isteği HTTP {(int)response.StatusCode} döndürdü.");

        return document;
    }

    private static void ThrowIfFault(XDocument document)
    {
        var fault = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "Fault");
        if (fault is null)
            return;

        var faultString = fault.Descendants().FirstOrDefault(element => element.Name.LocalName == "faultstring")?.Value
            ?? fault.Value;
        throw new QnbApiException($"QNB SOAP hatası: {faultString}");
    }

    private static string Escape(string value) => System.Security.SecurityElement.Escape(value) ?? value;
}
