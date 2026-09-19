using System.Net;
using Xunit;

namespace ALKAROS.Payments.Token.Draft.Tests;

public sealed class TokenTenderHandlerTests
{
    private const string AuthSuccessBody = """
        { "status": 201, "description": "User authenticated successfully.", "result": { "accessToken": "t" } }
        """;

    private const string InstantBasketAcceptedBody = """
        { "status": 0, "description": "Instant Basket Record Successfully Created" }
        """;

    [Fact]
    public async Task HandleAsyncMapsSaleStatus0ToApproved()
    {
        var settledBody = """
            {
                "status": 0, "description": "OK",
                "result": {
                    "basketID": "b", "status": 1, "isLocked": true, "total": 15000,
                    "sale": { "basketID": "b", "status": 0, "message": "OK", "receiptNo": 6, "paymentItems": [] }
                }
            }
            """;
        var handler = new FakeHttpMessageHandler()
            .Enqueue(HttpStatusCode.OK, AuthSuccessBody)
            .Enqueue(HttpStatusCode.Created, InstantBasketAcceptedBody)
            .Enqueue(HttpStatusCode.OK, settledBody);
        var handlerUnderTest = new TokenTenderHandler(
            new TokenBasketClient(new HttpClient(handler), "https://auth.example", "https://basket.example", "cid", "secret"),
            "AV0000111044");

        var outcome = await handlerUnderTest.HandleAsync(Guid.NewGuid(), amountKurus: 15000);

        var approved = Assert.IsType<TokenTenderOutcome.Approved>(outcome);
        Assert.Equal(15000, approved.ApprovedAmountKurus);
        Assert.Equal(6, approved.Evidence.ReceiptNo);
    }

    [Fact]
    public async Task HandleAsyncMapsSaleStatusMinus1ToDeclined()
    {
        var settledBody = """
            {
                "status": 0, "description": "OK",
                "result": {
                    "basketID": "b", "status": 1, "isLocked": true, "total": 15000,
                    "sale": { "basketID": "b", "status": -1, "message": "Kart reddedildi.", "receiptNo": null, "paymentItems": [] }
                }
            }
            """;
        var handler = new FakeHttpMessageHandler()
            .Enqueue(HttpStatusCode.OK, AuthSuccessBody)
            .Enqueue(HttpStatusCode.Created, InstantBasketAcceptedBody)
            .Enqueue(HttpStatusCode.OK, settledBody);
        var handlerUnderTest = new TokenTenderHandler(
            new TokenBasketClient(new HttpClient(handler), "https://auth.example", "https://basket.example", "cid", "secret"),
            "AV0000111044");

        var outcome = await handlerUnderTest.HandleAsync(Guid.NewGuid(), amountKurus: 15000);

        var declined = Assert.IsType<TokenTenderOutcome.Declined>(outcome);
        Assert.Equal("Kart reddedildi.", declined.Reason);
    }

    [Fact]
    public async Task HandleAsyncNeverTreatsAPollingTimeoutAsApprovedOrDeclined()
    {
        var openBasketBody = """
            { "status": 0, "description": "OK", "result": { "basketID": "b", "status": 0, "isLocked": false, "total": 15000, "sale": null } }
            """;
        var handler = new FakeHttpMessageHandler()
            .Enqueue(HttpStatusCode.OK, AuthSuccessBody)
            .Enqueue(HttpStatusCode.Created, InstantBasketAcceptedBody);
        for (var i = 0; i < 10; i++)
            handler.Enqueue(HttpStatusCode.OK, openBasketBody);
        var handlerUnderTest = new TokenTenderHandler(
            new TokenBasketClient(new HttpClient(handler), "https://auth.example", "https://basket.example", "cid", "secret"),
            "AV0000111044");

        var outcome = await handlerUnderTest.HandleAsync(Guid.NewGuid(), amountKurus: 15000);

        Assert.IsType<TokenTenderOutcome.RequiresReconciliation>(outcome);
    }

