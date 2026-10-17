using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using ALKAROS.Invoicing.Qnb.EArsiv;
using Xunit;

namespace ALKAROS.Invoicing.Qnb.Client.Tests;

public sealed class QnbEArsivClientTests
{
    private static readonly Guid Uuid = Guid.Parse("1303d6ed-db9e-4465-b48e-9ec6791a3bcb");

    private const string SuccessReply = """
        <S:Envelope xmlns:S="http://schemas.xmlsoap.org/soap/envelope/"><S:Body>
        <ns2:faturaOlusturExtResponse xmlns:ns2="http://service.earsiv.uut.cs.com.tr/">
        <output><belgeFormati>HTML</belgeFormati><belgeIcerigi>H4sIAAAA</belgeIcerigi></output>
        <return><resultCode>AE00000</resultCode>
        <resultExtra>
        <entry><key>islemID</key><value>1303d6ed-db9e-4465-b48e-9ec6791a3bcb</value></entry>
        <entry><key>faturaURL</key><value>http://earsivtest.qnbesolutions.com.tr/earsiv/goruntule.jsp?vkn=1&amp;uuid=2</value></entry>
        <entry><key>uuid</key><value>1303d6ed-db9e-4465-b48e-9ec6791a3bcb</value></entry>
        <entry><key>faturaNo</key><value>TTT2026000000001</value></entry>
        </resultExtra>
        <resultText>İşlem başarılı.</resultText></return>
        </ns2:faturaOlusturExtResponse></S:Body></S:Envelope>
        """;

    private const string LoginReply = """
        <S:Envelope xmlns:S="http://schemas.xmlsoap.org/soap/envelope/"><S:Body><ns2:wsLoginResponse xmlns:ns2="http://service.csap.cs.com.tr/"/></S:Body></S:Envelope>
        """;

    private static QnbEArsivInvoice Invoice(string erpCode = "ERP1") =>
        new("5544332211", "S1", "K1", erpCode, Uuid, "<Invoice>Çorba &amp; ayran</Invoice>");

    private static (QnbEArsivClient Client, FakeHttpMessageHandler Handler) Make(params string[] replies)
    {
        var handler = new FakeHttpMessageHandler();
        foreach (var reply in replies)
            handler.Enqueue(HttpStatusCode.OK, reply);
        return (new QnbEArsivClient(new HttpClient(handler), "https://earsiv.example/ws/userService", "https://earsiv.example/ws/EarsivWebService"), handler);
    }

    [Fact]
    public async Task TheRequestCarriesTheJsonInputAndTheBase64UblInTheEArsivNamespace()
    {
        var (client, handler) = Make(SuccessReply);

        await client.SendInvoiceAsync(Invoice());

        var request = XDocument.Parse(handler.RequestBodies[0]!);
        var call = request.Descendants().Single(element => element.Name.LocalName == "faturaOlusturExt");
        Assert.Equal("http://service.earsiv.uut.cs.com.tr/", call.Name.NamespaceName);
        var input = JsonDocument.Parse(call.Element("input")!.Value).RootElement;
        Assert.Equal("1303d6ed-db9e-4465-b48e-9ec6791a3bcb", input.GetProperty("islemId").GetString());
        Assert.Equal("5544332211", input.GetProperty("vkn").GetString());
        Assert.Equal("S1", input.GetProperty("sube").GetString());
        Assert.Equal("K1", input.GetProperty("kasa").GetString());
        Assert.Equal("ERP1", input.GetProperty("erpKodu").GetString());
        Assert.Equal("9", input.GetProperty("donenBelgeFormati").GetString());
        var fatura = call.Element("fatura")!;
        Assert.Equal("UBL", fatura.Element("belgeFormati")!.Value);
        Assert.Equal("<Invoice>Çorba &amp; ayran</Invoice>", Encoding.UTF8.GetString(Convert.FromBase64String(fatura.Element("belgeIcerigi")!.Value)));
    }

