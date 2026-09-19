using System.Net;
using Xunit;

namespace ALKAROS.Invoicing.Qnb.Draft.Tests;

/// <summary>
/// Every response body below is either copied verbatim from
/// `evidence/v0/integrations/V0-QNB-001/qnb-esolutions-code-library-raw.txt`
/// (QNB's own saved SOAP examples), or a minimal envelope wrapper built to
/// the same real element names when QNB's own docs did not save a
/// concrete example (noted per-test). This is schema-conformance testing,
/// NOT acceptance evidence of real QNB tenant behavior — see README.md.
/// </summary>
public sealed class QnbSoapClientTests
{
    private const string SuccessEnvelope = """
        <S:Envelope xmlns:S="http://schemas.xmlsoap.org/soap/envelope/">
          <S:Body><ns2:okResponse xmlns:ns2="http://service.csap.cs.com.tr/"/></S:Body>
        </S:Envelope>
        """;

    private static QnbSoapClient MakeClient(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler), "https://userservice.example/ws", "https://connector.example/ws");

    [Fact]
    public async Task LoginAsyncSendsWsLoginWithCredentialsAsPlainElements()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK, SuccessEnvelope);
        var client = MakeClient(handler);

        await client.LoginAsync("UserID", "Password", "tr");

        var body = handler.RequestBodies[0]!;
        Assert.Contains("<userId>UserID</userId>", body);
        Assert.Contains("<password>Password</password>", body);
        Assert.Contains("<lang>tr</lang>", body);
        Assert.Contains("wsLogin", body);
    }

    [Fact]
    public async Task LoginAsyncThrowsQnbApiExceptionOnSoapFault()
    {
        const string faultEnvelope = """
            <S:Envelope xmlns:S="http://schemas.xmlsoap.org/soap/envelope/">
              <S:Body>
                <S:Fault>
                  <faultcode>S:Server</faultcode>
                  <faultstring>Kullanıcı adı veya şifre hatalı.</faultstring>
                </S:Fault>
              </S:Body>
            </S:Envelope>
            """;
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.InternalServerError, faultEnvelope);
        var client = MakeClient(handler);

        var ex = await Assert.ThrowsAsync<QnbApiException>(() => client.LoginAsync("wrong", "wrong"));

        Assert.Contains("Kullanıcı adı veya şifre hatalı", ex.Message);
    }

    [Fact]
    public async Task BelgeGonderExtAsyncComputesTheDocumentedMd5HashOfTheDocumentBytes()
    {
        var handler = new FakeHttpMessageHandler()
            .Enqueue(HttpStatusCode.OK, WrapReturn("belgeGonderExtResponse", "msg-oid-123"));
        var client = MakeClient(handler);
        var documentBytes = "<Invoice>example</Invoice>"u8.ToArray();
#pragma warning disable CA5351 // matches QNB's own documented belgeHash algorithm, see QnbSoapClient.ComputeMd5Hex
        var expectedHash = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(documentBytes));
