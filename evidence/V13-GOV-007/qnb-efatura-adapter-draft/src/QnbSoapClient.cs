using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace ALKAROS.Invoicing.Qnb.Draft;

/// <summary>
/// V13-GOV-007 DRAFT (unverified — no real QNB test tenant was ever
/// reached; see README.md). Endpoint URLs, SOAP envelope shapes, and
/// every field name come from QNB eSolutions' own published API
/// documentation (`evidence/v0/integrations/V0-QNB-001/
/// qnb-esolutions-code-library-raw.txt`). Style mirrors
/// `ALKAROS.Payments.Token.Draft.TokenBasketClient` (raw `HttpClient` +
/// manual serialize/deserialize) — this codebase has no WCF/
/// `System.ServiceModel` dependency anywhere, and QNB's own C#/Java
/// samples themselves construct SOAP envelopes by hand rather than using
/// a generated WSDL proxy, so this draft does the same rather than
/// introducing a new dependency style.
///
/// Auth is session-cookie based, NOT Token's REST Bearer model: call
/// <see cref="LoginAsync"/> once, reuse this client for every subsequent
/// call (the caller's `HttpClient` must be built with an
/// `HttpClientHandler` whose `CookieContainer`/`UseCookies=true` are set,
/// exactly like QNB's own `SoapLoginClient` sample — this class does not
/// construct its own `HttpClient`, so it never silently drops the
/// session), then call <see cref="LogoutAsync"/> when done.
/// </summary>
public sealed class QnbSoapClient
{
    private const string UserServiceNamespace = "http://service.csap.cs.com.tr/";
    private const string ConnectorServiceNamespace = "http://service.connector.uut.cs.com.tr/";

    private readonly HttpClient _httpClient;
    private readonly string _userServiceUrl;
    private readonly string _connectorServiceUrl;

