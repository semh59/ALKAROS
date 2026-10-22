using System.Net;
using Xunit;

namespace ALKAROS.Host.Experience.WebPush.Tests;

public sealed class PushEndpointPolicyTests
{
    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/abc")]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/xyz")]
    [InlineData("https://web.push.apple.com/QXYz")]
    public void RealPushServicesAreAccepted(string endpoint) => Assert.True(PushEndpointPolicy.IsAcceptable(endpoint));

    [Theory]
    [InlineData("http://fcm.googleapis.com/fcm/send/abc")]
    [InlineData("https://127.0.0.1/x")]
    [InlineData("https://10.0.0.5/x")]
    [InlineData("https://[::1]/x")]
    [InlineData("https://localhost/x")]
    [InlineData("https://intranet/x")]
    [InlineData("https://user:secret@push.example.net/x")]
    [InlineData("https://push.example.net:8443/x")]
    [InlineData("ftp://push.example.net/x")]
    [InlineData("/relative/path")]
    [InlineData("not a url")]
    public void AddressesThatCouldReachTheInternalNetworkAreRefused(string endpoint)
        => Assert.False(PushEndpointPolicy.IsAcceptable(endpoint));

    [Fact]
    public void AnEndpointLongerThanTheLimitIsRefused()
    {
        var atLimit = "https://push.example.net/" + new string('a', PushEndpointPolicy.MaximumEndpointLength - 25);
        Assert.Equal(PushEndpointPolicy.MaximumEndpointLength, atLimit.Length);
        Assert.True(PushEndpointPolicy.IsAcceptable(atLimit));
        Assert.False(PushEndpointPolicy.IsAcceptable(atLimit + "a"));
    }

    [Theory]
    [InlineData("8.8.8.8", true)]
    [InlineData("172.15.0.1", true)]
    [InlineData("172.32.0.1", true)]
    [InlineData("100.63.0.1", true)]
    [InlineData("2606:4700::1111", true)]
    [InlineData("127.0.0.1", false)]
    [InlineData("0.0.0.0", false)]
    [InlineData("10.1.2.3", false)]
    [InlineData("172.16.0.1", false)]
    [InlineData("172.31.255.255", false)]
    [InlineData("192.168.1.1", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("100.64.0.1", false)]
    [InlineData("100.127.255.255", false)]
    [InlineData("224.0.0.1", false)]
    [InlineData("::1", false)]
    [InlineData("::", false)]
    [InlineData("fe80::1", false)]
    [InlineData("fc00::1", false)]
    [InlineData("fd12:3456::1", false)]
    [InlineData("ff02::1", false)]
    [InlineData("::ffff:10.0.0.1", false)]
    [InlineData("::ffff:8.8.8.8", true)]
    public void OnlyPublicAddressesAreReachable(string address, bool expected)
        => Assert.Equal(expected, PushEndpointPolicy.IsPublicAddress(IPAddress.Parse(address)));

    [Fact]
    public async Task ANameThatResolvesToTheLocalMachineCannotBeConnectedTo()
    {
        using var client = new HttpClient(PushEndpointPolicy.CreateHandler());

        var failure = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("https://localhost/"));

        Assert.Contains("public address", failure.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ThePushClientNeitherFollowsRedirectsNorKeepsConnectionsForever()
    {
        using var handler = PushEndpointPolicy.CreateHandler();

        Assert.False(handler.AllowAutoRedirect);
        Assert.NotNull(handler.ConnectCallback);
        Assert.True(handler.PooledConnectionLifetime > TimeSpan.Zero);
        Assert.True(handler.PooledConnectionLifetime < TimeSpan.FromHours(1));
    }

    [Fact]
    public void SubscriptionKeysMustBeAPointOnTheCurveAndASixteenByteSecret()
    {
        const string validPoint = "BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4";
        const string validAuth = "BTBZMqHH6r4Tts7J_aSIgg";
        var offCurve = WebPushCrypto.ToBase64Url([.. new byte[] { 4 }, .. new byte[64]]);

        Assert.True(WebPushCrypto.AreValidSubscriptionKeys(validPoint, validAuth));
        Assert.False(WebPushCrypto.AreValidSubscriptionKeys(offCurve, validAuth));
        Assert.False(WebPushCrypto.AreValidSubscriptionKeys("p256dh", validAuth));
        Assert.False(WebPushCrypto.AreValidSubscriptionKeys(validPoint, "auth"));
        Assert.False(WebPushCrypto.AreValidSubscriptionKeys(validPoint, WebPushCrypto.ToBase64Url(new byte[17])));
        Assert.False(WebPushCrypto.AreValidSubscriptionKeys(validPoint, "***"));
    }
}
