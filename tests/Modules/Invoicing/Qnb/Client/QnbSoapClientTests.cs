using System.Net;
using Xunit;

namespace ALKAROS.Invoicing.Qnb.Client.Tests;

/// <summary>
/// The `EF0003` fault body below is copied VERBATIM from a real `curl`
/// call this codebase's own session made to QNB's live test server
/// (`https://erpefaturatest1.qnbesolutions.com.tr/efatura/ws/userService`,
/// 2026-09-18) with placeholder credentials — not a guessed shape. See
/// `QnbSoapClient`'s own doc comment and `V14-QNB-007`'s task file
/// Acceptance evidence for the full transcript.
/// </summary>
public sealed class QnbSoapClientTests
{
    // Verbatim real response body.
    private const string RealEf0003FaultBody = """
        <?xml version='1.0' encoding='UTF-8'?><S:Envelope xmlns:S="http://schemas.xmlsoap.org/soap/envelope/"><S:Body><ns2:Fault xmlns:ns2="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ns3="http://www.w3.org/2003/05/soap-envelope"><faultcode>EF0003</faultcode><faultstring>[EF0003] Oturum açma işlemi başarısız. "Şifremi Unuttum" butonuna basarak işleminize devam edebilir ya da firma yöneticiniz ile iletişime geçebilirsiniz.</faultstring></ns2:Fault></S:Body></S:Envelope>
        """;

    private const string OkEnvelope = """
        <S:Envelope xmlns:S="http://schemas.xmlsoap.org/soap/envelope/">
          <S:Body><ns2:okResponse xmlns:ns2="http://service.csap.cs.com.tr/"/></S:Body>
        </S:Envelope>
        """;