    public QnbSoapClient(HttpClient httpClient, string userServiceUrl, string connectorServiceUrl)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ArgumentException.ThrowIfNullOrWhiteSpace(userServiceUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectorServiceUrl);
        _userServiceUrl = userServiceUrl;
        _connectorServiceUrl = connectorServiceUrl;
    }

    /// <summary>`userService.wsLogin(userId, password, lang)` — the response session cookie is captured by the caller's own CookieContainer, not read here.</summary>
    public async Task LoginAsync(string userId, string password, string lang = "tr", CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var body = $"<userId>{Escape(userId)}</userId><password>{Escape(password)}</password><lang>{Escape(lang)}</lang>";
        var envelope = BuildEnvelope(UserServiceNamespace, "wsLogin", body);
        await PostAsync(_userServiceUrl, envelope, cancellationToken);
    }

    /// <summary>`userService.logout()`.</summary>
    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var envelope = BuildEnvelope(UserServiceNamespace, "logout", string.Empty);
        await PostAsync(_userServiceUrl, envelope, cancellationToken);
    }

    /// <summary>
    /// `connectorService.belgeGonderExt(parametreler)` — computes the MD5
    /// hash of <paramref name="request"/>'s document bytes client-side
    /// (the exact pattern QNB's own `GetMD5Hash` sample uses) and returns
    /// the provider's `belgeGonderMsgOid`.
    /// </summary>
    public async Task<string> BelgeGonderExtAsync(BelgeGonderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.VergiTcKimlikNo);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.BelgeNo);
        ArgumentNullException.ThrowIfNull(request.Veri);

        var hash = ComputeMd5Hex(request.Veri);
        var erpKoduElement = request.ErpKodu is null ? "<erpKodu/>" : $"<erpKodu>{Escape(request.ErpKodu)}</erpKodu>";
        var body = $"""
            <parametreler>
                <vergiTcKimlikNo>{Escape(request.VergiTcKimlikNo)}</vergiTcKimlikNo>
                <belgeTuru>{Escape(request.BelgeTuru)}</belgeTuru>
                <belgeNo>{Escape(request.BelgeNo)}</belgeNo>
                <veri>{Convert.ToBase64String(request.Veri)}</veri>
                <belgeHash>{hash}</belgeHash>
                <mimeType>{Escape(request.MimeType)}</mimeType>
                <belgeVersiyon>{Escape(request.BelgeVersiyon)}</belgeVersiyon>
                {erpKoduElement}
            </parametreler>
            """;
        var envelope = BuildEnvelope(ConnectorServiceNamespace, "belgeGonderExt", body);
        var document = await PostAsync(_connectorServiceUrl, envelope, cancellationToken);

        return ExtractReturnValue(document, "belgeGonderExt")
            ?? throw new QnbApiException("QNB belgeGonderExt cevabında 'return' (belgeGonderMsgOid) yok.");
    }

    /// <summary>`connectorService.gidenBelgeDurumSorgulaExt(vergiTcKimlikNo, parametreler)`.</summary>
    public async Task<QnbDocumentStatus> GidenBelgeDurumSorgulaExtAsync(QnbDocumentStatusQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.VergiTcKimlikNo);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.BelgeNo);

        var body = $"""
            <vergiTcKimlikNo>{Escape(query.VergiTcKimlikNo)}</vergiTcKimlikNo>
            <parametreler>
                <belgeNo>{Escape(query.BelgeNo)}</belgeNo>
                <belgeNoTipi>{Escape(query.BelgeNoTipi)}</belgeNoTipi>
                <belgeTuru>{Escape(query.BelgeTuru)}</belgeTuru>
                <donusTipiVersiyon>{Escape(query.DonusTipiVersiyon)}</donusTipiVersiyon>
            </parametreler>
            """;
        var envelope = BuildEnvelope(ConnectorServiceNamespace, "gidenBelgeDurumSorgulaExt", body);
        var document = await PostAsync(_connectorServiceUrl, envelope, cancellationToken);

        return ParseDocumentStatus(document);
    }

    /// <summary>
    /// `connectorService.kayitliKullaniciListeleExtended(urun, gecmisEklensin)`
    /// — returns the raw ZIP bytes of the registered-taxpayer list (the
    /// documented response shape; QNB's own sample notes "günde 1 defa
    /// çekmek yeterlidir" — no hard rate limit number is published).
    /// </summary>
    public async Task<byte[]> KayitliKullaniciListeleExtendedAsync(string urun, int gecmisEklensin, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(urun);

        var body = $"<urun>{Escape(urun)}</urun><gecmisEklensin>{gecmisEklensin}</gecmisEklensin>";
        var envelope = BuildEnvelope(ConnectorServiceNamespace, "kayitliKullaniciListeleExtended", body);
        var document = await PostAsync(_connectorServiceUrl, envelope, cancellationToken);

        var base64 = ExtractReturnValue(document, "kayitliKullaniciListeleExtended")
            ?? throw new QnbApiException("QNB kayitliKullaniciListeleExtended cevabında 'return' yok.");
        return Convert.FromBase64String(base64);
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

    private async Task<XDocument> PostAsync(string url, string envelope, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
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

        // A real SOAP fault is very commonly delivered WITH a non-2xx HTTP
        // status (many SOAP stacks return 500 for `<S:Fault>`) — check for
        // the fault's own real message first, so the caller sees "wrong
        // password" instead of a generic "HTTP 500" that discards it.
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

    private static string? ExtractReturnValue(XDocument document, string methodName) =>
        document.Descendants().FirstOrDefault(element => element.Name.LocalName == "return")?.Value;

    private static QnbDocumentStatus ParseDocumentStatus(XDocument document)
    {
        var returnElement = document.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "return")
            ?? throw new QnbApiException("QNB gidenBelgeDurumSorgulaExt cevabında 'return' yok.");

        string? Field(string name) => returnElement.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value;
        int? IntField(string name) => int.TryParse(Field(name), out var value) ? value : null;
        bool BoolField(string name) => bool.TryParse(Field(name), out var value) && value;

        return new QnbDocumentStatus(
            AlimTarihi: Field("alimTarihi"),
            BelgeNo: Field("belgeNo") ?? throw new QnbApiException("QNB response'unda belgeNo yok."),
            DurumKodu: IntField("durum") ?? throw new QnbApiException("QNB response'unda durum yok."),
            Ettn: Field("ettn"),
            GonderimCevabiDetayi: Field("gonderimCevabiDetayi"),
            GonderimCevabiKodu: IntField("gonderimCevabiKodu"),
            GonderimDurumu: IntField("gonderimDurumu") ?? throw new QnbApiException("QNB response'unda gonderimDurumu yok."),
            OlusturulmaTarihi: Field("olusturulmaTarihi"),
            YanitDetayi: Field("yanitDetayi"),
            YanitDurumu: IntField("yanitDurumu"),
            UlastiMi: BoolField("ulastiMi"),
            YenidenGonderilebilirMi: BoolField("yenidenGonderilebilirMi"),
            YerelBelgeOid: Field("yerelBelgeOid"));
    }

    // MD5 is not used for anything security-sensitive here — it is the
    // exact document-integrity checksum QNB's own real `belgeGonderExt`
    // contract requires in the `belgeHash` field (their own C# sample
    // computes it the same way). Not our choice to make; a provider
    // protocol constraint.
#pragma warning disable CA5351
    private static string ComputeMd5Hex(byte[] data) => Convert.ToHexString(MD5.HashData(data));
#pragma warning restore CA5351

    private static string Escape(string value) => System.Security.SecurityElement.Escape(value) ?? value;
}