    [Fact]
    public async Task HandleAsyncTreatsAnUnexpectedSaleStatusAsRequiresReconciliationNotApproved()
    {
        // status 99 = "Fiş iptal" (void) — out of this handler's scope
        // (V13-HUG-003 owns cancel/refund); must never be silently approved.
        var voidedBody = """
            {
                "status": 0, "description": "OK",
                "result": {
                    "basketID": "b", "status": 1, "isLocked": true, "total": 15000,
                    "sale": { "basketID": "b", "status": 99, "message": "Fiş iptal", "receiptNo": 6, "paymentItems": [] }
                }
            }
            """;
        var handler = new FakeHttpMessageHandler()
            .Enqueue(HttpStatusCode.OK, AuthSuccessBody)
            .Enqueue(HttpStatusCode.Created, InstantBasketAcceptedBody)
            .Enqueue(HttpStatusCode.OK, voidedBody);
        var handlerUnderTest = new TokenTenderHandler(
            new TokenBasketClient(new HttpClient(handler), "https://auth.example", "https://basket.example", "cid", "secret"),
            "AV0000111044");

        var outcome = await handlerUnderTest.HandleAsync(Guid.NewGuid(), amountKurus: 15000);

        Assert.IsType<TokenTenderOutcome.RequiresReconciliation>(outcome);
    }

    [Fact]
    public async Task HandleAsyncRejectsANonPositiveAmountBeforeAnyNetworkCall()
    {
        var handler = new FakeHttpMessageHandler();
        var handlerUnderTest = new TokenTenderHandler(
            new TokenBasketClient(new HttpClient(handler), "https://auth.example", "https://basket.example", "cid", "secret"),
            "AV0000111044");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => handlerUnderTest.HandleAsync(Guid.NewGuid(), amountKurus: 0));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task HandleAsyncThrowsRatherThanReturningDeclinedWhenTheTerminalRejectsTheRequestItself()
    {
        // 1100 ("terminal already has an open basket") is a request-mapping
        // failure — the card was never even presented — so it must surface
        // as an exception, never as a false TokenTenderOutcome.Declined
        // (that would misreport "card rejected" for "terminal was busy").
        var handler = new FakeHttpMessageHandler()
            .Enqueue(HttpStatusCode.OK, AuthSuccessBody)
            .Enqueue(HttpStatusCode.BadRequest, """{ "status": 1100, "description": "There is already an open basket assigned to this terminal" }""");
        var handlerUnderTest = new TokenTenderHandler(
            new TokenBasketClient(new HttpClient(handler), "https://auth.example", "https://basket.example", "cid", "secret"),
            "AV0000111044");

        var ex = await Assert.ThrowsAsync<TokenApiException>(() => handlerUnderTest.HandleAsync(Guid.NewGuid(), amountKurus: 15000));

        Assert.Equal(1100, ex.TokenStatusCode);
    }

    [Fact]
    public async Task HandleAsyncRoutesTheCardPaymentAsTypeThreeWithNoForcedOperator()
    {
        // V13-HUG-001's own scope is card (BankCard) only — operatorId 0
        // means "any bank / open selection on the terminal", never a forced
        // bank app, since ALKAROS has no basis for picking one.
        var settledBody = """
            { "status": 0, "description": "OK", "result": { "basketID": "b", "status": 1, "isLocked": true, "total": 15000,
              "sale": { "basketID": "b", "status": 0, "message": "OK", "receiptNo": 1, "paymentItems": [] } } }
            """;
        var handler = new FakeHttpMessageHandler()
            .Enqueue(HttpStatusCode.OK, AuthSuccessBody)
            .Enqueue(HttpStatusCode.Created, InstantBasketAcceptedBody)
            .Enqueue(HttpStatusCode.OK, settledBody);
        var handlerUnderTest = new TokenTenderHandler(
            new TokenBasketClient(new HttpClient(handler), "https://auth.example", "https://basket.example", "cid", "secret"),
            "AV0000111044");

        await handlerUnderTest.HandleAsync(Guid.NewGuid(), amountKurus: 15000);

        var sentBody = handler.RequestBodies[1];
        Assert.Contains("\"type\":3", sentBody);
        Assert.Contains("\"operatorId\":0", sentBody);
    }
}
