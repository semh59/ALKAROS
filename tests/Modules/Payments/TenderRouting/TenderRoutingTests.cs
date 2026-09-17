using Xunit;

namespace ALKAROS.Payments.TenderRouting.Tests;

/// <summary>
/// Pure domain tests for V13-PAY-002 — no database, no real Cash/BankCard/
/// MealCard handler (those are separate tasks). Covers unknown-method
/// rejection, the CustomerAccount version-not-enabled result, and
/// fail-closed routing when no handler is registered.
/// </summary>
public sealed class TenderRoutingTests
{
    [Theory]
    [InlineData("Cash", TenderMethod.Cash)]
    [InlineData("BankCard", TenderMethod.BankCard)]
    [InlineData("MealCard", TenderMethod.MealCard)]
    [InlineData("CustomerAccount", TenderMethod.CustomerAccount)]
    public void TryParseAcceptsEveryCanonicalMethodName(string raw, TenderMethod expected)
    {
        Assert.True(TenderMethodCatalog.TryParse(raw, out var method));
        Assert.Equal(expected, method);
    }

    [Theory]
    [InlineData("SplitPayment")]
    [InlineData("split_payment")]
    [InlineData("cash")]
    [InlineData("Foo")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("99")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData(" 1 ")]
    [InlineData("+1")]
    [InlineData("1.0")]
    public void TryParseRejectsSplitPaymentAndUnknownText(string? raw)
    {
        Assert.False(TenderMethodCatalog.TryParse(raw, out _));
    }

    [Fact]
    public void TenderRequestValidateRejectsEmptyPaymentId()
    {
        var request = new TenderRequest(Guid.Empty, TenderMethod.Cash, 50m);
        Assert.Throws<ArgumentException>(request.Validate);
    }

    [Fact]
    public void TenderRequestValidateRejectsZeroAndNegativeAmount()
    {
        Assert.Throws<ArgumentException>(new TenderRequest(Guid.NewGuid(), TenderMethod.Cash, 0m).Validate);
        Assert.Throws<ArgumentException>(new TenderRequest(Guid.NewGuid(), TenderMethod.Cash, -5m).Validate);
    }

    [Fact]
    public async Task RouteRejectsCustomerAccountUnconditionallyEvenIfSomehowRegistered()
    {
        var registry = new TenderHandlerRegistry();
        registry.Register(new FakeHandler(TenderMethod.CustomerAccount, new TenderApproved(50m)));
        var router = new TenderRouter(registry);
        var request = new TenderRequest(Guid.NewGuid(), TenderMethod.CustomerAccount, 50m);

        var result = await router.RouteAsync(request);

        var notEnabled = Assert.IsType<TenderVersionNotEnabled>(result);
        Assert.Equal(TenderMethod.CustomerAccount, notEnabled.Method);
        Assert.Equal("TENDER_VERSION_NOT_ENABLED", TenderVersionNotEnabled.Code);
    }

    [Theory]
    [InlineData(TenderMethod.Cash)]
    [InlineData(TenderMethod.BankCard)]
    [InlineData(TenderMethod.MealCard)]
    public async Task RouteFailsClosedWhenNoHandlerIsRegistered(TenderMethod method)
    {
        var router = new TenderRouter(new TenderHandlerRegistry());
        var request = new TenderRequest(Guid.NewGuid(), method, 50m);

        var result = await router.RouteAsync(request);

        var notRegistered = Assert.IsType<TenderMethodNotRegistered>(result);
        Assert.Equal(method, notRegistered.Method);
        Assert.Equal("TENDER_METHOD_NOT_REGISTERED", TenderMethodNotRegistered.Code);
    }

    [Fact]
    public async Task RouteInvokesTheRegisteredHandlerAndWrapsItsResult()
    {
        var registry = new TenderHandlerRegistry();
        registry.Register(new FakeHandler(TenderMethod.Cash, new TenderApproved(80m)));
        var router = new TenderRouter(registry);
        var request = new TenderRequest(Guid.NewGuid(), TenderMethod.Cash, 80m);

        var result = await router.RouteAsync(request);

        var handled = Assert.IsType<TenderRoutingHandled>(result);
        var approved = Assert.IsType<TenderApproved>(handled.Result);
        Assert.Equal(80m, approved.ApprovedAmount);
    }

    [Fact]
    public async Task RouteValidatesTheRequestBeforeConsultingTheRegistry()
    {
        var router = new TenderRouter(new TenderHandlerRegistry());
        var invalid = new TenderRequest(Guid.NewGuid(), TenderMethod.Cash, -1m);

        await Assert.ThrowsAsync<ArgumentException>(() => router.RouteAsync(invalid));
    }

    [Fact]
    public void RegistryRejectsRegisteringTheSameMethodTwice()
    {
        var registry = new TenderHandlerRegistry();
        registry.Register(new FakeHandler(TenderMethod.Cash, new TenderApproved(1m)));

        Assert.Throws<InvalidOperationException>(
            () => registry.Register(new FakeHandler(TenderMethod.Cash, new TenderApproved(1m))));
    }

    [Fact]
    public void RegistryTryGetReturnsFalseForAnUnregisteredMethod()
    {
        var registry = new TenderHandlerRegistry();
        Assert.False(registry.TryGet(TenderMethod.MealCard, out _));
    }

    private sealed class FakeHandler(TenderMethod method, TenderHandlerResult result) : ITenderHandler
    {
        public TenderMethod Method { get; } = method;

        public Task<TenderHandlerResult> HandleAsync(TenderRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(result);
    }
}
