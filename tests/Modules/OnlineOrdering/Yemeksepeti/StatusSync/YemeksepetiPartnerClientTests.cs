using System.Net;
using System.Text;
using System.Text.Json;
using ALKAROS.IntegrationContracts;
using ALKAROS.Secrets;
using FluentAssertions;
using Xunit;

namespace ALKAROS.OnlineOrdering.Yemeksepeti.StatusSync.Tests;

/// <summary>
/// These tests pin our draft client to what the public Partner API v2.0.2 page describes. They do
/// not — and cannot — prove the real provider accepts it; nothing has been run against the sandbox.
/// </summary>
public sealed class YemeksepetiPartnerClientTests
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Url, string? Authorization, string ContentType, string Body)> Requests { get; } = [];
        public HttpStatusCode UpdateStatus { get; set; } = HttpStatusCode.OK;
        public int ExpiresIn { get; set; } = 7200;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method, request.RequestUri!.ToString(), request.Headers.Authorization?.ToString(),
                request.Content?.Headers.ContentType?.MediaType ?? string.Empty, body));
            if (request.RequestUri!.AbsolutePath.EndsWith("/v2/oauth/token", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        $$"""{"access_token":"tok-{{Requests.Count}}","token_type":"Bearer","expires_in":{{ExpiresIn}}}""",
                        Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(UpdateStatus);
        }
    }

    private sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static InMemorySecretProvider Configured()
    {
        var secrets = new InMemorySecretProvider();
        secrets.Set(YemeksepetiPartnerHttpClient.BaseUrl, "https://sandbox.partner.deliveryhero.io");
        secrets.Set(YemeksepetiPartnerHttpClient.ChainId, "chain-42");
        secrets.Set(YemeksepetiPartnerHttpClient.ClientId, "client-a");
        secrets.Set(YemeksepetiPartnerHttpClient.ClientSecret, "s3cr3t&=");
        return secrets;
    }

    private static YemeksepetiStatusUpdateRequested Update(YemeksepetiOutboundStatus status, YemeksepetiCancellationReason? reason = null) =>
        new(Guid.NewGuid(), "9f0c2d4e-5b6a-4c3d-8e7f-001122334455", status, reason,
            [new YemeksepetiOrderLineReference("ys-pide", 2m)], DateTimeOffset.UtcNow);

    [Fact]
    public async Task AStatusUpdateUsesTheDocumentedTokenAndOrderUpdateCalls()
    {
        var handler = new RecordingHandler();
        using var client = new YemeksepetiPartnerHttpClient(new HttpClient(handler), Configured(), new ManualTime());

        await client.UpdateOrderStatusAsync(Update(YemeksepetiOutboundStatus.ReadyForPickup));

        handler.Requests.Should().HaveCount(2);
        var token = handler.Requests[0];
        token.Method.Should().Be(HttpMethod.Post);
        token.Url.Should().Be("https://sandbox.partner.deliveryhero.io/v2/oauth/token");
        token.ContentType.Should().Be("application/x-www-form-urlencoded");
        token.Body.Should().Be("grant_type=client_credentials&client_id=client-a&client_secret=s3cr3t%26%3D");

        var update = handler.Requests[1];
        update.Method.Should().Be(HttpMethod.Put);
        update.Url.Should().Be("https://sandbox.partner.deliveryhero.io/v2/chains/chain-42/orders/9f0c2d4e-5b6a-4c3d-8e7f-001122334455");
        update.Authorization.Should().Be("Bearer tok-1");
        using var body = JsonDocument.Parse(update.Body);
        body.RootElement.GetProperty("order_id").GetString().Should().Be("9f0c2d4e-5b6a-4c3d-8e7f-001122334455");
        body.RootElement.GetProperty("status").GetString().Should().Be("READY_FOR_PICKUP");
        var item = body.RootElement.GetProperty("items")[0];
        item.GetProperty("sku").GetString().Should().Be("ys-pide");
        item.GetProperty("status").GetString().Should().Be("IN_CART");
        item.GetProperty("pricing").GetProperty("quantity").GetDecimal().Should().Be(2m);
        body.RootElement.TryGetProperty("cancellation", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(YemeksepetiCancellationReason.Closed, "CLOSED")]
    [InlineData(YemeksepetiCancellationReason.ItemUnavailable, "ITEM_UNAVAILABLE")]
    [InlineData(YemeksepetiCancellationReason.TooBusy, "TOO_BUSY")]
    public async Task ACancellationCarriesItsDocumentedReason(YemeksepetiCancellationReason reason, string expected)
    {
        var handler = new RecordingHandler();
        using var client = new YemeksepetiPartnerHttpClient(new HttpClient(handler), Configured(), new ManualTime());

        await client.UpdateOrderStatusAsync(Update(YemeksepetiOutboundStatus.Cancelled, reason));

        using var body = JsonDocument.Parse(handler.Requests[1].Body);
        body.RootElement.GetProperty("status").GetString().Should().Be("CANCELLED");
        body.RootElement.GetProperty("cancellation").GetProperty("reason").GetString().Should().Be(expected);
    }

    [Fact]
    public async Task TheTokenIsReusedUntilShortlyBeforeItExpires()
    {
        var handler = new RecordingHandler { ExpiresIn = 600 };
        var time = new ManualTime();
        using var client = new YemeksepetiPartnerHttpClient(new HttpClient(handler), Configured(), time);

        await client.UpdateOrderStatusAsync(Update(YemeksepetiOutboundStatus.Dispatched));
        time.Now += TimeSpan.FromSeconds(530);
        await client.UpdateOrderStatusAsync(Update(YemeksepetiOutboundStatus.Dispatched));
        time.Now += TimeSpan.FromSeconds(20);
        await client.UpdateOrderStatusAsync(Update(YemeksepetiOutboundStatus.Dispatched));

        handler.Requests.Count(r => r.Url.EndsWith("/v2/oauth/token", StringComparison.Ordinal)).Should().Be(2);
    }

    [Fact]
    public async Task AProviderErrorFailsTheDeliverySoTheOutboxRetriesIt()
    {
        var handler = new RecordingHandler { UpdateStatus = HttpStatusCode.ServiceUnavailable };
        using var client = new YemeksepetiPartnerHttpClient(new HttpClient(handler), Configured(), new ManualTime());

        var act = () => client.UpdateOrderStatusAsync(Update(YemeksepetiOutboundStatus.Dispatched));

        await act.Should().ThrowAsync<YemeksepetiPartnerApiException>().WithMessage("*HTTP 503*");
    }

    [Fact]
    public async Task WithoutCredentialsNothingIsSent()
    {
        var handler = new RecordingHandler();
        using var client = new YemeksepetiPartnerHttpClient(new HttpClient(handler), new InMemorySecretProvider(), new ManualTime());

        var act = () => client.UpdateOrderStatusAsync(Update(YemeksepetiOutboundStatus.Dispatched));

        await act.Should().ThrowAsync<SecretNotFoundException>();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task TheConsumerHandlesOnlyStatusUpdatesAndDeliversThemUnchanged()
    {
        var handler = new RecordingHandler();
        using var client = new YemeksepetiPartnerHttpClient(new HttpClient(handler), Configured(), new ManualTime());
        var consumer = new YemeksepetiStatusUpdateConsumer(client);
        var update = Update(YemeksepetiOutboundStatus.Cancelled, YemeksepetiCancellationReason.ItemUnavailable);

        consumer.CanHandle("qr-ordering.order-submitted.v1").Should().BeFalse();
        consumer.CanHandle(YemeksepetiStatusSync.StatusUpdateRequestedEventType).Should().BeTrue();
        await consumer.HandleAsync(YemeksepetiStatusSync.StatusUpdateRequestedEventType, IntegrationEventSerializer.Serialize(update), CancellationToken.None);

        using var body = JsonDocument.Parse(handler.Requests.Single(r => r.Method == HttpMethod.Put).Body);
        body.RootElement.GetProperty("cancellation").GetProperty("reason").GetString().Should().Be("ITEM_UNAVAILABLE");
    }

    [Theory]
    [InlineData("LOGISTICS_DELIVERY", YemeksepetiOutboundStatus.ReadyForPickup)]
    [InlineData("VENDOR_DELIVERY", YemeksepetiOutboundStatus.Dispatched)]
    [InlineData("PICKUP", null)]
    [InlineData(null, null)]
    public void TheHandoverStatusFollowsTheDeliveryKind(string? transport, YemeksepetiOutboundStatus? expected) =>
        YemeksepetiStatusSync.HandoverStatusFor(transport).Should().Be(expected);

    [Fact]
    public void ItemReferencesAreReadLenientlyFromAnUnusablePayload()
    {
        const string payload = """
            {"items":[{"sku":"a","pricing":{"quantity":3}},{"sku":"  b  "},{"name":"no sku"},{"sku":"c","pricing":{"quantity":"x"}}]}
            """;

        YemeksepetiStatusSync.ReadItemReferences(payload).Should().Equal(
            new YemeksepetiOrderLineReference("a", 3m),
            new YemeksepetiOrderLineReference("b", 1m),
            new YemeksepetiOrderLineReference("c", 1m));
        YemeksepetiStatusSync.ReadItemReferences("not json").Should().BeEmpty();
    }
}