#pragma warning restore CA5351

        await client.BelgeGonderExtAsync(new BelgeGonderRequest(
            "3250566851", BelgeGonderRequest.BelgeTuruFaturaUbl, "KKT2022000000005", documentBytes));

        var body = handler.RequestBodies[0]!;
        Assert.Contains($"<belgeHash>{expectedHash}</belgeHash>", body);
        Assert.Contains("<vergiTcKimlikNo>3250566851</vergiTcKimlikNo>", body);
        Assert.Contains(Convert.ToBase64String(documentBytes), body);
    }

    [Fact]
    public async Task BelgeGonderExtAsyncReturnsTheProviderMessageOid()
    {
        var handler = new FakeHttpMessageHandler()
            .Enqueue(HttpStatusCode.OK, WrapReturn("belgeGonderExtResponse", "msg-oid-abc"));
        var client = MakeClient(handler);

        var result = await client.BelgeGonderExtAsync(new BelgeGonderRequest(
            "3250566851", BelgeGonderRequest.BelgeTuruFaturaUbl, "KKT2022000000005", [1, 2, 3]));

        Assert.Equal("msg-oid-abc", result);
    }

    [Fact]
    public async Task GidenBelgeDurumSorgulaExtAsyncParsesTheDocumentedRealExampleResponse()
    {
        // Verbatim from qnb-esolutions-code-library-raw.txt's "efatura_status" section.
        const string realExampleResponse = """
            <S:Envelope xmlns:S="http://schemas.xmlsoap.org/soap/envelope/"><S:Body>
            <ns2:gidenBelgeDurumSorgulaExtResponse
                xmlns:ns2="http://service.connector.uut.cs.com.tr/">
                <return xsi:type="ns2:gidenBelgeDurumv5"
                    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
                    <alimTarihi>20220617214636473</alimTarihi>
                    <belgeNo>SAL2022090578078</belgeNo>
                    <durum>3</durum>
                    <ettn>25ADF33C-C065-4AB9-A0F2-30A72419C607</ettn>
                    <gonderimCevabiDetayi/>
                    <gonderimCevabiKodu>0</gonderimCevabiKodu>
                    <gonderimDurumu>-1</gonderimDurumu>
                    <olusturulmaTarihi>20220617214644893</olusturulmaTarihi>
                    <yanitDetayi/>
                    <yanitDurumu>0</yanitDurumu>
                    <ulastiMi>false</ulastiMi>
                    <yenidenGonderilebilirMi>false</yenidenGonderilebilirMi>
                    <yerelBelgeOid>3ul4dvqhmd1095</yerelBelgeOid>
                </return>
            </ns2:gidenBelgeDurumSorgulaExtResponse></S:Body></S:Envelope>
            """;
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK, realExampleResponse);
        var client = MakeClient(handler);

        var status = await client.GidenBelgeDurumSorgulaExtAsync(
            new QnbDocumentStatusQuery("3250566851", "3ul4dvqhmd1095", QnbDocumentStatusQuery.BelgeNoTipiOid, "FATURA"));

        Assert.Equal("SAL2022090578078", status.BelgeNo);
        Assert.Equal(3, status.DurumKodu);
        Assert.Equal("25ADF33C-C065-4AB9-A0F2-30A72419C607", status.Ettn);
        Assert.Equal(-1, status.GonderimDurumu);
        Assert.Equal(0, status.YanitDurumu);
        Assert.False(status.UlastiMi);
        Assert.False(status.YenidenGonderilebilirMi);
        Assert.Equal("3ul4dvqhmd1095", status.YerelBelgeOid);
    }

    [Fact]
    public async Task KayitliKullaniciListeleExtendedAsyncDecodesTheBase64ZipReturn()
    {
        var zipBytes = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // real ZIP magic number
        var handler = new FakeHttpMessageHandler()
            .Enqueue(HttpStatusCode.OK, WrapReturn("kayitliKullaniciListeleExtendedResponse", Convert.ToBase64String(zipBytes)));
        var client = MakeClient(handler);

        var result = await client.KayitliKullaniciListeleExtendedAsync("EFATURA", gecmisEklensin: 1);

        Assert.Equal(zipBytes, result);
        Assert.Contains("<urun>EFATURA</urun>", handler.RequestBodies[0]);
    }

    [Fact]
    public async Task PostAsyncThrowsOnMalformedXmlInsteadOfCrashingWithAnUnrelatedException()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK, "not xml at all <<<");
        var client = MakeClient(handler);

        await Assert.ThrowsAsync<QnbApiException>(() => client.LoginAsync("u", "p"));
    }

    [Fact]
    public async Task LoginAsyncPropagatesCancellationInsteadOfSwallowingIt()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var handler = new FakeHttpMessageHandler();
        var client = MakeClient(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.LoginAsync("u", "p", cancellationToken: cts.Token));
    }

    private static string WrapReturn(string responseElementName, string returnValue) => $"""
        <S:Envelope xmlns:S="http://schemas.xmlsoap.org/soap/envelope/">
          <S:Body>
            <ns2:{responseElementName} xmlns:ns2="http://service.connector.uut.cs.com.tr/">
              <return>{returnValue}</return>
            </ns2:{responseElementName}>
          </S:Body>
        </S:Envelope>
        """;
}