    [Fact]
    public async Task ASuccessReturnsTheInvoiceNumberUuidAndViewAddress()
    {
        var (client, _) = Make(SuccessReply);

        var result = await client.SendInvoiceAsync(Invoice());

        Assert.Equal(("TTT2026000000001", "1303d6ed-db9e-4465-b48e-9ec6791a3bcb", "http://earsivtest.qnbesolutions.com.tr/earsiv/goruntule.jsp?vkn=1&uuid=2"),
            (result.InvoiceNumber, result.Uuid, result.ViewUrl));
    }

    [Fact]
    public async Task AResultCodeOtherThanSuccessIsAnErrorWithTheCodeAndText()
    {
        var (client, _) = Make(SuccessReply.Replace("AE00000", "AE00123", StringComparison.Ordinal).Replace("İşlem başarılı.", "ERP kodu geçersiz.", StringComparison.Ordinal));

        var failure = await Assert.ThrowsAsync<QnbApiException>(() => client.SendInvoiceAsync(Invoice()));

        Assert.Contains("AE00123", failure.Message);
        Assert.Contains("ERP kodu geçersiz.", failure.Message);
    }

    [Theory]
    [InlineData("faturaNo")]
    [InlineData("uuid")]
    public async Task ASuccessWithoutTheInvoiceNumberOrUuidIsRefused(string missingKey)
    {
        var (client, _) = Make(SuccessReply.Replace($"<key>{missingKey}</key>", "<key>other</key>", StringComparison.Ordinal));

        await Assert.ThrowsAsync<QnbApiException>(() => client.SendInvoiceAsync(Invoice()));
    }

    [Theory]
    [InlineData("TTT2026000000001")]
    [InlineData("1303d6ed-db9e-4465-b48e-9ec6791a3bcb")]
    public async Task ASuccessWithABlankInvoiceNumberOrUuidIsRefused(string value)
    {
        var (client, _) = Make(SuccessReply.Replace($"<value>{value}</value>", "<value> </value>", StringComparison.Ordinal));

        await Assert.ThrowsAsync<QnbApiException>(() => client.SendInvoiceAsync(Invoice()));
    }

    [Fact]
    public async Task ASuccessReplyWithoutAResultIsRefused()
    {
        var (client, _) = Make("<S:Envelope xmlns:S=\"http://schemas.xmlsoap.org/soap/envelope/\"><S:Body/></S:Envelope>");

        await Assert.ThrowsAsync<QnbApiException>(() => client.SendInvoiceAsync(Invoice()));
    }

    [Fact]
    public async Task TheErpCodeIsXmlEscapedAndLoginUsesTheSameSession()
    {
        var (client, handler) = Make(LoginReply, SuccessReply);

        await client.LoginAsync("user", "secret");
        await client.SendInvoiceAsync(Invoice("A<B&C"));

        Assert.Contains("wsLogin", handler.RequestBodies[0]);
        var input = XDocument.Parse(handler.RequestBodies[1]!).Descendants().Single(element => element.Name.LocalName == "input").Value;
        Assert.Equal("A<B&C", JsonDocument.Parse(input).RootElement.GetProperty("erpKodu").GetString());
    }

    [Theory]
    [InlineData("", "DFLT", "DFLT", "ERP1")]
    [InlineData("5544332211", "", "DFLT", "ERP1")]
    [InlineData("5544332211", "DFLT", "", "ERP1")]
    [InlineData("5544332211", "DFLT", "DFLT", "")]
    public async Task AnIncompleteInvoiceIsRefusedBeforeAnyCall(string taxNumber, string branch, string cashDesk, string erpCode)
    {
        var (client, handler) = Make();

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            client.SendInvoiceAsync(new QnbEArsivInvoice(taxNumber, branch, cashDesk, erpCode, Uuid, "<Invoice/>")));

        Assert.Empty(handler.RequestBodies);
    }
}
