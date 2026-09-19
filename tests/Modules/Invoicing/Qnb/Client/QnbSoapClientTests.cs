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
}