    private static QnbSoapClient MakeClient(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler), "https://userservice.example/ws");

    [Fact]
    public async Task LoginAsyncSendsCredentialsAsPlainXmlElements()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK, OkEnvelope);
        var client = MakeClient(handler);

        await client.LoginAsync("UserID", "Password", "tr");

        var body = handler.RequestBodies[0]!;
        Assert.Contains("<userId>UserID</userId>", body);
        Assert.Contains("<password>Password</password>", body);
        Assert.Contains("wsLogin", body);
    }

    [Fact]
    public async Task LoginAsyncThrowsQnbApiExceptionOnTheRealEf0003Fault()
    {
        // QNB's own live server returns HTTP 500 for this fault (confirmed
        // by the same curl call) — proving the fault must be read BEFORE
        // the HTTP-status check, not after (a real bug this exact scenario
        // caught during the QNB draft, fixed here from the start).
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.InternalServerError, RealEf0003FaultBody);
        var client = MakeClient(handler);

        var ex = await Assert.ThrowsAsync<QnbApiException>(() => client.LoginAsync("wrong", "wrong"));

        Assert.Contains("EF0003", ex.Message);
        Assert.Contains("Oturum açma işlemi başarısız", ex.Message);
    }

    [Fact]
    public async Task LogoutAsyncSendsTheLogoutEnvelope()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK, OkEnvelope);
        var client = MakeClient(handler);

        await client.LogoutAsync();

        Assert.Contains("logout", handler.RequestBodies[0]);
    }

    [Fact]
    public async Task ThrowsOnMalformedXmlInsteadOfCrashingWithAnUnrelatedException()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK, "not xml at all <<<");
        var client = MakeClient(handler);

        await Assert.ThrowsAsync<QnbApiException>(() => client.LoginAsync("u", "p"));
    }

    [Fact]
    public async Task PropagatesCancellationInsteadOfSwallowingIt()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var handler = new FakeHttpMessageHandler();
        var client = MakeClient(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.LoginAsync("u", "p", cancellationToken: cts.Token));
    }

    // The list reply below follows the sample in QNB's published code library; the download reply shape is assumed from the same library.
    private const string IncomingListReply = """
        <S:Envelope xmlns:S="http://schemas.xmlsoap.org/soap/envelope/"><S:Body>
          <ns2:gelenBelgeleriListeleExtResponse xmlns:ns2="http://service.connector.uut.cs.com.tr/">
            <return xsi:type="ns2:belgev6" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <belgeNo>AAA2017000000080</belgeNo><belgeSiraNo>1010</belgeSiraNo><belgeTuru>FATURA</belgeTuru>
              <ettn>22056A27-D326-4FE0-A538-F775F36A28BA</ettn><gonderenVknTckn>1234567802</gonderenVknTckn><saticiUnvan>Test A.Ş.</saticiUnvan>
            </return>
            <return xsi:type="ns2:belgev6" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <belgeNo>AAA2017000000081</belgeNo><belgeSiraNo>1011</belgeSiraNo><belgeTuru>FATURA</belgeTuru>
              <ettn>33056A27-D326-4FE0-A538-F775F36A28BA</ettn><gonderenVknTckn>1234567802</gonderenVknTckn><saticiUnvan>Test A.Ş.</saticiUnvan>
            </return>
          </ns2:gelenBelgeleriListeleExtResponse>
        </S:Body></S:Envelope>
        """;

    private static string DownloadReply(byte[] payload) =>
        $"<S:Envelope xmlns:S=\"http://schemas.xmlsoap.org/soap/envelope/\"><S:Body><ns2:gelenBelgeleriIndirExtResponse xmlns:ns2=\"http://service.connector.uut.cs.com.tr/\"><return>{Convert.ToBase64String(payload)}</return></ns2:gelenBelgeleriIndirExtResponse></S:Body></S:Envelope>";

    private static byte[] Zip(string entryName, string content)
    {
        using var stream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(archive.CreateEntry(entryName).Open(), System.Text.Encoding.UTF8);
            writer.Write(content);
        }

        return stream.ToArray();
    }

    private sealed class UrlRecordingHandler(string reply) : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Urls.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(reply, System.Text.Encoding.UTF8, "text/xml") });
        }
    }

    [Fact]
    public async Task ListIncomingInvoicesAsksTheConnectorServiceAfterTheGivenSequenceAndParsesEveryDocument()
    {
        var handler = new UrlRecordingHandler(IncomingListReply);
        var client = new QnbSoapClient(new HttpClient(handler), "https://qnb.example/efatura/ws/userService");

        var documents = await client.ListIncomingInvoicesAsync("3250566851", 1000);

        Assert.Equal(["https://qnb.example/efatura/ws/connectorService"], handler.Urls);
        Assert.Equal(2, documents.Count);
        Assert.Equal(new QnbIncomingDocument("22056A27-D326-4FE0-A538-F775F36A28BA", "AAA2017000000080", 1010, "1234567802", "Test A.Ş."), documents[0]);
        Assert.Equal(1011, documents[1].SequenceNo);
    }

    [Fact]
    public async Task ListIncomingInvoicesSendsTheDocumentTypeTaxNumberAndResumePoint()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK, IncomingListReply);
        var client = new QnbSoapClient(new HttpClient(handler), "https://qnb.example/efatura/ws/userService");

        await client.ListIncomingInvoicesAsync("3250566851", 1000);

        var body = handler.RequestBodies[0]!;
        Assert.Contains("gelenBelgeleriListeleExt", body);
        Assert.Contains("<belgeTuru>FATURA</belgeTuru>", body);
        Assert.Contains("<sonAlinanBelgeSiraNumarasi>1000</sonAlinanBelgeSiraNumarasi>", body);
        Assert.Contains("<vergiTcKimlikNo>3250566851</vergiTcKimlikNo>", body);
    }

    [Fact]
    public async Task AnEmptyIncomingListIsAnEmptyResult()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK, OkEnvelope);
        var client = new QnbSoapClient(new HttpClient(handler), "https://qnb.example/efatura/ws/userService");

        Assert.Empty(await client.ListIncomingInvoicesAsync("3250566851", 0));
    }

    [Fact]
    public async Task ADocumentWithoutASequenceNumberIsRefusedInsteadOfGuessed()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK,
            "<S:Envelope xmlns:S=\"http://schemas.xmlsoap.org/soap/envelope/\"><S:Body><r><return><ettn>X</ettn></return></r></S:Body></S:Envelope>");
        var client = new QnbSoapClient(new HttpClient(handler), "https://qnb.example/efatura/ws/userService");

        await Assert.ThrowsAsync<QnbApiException>(() => client.ListIncomingInvoicesAsync("3250566851", 0));
    }

    [Fact]
    public async Task DownloadIncomingInvoiceUnzipsTheUblDocument()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK, DownloadReply(Zip("fatura.xml", "<Invoice>İçerik</Invoice>")));
        var client = new QnbSoapClient(new HttpClient(handler), "https://qnb.example/efatura/ws/userService");

        var xml = await client.DownloadIncomingInvoiceXmlAsync("3250566851", "22056A27-D326-4FE0-A538-F775F36A28BA");

        Assert.Equal("<Invoice>İçerik</Invoice>", xml);
        var body = handler.RequestBodies[0]!;
        Assert.Contains("gelenBelgeleriIndirExt", body);
        Assert.Contains("<belgeFormati>UBL</belgeFormati>", body);
        Assert.Contains("<ettn>22056A27-D326-4FE0-A538-F775F36A28BA</ettn>", body);
    }

    [Fact]
    public async Task DownloadIncomingInvoiceAcceptsABareXmlPayloadToo()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK, DownloadReply(System.Text.Encoding.UTF8.GetBytes("<Invoice/>")));
        var client = new QnbSoapClient(new HttpClient(handler), "https://qnb.example/efatura/ws/userService");

        Assert.Equal("<Invoice/>", await client.DownloadIncomingInvoiceXmlAsync("3250566851", "E"));
    }

    [Fact]
    public async Task DownloadIncomingInvoiceRefusesAnEmptyOrCorruptReply()
    {
        var empty = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK, OkEnvelope);
        var corrupt = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK, DownloadReply([]).Replace("<return></return>", "<return>%%%</return>"));

        await Assert.ThrowsAsync<QnbApiException>(() => new QnbSoapClient(new HttpClient(empty), "https://qnb.example/efatura/ws/userService").DownloadIncomingInvoiceXmlAsync("1", "E"));
        await Assert.ThrowsAsync<QnbApiException>(() => new QnbSoapClient(new HttpClient(corrupt), "https://qnb.example/efatura/ws/userService").DownloadIncomingInvoiceXmlAsync("1", "E"));
    }
}
