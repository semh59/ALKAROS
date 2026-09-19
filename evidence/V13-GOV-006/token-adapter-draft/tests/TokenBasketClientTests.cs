using System.Net;
using Xunit;

namespace ALKAROS.Payments.Token.Draft.Tests;

/// <summary>
/// Every JSON body below is copied verbatim (only re-indented) from the real
/// "TokenX Documentation" Postman collection's saved example responses
/// (`evidence/v0/integrations/V0-HUG-001/tokenx-documentation.postman_collection.json`),
/// to prove <see cref="TokenBasketClient"/> parses what Token's own
/// documentation says it returns — not a guessed shape.
/// </summary>
public sealed class TokenBasketClientTests
{
    private const string AuthSuccessBody = """
        {
            "status": 201,
            "description": "User authenticated successfully.",
            "result": { "accessToken": "example.jwt.token" }
        }
        """;

    [Fact]
    public async Task AuthenticateAsyncParsesTheDocumentedSuccessShapeDespiteStatus201()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK, AuthSuccessBody);
        var client = new TokenBasketClient(new HttpClient(handler), "https://auth.example", "https://basket.example", "cid", "secret");

        var token = await client.AuthenticateAsync();

        Assert.Equal("example.jwt.token", token);
        var sentAuthHeader = handler.Requests[0].Headers.Authorization;
        Assert.Equal("Basic", sentAuthHeader!.Scheme);
    }

    [Fact]
    public async Task AuthenticateAsyncCachesTheTokenAndDoesNotCallAuthTwice()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK, AuthSuccessBody);
        var client = new TokenBasketClient(new HttpClient(handler), "https://auth.example", "https://basket.example", "cid", "secret");

        await client.AuthenticateAsync();
        var second = await client.AuthenticateAsync();

        Assert.Equal("example.jwt.token", second);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task AddInstantBasketAsyncSendsTerminalIdHeaderNeverBranchId()
    {
        var handler = new FakeHttpMessageHandler()
            .Enqueue(HttpStatusCode.OK, AuthSuccessBody)
            .Enqueue(HttpStatusCode.Created, """{ "status": 0, "description": "Instant Basket Record Successfully Created" }""");
        var client = new TokenBasketClient(new HttpClient(handler), "https://auth.example", "https://basket.example", "cid", "secret");

        var request = new TokenAddInstantBasketRequest(
            Guid.NewGuid(), 1, [new TokenBasketItem("Cheesecake", 15000, 1, 1000, 1000)]);

        await client.AddInstantBasketAsync("AV0000111044", request);

        var basketRequest = handler.Requests[1];
        Assert.True(basketRequest.Headers.TryGetValues("terminal-id", out var values));
        Assert.Equal("AV0000111044", Assert.Single(values));
        Assert.False(basketRequest.Headers.Contains("branch-id"));
    }

    [Fact]
    public async Task AddInstantBasketAsyncThrowsTokenApiExceptionOnTheDocumented1100Error()
    {
        var handler = new FakeHttpMessageHandler()
            .Enqueue(HttpStatusCode.OK, AuthSuccessBody)
            .Enqueue(HttpStatusCode.BadRequest, """{ "status": 1100, "description": "There is already an open basket assigned to this terminal" }""");
        var client = new TokenBasketClient(new HttpClient(handler), "https://auth.example", "https://basket.example", "cid", "secret");
        var request = new TokenAddInstantBasketRequest(Guid.NewGuid(), 1, [new TokenBasketItem("Cola", 7500, 1, 1800, 1000)]);

        var ex = await Assert.ThrowsAsync<TokenApiException>(() => client.AddInstantBasketAsync("AV0000111044", request));

        Assert.Equal(1100, ex.TokenStatusCode);
    }

    [Fact]
    public async Task GetBasketDetailsAsyncParsesTheDocumentedCompletedBasketShape()
    {
        const string completedBasketBody = """
            {
                "status": 0,
                "description": "Successfully Fetched the Basket",
                "result": {
                    "basketID": "653a9a6b-36c4-49e6-b6d2-e9e33d62e5ed",
                    "status": 1,
                    "isLocked": true,
                    "total": 22500,
                    "sale": {
                        "basketID": "653a9a6b-36c4-49e6-b6d2-e9e33d62e5ed",
                        "status": 0,
                        "message": "OK",
                        "receiptNo": 6,
                        "paymentItems": [
                            { "amount": 22500, "type": 3, "operatorId": 0, "status": -1 }
                        ]
                    }
                }
            }
            """;
        var handler = new FakeHttpMessageHandler()
            .Enqueue(HttpStatusCode.OK, AuthSuccessBody)
            .Enqueue(HttpStatusCode.OK, completedBasketBody);
        var client = new TokenBasketClient(new HttpClient(handler), "https://auth.example", "https://basket.example", "cid", "secret");

        var details = await client.GetBasketDetailsAsync("AV0000111044", Guid.Parse("653a9a6b-36c4-49e6-b6d2-e9e33d62e5ed"));

        Assert.NotNull(details.Sale);
        Assert.Equal(TokenSaleResult.SaleStatusSuccessful, details.Sale!.Status);
        Assert.Equal(6, details.Sale.ReceiptNo);
        Assert.Equal(22500, details.Total);
    }

    [Fact]
    public async Task PollUntilSettledAsyncReturnsNullAfterExhaustingAttemptsOnAnOpenBasket()
    {
        const string openBasketBody = """
            { "status": 0, "description": "OK", "result": { "basketID": "b", "status": 0, "isLocked": false, "total": 22500, "sale": null } }
            """;
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK, AuthSuccessBody);
        for (var i = 0; i < 3; i++)
            handler.Enqueue(HttpStatusCode.OK, openBasketBody);
        var client = new TokenBasketClient(new HttpClient(handler), "https://auth.example", "https://basket.example", "cid", "secret");

        var sale = await client.PollUntilSettledAsync("AV0000111044", Guid.NewGuid(), maxAttempts: 3, pollInterval: TimeSpan.Zero);

        Assert.Null(sale);
    }

    [Fact]
    public async Task PollUntilSettledAsyncReturnsTheSaleWhenItAppearsOnTheExactLastAttempt()
    {
        const string openBasketBody = """
            { "status": 0, "description": "OK", "result": { "basketID": "b", "status": 0, "isLocked": false, "total": 22500, "sale": null } }
            """;
        const string settledBasketBody = """
            { "status": 0, "description": "OK", "result": { "basketID": "b", "status": 1, "isLocked": true, "total": 22500,
              "sale": { "basketID": "b", "status": 0, "message": "OK", "receiptNo": 9, "paymentItems": [] } } }
            """;
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK, AuthSuccessBody);
        for (var i = 0; i < 2; i++)
            handler.Enqueue(HttpStatusCode.OK, openBasketBody);
        handler.Enqueue(HttpStatusCode.OK, settledBasketBody);
        var client = new TokenBasketClient(new HttpClient(handler), "https://auth.example", "https://basket.example", "cid", "secret");

        var sale = await client.PollUntilSettledAsync("AV0000111044", Guid.NewGuid(), maxAttempts: 3, pollInterval: TimeSpan.Zero);

        Assert.NotNull(sale);
        Assert.Equal(9, sale!.ReceiptNo);
    }

    [Fact]
    public async Task AddInstantBasketAsyncThrowsTokenApiExceptionOnTheDocumented1104Error()
    {
        var handler = new FakeHttpMessageHandler()
            .Enqueue(HttpStatusCode.OK, AuthSuccessBody)
            .Enqueue(HttpStatusCode.BadRequest, """{ "status": 1104, "description": "This terminals sale mode is not suitable for receive instant basket!" }""");
        var client = new TokenBasketClient(new HttpClient(handler), "https://auth.example", "https://basket.example", "cid", "secret");
        var request = new TokenAddInstantBasketRequest(Guid.NewGuid(), 1, [new TokenBasketItem("Su", 500, 1, 1000, 1000)]);

        var ex = await Assert.ThrowsAsync<TokenApiException>(() => client.AddInstantBasketAsync("AV0000111044", request));

        Assert.Equal(1104, ex.TokenStatusCode);
    }

    [Fact]
    public async Task AuthenticateAsyncThrowsWhenHttpStatusIsNotSuccessEvenIfBodyLooksValid()
    {
        var handler = new FakeHttpMessageHandler()
            .Enqueue(HttpStatusCode.Unauthorized, """{ "status": 401, "description": "Invalid client credentials." }""");
        var client = new TokenBasketClient(new HttpClient(handler), "https://auth.example", "https://basket.example", "wrong-cid", "wrong-secret");

        var ex = await Assert.ThrowsAsync<TokenApiException>(() => client.AuthenticateAsync());

        Assert.Contains("Invalid client credentials", ex.Message);
    }

    [Fact]
    public async Task AuthenticateAsyncThrowsOnMalformedJsonInsteadOfCrashingWithAnUnrelatedException()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK, "<html>not json</html>");
        var client = new TokenBasketClient(new HttpClient(handler), "https://auth.example", "https://basket.example", "cid", "secret");

        await Assert.ThrowsAsync<TokenApiException>(() => client.AuthenticateAsync());
    }

    [Fact]
    public async Task GetBasketDetailsAsyncThrowsOnMalformedJsonInsteadOfCrashingWithAnUnrelatedException()
    {
        var handler = new FakeHttpMessageHandler()
            .Enqueue(HttpStatusCode.OK, AuthSuccessBody)
            .Enqueue(HttpStatusCode.InternalServerError, "internal error, not json at all");
        var client = new TokenBasketClient(new HttpClient(handler), "https://auth.example", "https://basket.example", "cid", "secret");

        await Assert.ThrowsAsync<TokenApiException>(() => client.GetBasketDetailsAsync("AV0000111044", Guid.NewGuid()));
    }

    [Fact]
    public async Task AuthenticateAsyncReAuthenticatesOnceTheCachedTokenExpires()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var handler = new FakeHttpMessageHandler()
            .Enqueue(HttpStatusCode.OK, AuthSuccessBody)
            .Enqueue(HttpStatusCode.OK, AuthSuccessBody);
        var client = new TokenBasketClient(
            new HttpClient(handler), "https://auth.example", "https://basket.example", "cid", "secret",
            clock: () => now);

        await client.AuthenticateAsync();
        now = now.AddHours(25); // past the documented 86400s (24h) validity window
        await client.AuthenticateAsync();

        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task AddInstantBasketAsyncSerializesMealCardRoutingWithTheDocumentedOperatorIds()
    {
        var handler = new FakeHttpMessageHandler()
            .Enqueue(HttpStatusCode.OK, AuthSuccessBody)
            .Enqueue(HttpStatusCode.Created, """{ "status": 0, "description": "Instant Basket Record Successfully Created" }""");
        var client = new TokenBasketClient(new HttpClient(handler), "https://auth.example", "https://basket.example", "cid", "secret");

        // TokenFlex = 1005, per evidence/v0/integrations/V0-HUG-001/2026-09-18-operator-id-and-void-schema.md.
        // Note: routing a MealCard tender through Token is V13-MCD-004's own
        // scope, not V13-HUG-001's (see ITenderHandler's XML doc) — this test
        // only proves the generic client can serialize the shape, it is not
        // a claim that TokenTenderHandler (BankCard-only) ever sends this.
        var request = new TokenAddInstantBasketRequest(
            Guid.NewGuid(), 1, [new TokenBasketItem("Menu", 10000, 1, 1000, 1000)],
            PaymentItems: [new TokenPaymentRoutingItem(10000, TokenPaymentRoutingItem.PaymentTypeMealCard, OperatorId: 1005)]);

        await client.AddInstantBasketAsync("AV0000111044", request);

        var sentBody = handler.RequestBodies[1];
        Assert.Contains("\"type\":7", sentBody);
        Assert.Contains("\"operatorId\":1005", sentBody);
    }

    [Fact]
    public async Task AddInstantBasketAsyncPropagatesCancellationInsteadOfSwallowingIt()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK, AuthSuccessBody);
        var client = new TokenBasketClient(new HttpClient(handler), "https://auth.example", "https://basket.example", "cid", "secret");
        var request = new TokenAddInstantBasketRequest(Guid.NewGuid(), 1, [new TokenBasketItem("Su", 500, 1, 1000, 1000)]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.AddInstantBasketAsync("AV0000111044", request, cts.Token));
    }
}
