using System.Net;
using System.Text.Json;
using ALKAROS.Reporting.V1Operations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Audit.MoneyFlowProbes;

/// <summary>
/// V1-RMD-393 money-flow probes. Every probe asserts what the system MUST do (the invariant or the documented
/// contract named in its comment). A failing probe is an audit finding; a passing probe closes its matrix cell
/// as verified-sound. Probe ids (P01..P12) are the ids used in evidence/V1-RMD-393/REPORT.md.
/// </summary>
public sealed class MoneyFlowProbes : IClassFixture<ProbeHarness>
{
    private const string Discount = "bills.discount";
    private const string Split = "bills.split";
    private const string ReconciliationManage = "reconciliation.manage";
    private const string CloseDay = "reports.close-day";

    private readonly ProbeHarness _h;

    public MoneyFlowProbes(ProbeHarness harness) => _h = harness;

    private static string Tenders(Guid terminalId, Guid billId)
        => $"/api/v1/terminals/{terminalId:D}/billing/bills/{billId:D}/tenders/";

    private static string CashTender(Guid terminalId, Guid sessionId)
        => $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/cash-tender";

    private async Task<decimal> RemainingAsync(Guid terminalId, Guid billId, string cookie)
    {
        var summary = await _h.GetAsync(Tenders(terminalId, billId), cookie);
        Assert.Equal(HttpStatusCode.OK, summary.StatusCode);
        return (await ProbeHarness.JsonAsync(summary)).GetProperty("remainingAmount").GetDecimal();
    }

    private async Task<Guid> UnresolvedCardAttemptAsync(Guid terminalId, Guid billId, string cookie, decimal amount)
    {
        var card = await _h.PostAsync(Tenders(terminalId, billId), cookie,
            new { Method = "BankCard", Amount = amount, IdempotencyKey = "card-" + Guid.NewGuid().ToString("N") });
        Assert.Equal(HttpStatusCode.OK, card.StatusCode);
        Assert.Equal("RequiresReconciliation", (await ProbeHarness.JsonAsync(card)).GetProperty("outcome").GetString());
        var summary = await ProbeHarness.JsonAsync(await _h.GetAsync(Tenders(terminalId, billId), cookie));
        return summary.GetProperty("unsettledPayment").GetProperty("paymentId").GetGuid();
    }

    // P01 - V1-RMD-258 contract: "a genuinely NEW tender must never be layered on top of a Bill that already has
    // an unresolved Payment". EFT and BankCard enforce it server-side; this probes the Cash path.
    [Fact]
    public async Task P01CashTenderIsRejectedWhileACardAttemptIsUnresolved()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _h.SeedCashierAsync(terminalId);
        var sessionId = await _h.OpenCashSessionAsync(terminalId, cookie, 100m);
        var billId = await _h.SeedBillAsync(100m);
        await UnresolvedCardAttemptAsync(terminalId, billId, cookie, 100m);

        var cash = await _h.PostAsync(CashTender(terminalId, sessionId), cookie,
            new { BillId = billId, AmountDue = 100m, TenderedAmount = 100m, IdempotencyKey = "p01-" + Guid.NewGuid().ToString("N") });

        Assert.Equal(HttpStatusCode.Conflict, cash.StatusCode);
        Assert.Equal(0L, await _h.SaleLedgerCountAsync(sessionId));
    }

    // P02 - V0-DOM-004 invariant: sum(allocations) <= adjusted payable, at all times. A discount applied after
    // money was already collected must not push the payable below what was collected.
    [Fact]
    public async Task P02DiscountCannotDropThePayableBelowTheAmountAlreadyCollected()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _h.SeedCashierAsync(terminalId, Discount);
        var billId = await _h.SeedBillAsync(100m);
        var eft = await _h.PostAsync(Tenders(terminalId, billId), cookie,
            new { Method = "Eft", Amount = 80m, IdempotencyKey = "p02-eft-" + Guid.NewGuid().ToString("N") });
        Assert.Equal(HttpStatusCode.OK, eft.StatusCode);

        var discount = await _h.PostAsync($"/api/v1/terminals/{terminalId:D}/billing/bills/{billId:D}/discount", cookie,
            new { IdempotencyKey = "p02-d-" + Guid.NewGuid().ToString("N"), CalculationType = "FixedAmount", Value = 30m, ReasonCode = "PromotionalOffer" });

        var remaining = await RemainingAsync(terminalId, billId, cookie);
        Assert.True(remaining >= 0m,
            $"Discount returned {(int)discount.StatusCode}; the bill now has 80.00 collected against a payable of " +
            $"{80m + remaining:0.00} (remaining {remaining:0.00}) - an unrecorded overpayment.");
    }

    // P03 - a Cancelled bill is terminal (BillState); no money may be taken against it.
    [Fact]
    public async Task P03CashTenderIsRejectedOnACancelledBill()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _h.SeedCashierAsync(terminalId);
        var sessionId = await _h.OpenCashSessionAsync(terminalId, cookie, 0m);
        var billId = await _h.SeedBillAsync(50m);
        await _h.ScalarAsync<int>("UPDATE billing.bills SET status = 'Cancelled' WHERE bill_id = @id RETURNING 1;", ("id", billId));

        var cash = await _h.PostAsync(CashTender(terminalId, sessionId), cookie,
            new { BillId = billId, AmountDue = 50m, TenderedAmount = 50m, IdempotencyKey = "p03-" + Guid.NewGuid().ToString("N") });

        Assert.NotEqual(HttpStatusCode.OK, cash.StatusCode);
        Assert.Equal(0L, await _h.PaymentCountAsync(billId));
    }

    // P04 - a cash session belongs to one terminal (cash-session-design.md: "Terminal has NO other active
    // session"); a cashier authenticated on terminal B must not post sales into terminal A's drawer.
    [Fact]
    public async Task P04ACashierOfAnotherTerminalCannotTenderIntoThisTerminalsDrawer()
    {
        var terminalA = Guid.NewGuid();
        var terminalB = Guid.NewGuid();
        var (_, cookieA) = await _h.SeedCashierAsync(terminalA);
        var (_, cookieB) = await _h.SeedCashierAsync(terminalB);
        var sessionA = await _h.OpenCashSessionAsync(terminalA, cookieA, 0m);
        var billId = await _h.SeedBillAsync(40m);

        var cash = await _h.PostAsync(CashTender(terminalB, sessionA), cookieB,
            new { BillId = billId, AmountDue = 40m, TenderedAmount = 40m, IdempotencyKey = "p04-" + Guid.NewGuid().ToString("N") });

        Assert.NotEqual(HttpStatusCode.OK, cash.StatusCode);
        Assert.Equal(0L, await _h.SaleLedgerCountAsync(sessionA));
    }

    // P05 - docs/domain/cash-session-design.md:109: ReconcileSession is "Supervisor, Accountant, Admin" only.
    [Fact]
    public async Task P05APlainCashierCannotReconcileACashSession()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _h.SeedCashierAsync(terminalId);
        var sessionId = await _h.OpenCashSessionAsync(terminalId, cookie, 100m);
        var close = await _h.PostAsync($"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/close", cookie, new { ActualCash = 100m });
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);

        var reconcile = await _h.PostAsync($"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/reconcile", cookie,
            new { Notes = "self-reconciled" });

        Assert.Equal(HttpStatusCode.Forbidden, reconcile.StatusCode);
    }

    // P06 - an end-of-day report's revenue must be the recorded approved payments of that business day, not a
    // free number the caller types. Probed at the service boundary the HTTP close endpoint forwards to verbatim.
    [Fact]
    public async Task P06EndOfDayRevenueIsDerivedFromRecordedPaymentsNotFromTheCaller()
    {
        var businessDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(5).AddDays(Random.Shared.Next(1, 300)));
        await using var scope = _h.App.Services.CreateAsyncScope();
        var reports = scope.ServiceProvider.GetRequiredService<IOperationalReportService>();
        await reports.OpenBusinessDayAsync(businessDate, DateTimeOffset.UtcNow);

        var closed = await reports.CloseBusinessDayAsync(
            businessDate, DateTimeOffset.UtcNow, totalRevenue: 987654.32m, totalOrders: 4242,
            cancelledItems: 0, printFailures: 0);

        // No payment exists for this (future) business date, so the only correct revenue is 0.00.
        Assert.Equal(0m, closed.BusinessDay.TotalRevenue);
    }

    // P07 - a manual card approval that can no longer be allocated (the bill was covered meanwhile) must come
    // back as a typed Turkish 409 like every other over-allocation, never a raw 500 (UI_STYLE_GUIDE).
    [Fact]
    public async Task P07ApprovingACardClaimOnAnAlreadyCoveredBillIsATyped409()
    {
        var terminalId = Guid.NewGuid();
        var (_, cashier) = await _h.SeedCashierAsync(terminalId);
        var (_, managerA) = await _h.SeedCashierAsync(terminalId, ReconciliationManage);
        var (_, managerB) = await _h.SeedCashierAsync(terminalId, ReconciliationManage);
        var sessionId = await _h.OpenCashSessionAsync(terminalId, cashier, 0m);
        var billId = await _h.SeedBillAsync(60m);
        var paymentId = await UnresolvedCardAttemptAsync(terminalId, billId, cashier, 60m);

        var cash = await _h.PostAsync(CashTender(terminalId, sessionId), cashier,
            new { BillId = billId, AmountDue = 60m, TenderedAmount = 60m, IdempotencyKey = "p07-" + Guid.NewGuid().ToString("N") });
        Assert.True(cash.StatusCode == HttpStatusCode.OK,
            $"Precondition (P01 behaviour) not met: cash tender returned {(int)cash.StatusCode}; P07 is only reachable while P01 fails.");

        var claim = await _h.PostAsync($"{Tenders(terminalId, billId)}unsettled/{paymentId:D}/card-charged", managerA,
            new { SlipNumber = "P07-" + Random.Shared.Next(1000, 9999), Note = (string?)null });
        Assert.Equal(HttpStatusCode.OK, claim.StatusCode);
        var confirmationId = (await ProbeHarness.JsonAsync(claim)).GetProperty("confirmationId").GetGuid();

        var approve = await _h.PostAsync($"{Tenders(terminalId, billId)}confirmations/{confirmationId:D}/approve", managerB,
            new { Note = (string?)null });

        Assert.Equal(HttpStatusCode.Conflict, approve.StatusCode);
    }

    // P08 - an idempotency key names ONE command. Reusing an EFT tender's key for a cash tender must not be
    // "replayed" as a cash success that recorded no drawer entry.
    [Fact]
    public async Task P08ACashTenderReusingAnEftKeyIsNotReportedAsASuccessfulCashSale()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _h.SeedCashierAsync(terminalId);
        var sessionId = await _h.OpenCashSessionAsync(terminalId, cookie, 0m);
        var billId = await _h.SeedBillAsync(100m);
        var key = "p08-" + Guid.NewGuid().ToString("N");
        var eft = await _h.PostAsync(Tenders(terminalId, billId), cookie, new { Method = "Eft", Amount = 40m, IdempotencyKey = key });
        Assert.Equal(HttpStatusCode.OK, eft.StatusCode);

        var cash = await _h.PostAsync(CashTender(terminalId, sessionId), cookie,
            new { BillId = billId, AmountDue = 40m, TenderedAmount = 40m, IdempotencyKey = key });

        var reportedSuccess = cash.StatusCode == HttpStatusCode.OK;
        var drawerEntries = await _h.SaleLedgerCountAsync(sessionId);
        Assert.False(reportedSuccess && drawerEntries == 0,
            "The cash tender answered 200 (a 'successful' sale the cashier will put money in the drawer for) " +
            "but no cash Sale ledger entry exists - the drawer will be over at close.");
    }

    // P09 - once the only blocker (an unresolved card attempt) is resolved as "not charged", a bill whose
    // approved allocations already cover it must become Paid; otherwise it has remaining 0 and no path to close.
    [Fact]
    public async Task P09ResolvingTheBlockingCardAttemptClosesAnAlreadyCoveredBill()
    {
        var terminalId = Guid.NewGuid();
        var (_, cashier) = await _h.SeedCashierAsync(terminalId);
        var (_, manager) = await _h.SeedCashierAsync(terminalId, ReconciliationManage);
        var sessionId = await _h.OpenCashSessionAsync(terminalId, cashier, 0m);
        var billId = await _h.SeedBillAsync(70m);
        var paymentId = await UnresolvedCardAttemptAsync(terminalId, billId, cashier, 70m);
        var cash = await _h.PostAsync(CashTender(terminalId, sessionId), cashier,
            new { BillId = billId, AmountDue = 70m, TenderedAmount = 70m, IdempotencyKey = "p09-" + Guid.NewGuid().ToString("N") });
        Assert.True(cash.StatusCode == HttpStatusCode.OK,
            $"Precondition (P01 behaviour) not met: cash tender returned {(int)cash.StatusCode}.");

        var resolved = await _h.PostAsync($"{Tenders(terminalId, billId)}unsettled/{paymentId:D}/not-charged", manager,
            new { Reason = "customer paid cash instead" });
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);

        Assert.Equal(0m, await RemainingAsync(terminalId, billId, cashier));
        Assert.Equal("Paid", await _h.BillStatusAsync(billId));
    }

    // P10 - CONTROL (expected to pass): the documented happy path with discount + tip + split EFT/cash with
    // change. Proves the probes are not failing for harness reasons, and closes the happy-path matrix cells.
    [Fact]
    public async Task P10ControlDiscountTipSplitTenderHappyPathSatisfiesEveryInvariant()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _h.SeedCashierAsync(terminalId, Discount, Split);
        var sessionId = await _h.OpenCashSessionAsync(terminalId, cookie, 200m);
        var billId = await _h.SeedBillAsync(100m);

        var discount = await _h.PostAsync($"/api/v1/terminals/{terminalId:D}/billing/bills/{billId:D}/discount", cookie,
            new { IdempotencyKey = "p10-d-" + Guid.NewGuid().ToString("N"), CalculationType = "Percentage", Value = 10m, ReasonCode = "PromotionalOffer" });
        Assert.Equal(HttpStatusCode.OK, discount.StatusCode);
        var tip = await _h.PostAsync($"/api/v1/terminals/{terminalId:D}/billing/bills/{billId:D}/tip", cookie,
            new { IdempotencyKey = "p10-t-" + Guid.NewGuid().ToString("N"), Amount = 15m });
        Assert.Equal(HttpStatusCode.OK, tip.StatusCode);
        Assert.Equal(105m, await RemainingAsync(terminalId, billId, cookie)); // 100 - 10 + 15

        var eft = await _h.PostAsync(Tenders(terminalId, billId), cookie,
            new { Method = "Eft", Amount = 55m, IdempotencyKey = "p10-e-" + Guid.NewGuid().ToString("N") });
        Assert.Equal(HttpStatusCode.OK, eft.StatusCode);
        var cash = await _h.PostAsync(CashTender(terminalId, sessionId), cookie,
            new { BillId = billId, AmountDue = 50m, TenderedAmount = 60m, IdempotencyKey = "p10-c-" + Guid.NewGuid().ToString("N") });
        Assert.Equal(HttpStatusCode.OK, cash.StatusCode);
        Assert.Equal(10m, (await ProbeHarness.JsonAsync(cash)).GetProperty("changeAmount").GetDecimal());

        Assert.Equal(105m, await _h.AllocatedTotalAsync(billId));
        Assert.Equal(0m, await RemainingAsync(terminalId, billId, cookie));
        Assert.Equal("Paid", await _h.BillStatusAsync(billId));
        Assert.Equal(250m, await _h.ExpectedCashFromLedgerAsync(sessionId)); // 200 float + 50 cash sale (change never enters)

        var expected = await _h.GetAsync($"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/expected-cash", cookie);
        Assert.Equal(250m, (await ProbeHarness.JsonAsync(expected)).GetProperty("expectedCash").GetDecimal());
    }

    // P11 - idempotency under a real race: N identical cash-tender submits (same key) racing each other must
    // produce exactly one Payment, one allocation and one drawer Sale entry.
    [Fact]
    public async Task P11ConcurrentIdenticalCashSubmitsRecordExactlyOneSale()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _h.SeedCashierAsync(terminalId);
        var sessionId = await _h.OpenCashSessionAsync(terminalId, cookie, 0m);
        var billId = await _h.SeedBillAsync(90m);
        var key = "p11-" + Guid.NewGuid().ToString("N");

        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => _h.PostAsync(CashTender(terminalId, sessionId), cookie,
            new { BillId = billId, AmountDue = 90m, TenderedAmount = 100m, IdempotencyKey = key })));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(1L, await _h.PaymentCountAsync(billId));
        Assert.Equal(1L, await _h.SaleLedgerCountAsync(sessionId));
        Assert.Equal(90m, await _h.AllocatedTotalAsync(billId));
    }

    // P12 - a race between a discount and a full payment must still end with sum(allocations) <= adjusted payable
    // (discount holds a row lock on billing.bills, tenders hold an advisory lock - two different locks).
    [Fact]
    public async Task P12ARacingDiscountAndFullPaymentNeverLeaveTheBillOverCollected()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _h.SeedCashierAsync(terminalId, Discount);
        var violations = new List<string>();
        for (var i = 0; i < 10; i++)
        {
            var billId = await _h.SeedBillAsync(100m);
            var eft = _h.PostAsync(Tenders(terminalId, billId), cookie,
                new { Method = "Eft", Amount = 100m, IdempotencyKey = $"p12-e-{i}-" + Guid.NewGuid().ToString("N") });
            var discount = _h.PostAsync($"/api/v1/terminals/{terminalId:D}/billing/bills/{billId:D}/discount", cookie,
                new { IdempotencyKey = $"p12-d-{i}-" + Guid.NewGuid().ToString("N"), CalculationType = "FixedAmount", Value = 30m, ReasonCode = "PromotionalOffer" });
            await Task.WhenAll(eft, discount);
            var remaining = await RemainingAsync(terminalId, billId, cookie);
            if (remaining < 0m)
                violations.Add($"iteration {i}: eft={(int)eft.Result.StatusCode} discount={(int)discount.Result.StatusCode} remaining={remaining:0.00}");
        }

        Assert.True(violations.Count == 0, "Over-collected bills: " + string.Join("; ", violations));
    }

    // P15 - CARDINALITY (added after the blind calibration run missed a seeded bug: every earlier probe used at
    // most one adjustment of each kind). Several tips, several discounts and three partial tenders on one bill:
    // the adjusted payable must be the sum of all of them, and the bill must close exactly at that sum.
    [Fact]
    public async Task P15MultipleTipsDiscountsAndPartialTendersAllCount()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _h.SeedCashierAsync(terminalId, Discount, Split);
        var sessionId = await _h.OpenCashSessionAsync(terminalId, cookie, 0m);
        var billId = await _h.SeedBillAsync(200m);
        var bills = $"/api/v1/terminals/{terminalId:D}/billing/bills/{billId:D}";

        foreach (var tip in new[] { 15m, 10m, 5m })
            Assert.Equal(HttpStatusCode.OK, (await _h.PostAsync(bills + "/tip", cookie,
                new { IdempotencyKey = "p15-t-" + Guid.NewGuid().ToString("N"), Amount = tip })).StatusCode);
        foreach (var discount in new[] { 20m, 12m })
            Assert.Equal(HttpStatusCode.OK, (await _h.PostAsync(bills + "/discount", cookie,
                new { IdempotencyKey = "p15-d-" + Guid.NewGuid().ToString("N"), CalculationType = "FixedAmount", Value = discount, ReasonCode = "PromotionalOffer" })).StatusCode);

        const decimal expectedPayable = 200m + 15m + 10m + 5m - 20m - 12m; // 198.00
        Assert.Equal(expectedPayable, await RemainingAsync(terminalId, billId, cookie));

        Assert.Equal(HttpStatusCode.OK, (await _h.PostAsync(Tenders(terminalId, billId), cookie,
            new { Method = "Eft", Amount = 50m, IdempotencyKey = "p15-e1-" + Guid.NewGuid().ToString("N") })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _h.PostAsync(Tenders(terminalId, billId), cookie,
            new { Method = "Eft", Amount = 48m, IdempotencyKey = "p15-e2-" + Guid.NewGuid().ToString("N") })).StatusCode);
        Assert.Equal("Open", await _h.BillStatusAsync(billId));
        Assert.Equal(HttpStatusCode.OK, (await _h.PostAsync(CashTender(terminalId, sessionId), cookie,
            new { BillId = billId, AmountDue = 100m, TenderedAmount = 100m, IdempotencyKey = "p15-c-" + Guid.NewGuid().ToString("N") })).StatusCode);

        Assert.Equal(expectedPayable, await _h.AllocatedTotalAsync(billId));
        Assert.Equal("Paid", await _h.BillStatusAsync(billId));
    }

    // P16 - the payment settlement report's per-method totals for the business day must move by exactly the
    // approved amount of each tender (cash counted at the approved amount, never the tendered amount incl. change;
    // a manually confirmed card payment counted as BankCard), and their sum must equal what the bill allocated.
    [Fact]
    public async Task P16SettlementReportMethodTotalsMatchTheTendersTaken()
    {
        var terminalId = Guid.NewGuid();
        var (_, cashier) = await _h.SeedCashierAsync(terminalId);
        var (_, managerA) = await _h.SeedCashierAsync(terminalId, ReconciliationManage);
        var (_, managerB) = await _h.SeedCashierAsync(terminalId, ReconciliationManage);
        var sessionId = await _h.OpenCashSessionAsync(terminalId, cashier, 0m);
        var billId = await _h.SeedBillAsync(95m);

        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        var businessDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).AddHours(-6).DateTime);
        async Task<Dictionary<string, decimal>> MixAsync()
        {
            await using var scope = _h.App.Services.CreateAsyncScope();
            var report = await scope.ServiceProvider.GetRequiredService<ALKAROS.Reporting.Payments.IPaymentSettlementReportService>()
                .GetReportAsync(new ALKAROS.Reporting.Payments.PaymentSettlementReportFilter(businessDate));
            return report.PaymentMix.ToDictionary(m => m.Method, m => m.ApprovedAmount);
        }
        static decimal Of(Dictionary<string, decimal> mix, string method) => mix.TryGetValue(method, out var v) ? v : 0m;

        var before = await MixAsync();

        var paymentId = await UnresolvedCardAttemptAsync(terminalId, billId, cashier, 25m);
        var claim = await _h.PostAsync($"{Tenders(terminalId, billId)}unsettled/{paymentId:D}/card-charged", managerA,
            new { SlipNumber = "P16-" + Random.Shared.Next(1000, 9999), Note = (string?)null });
        Assert.Equal(HttpStatusCode.OK, claim.StatusCode);
        var confirmationId = (await ProbeHarness.JsonAsync(claim)).GetProperty("confirmationId").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await _h.PostAsync(
            $"{Tenders(terminalId, billId)}confirmations/{confirmationId:D}/approve", managerB, new { Note = (string?)null })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _h.PostAsync(Tenders(terminalId, billId), cashier,
            new { Method = "Eft", Amount = 30m, IdempotencyKey = "p16-e-" + Guid.NewGuid().ToString("N") })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _h.PostAsync(CashTender(terminalId, sessionId), cashier,
            new { BillId = billId, AmountDue = 40m, TenderedAmount = 50m, IdempotencyKey = "p16-c-" + Guid.NewGuid().ToString("N") })).StatusCode);

        var after = await MixAsync();
        var delta = new
        {
            Cash = Of(after, "Cash") - Of(before, "Cash"),
            Eft = Of(after, "Eft") - Of(before, "Eft"),
            Card = Of(after, "BankCard") - Of(before, "BankCard"),
        };
        Assert.True(delta is { Cash: 40m, Eft: 30m, Card: 25m },
            $"Report deltas Cash={delta.Cash:0.00} Eft={delta.Eft:0.00} BankCard={delta.Card:0.00}; expected 40.00 / 30.00 / 25.00.");
        Assert.Equal(95m, await _h.AllocatedTotalAsync(billId));
        Assert.Equal("Paid", await _h.BillStatusAsync(billId));
    }

    // P14 - after a discount the split-design screen must show (and accept) the same payable the tender path
    // enforces. PosTerminal's BillSplitWorkspace.tsx:74 only lets a ByAmount split through when the targets sum
    // to exactly design.payableAmount, so that value must be the adjusted payable.
    [Fact]
    public async Task P14SplitDesignUsesTheSamePayableAsTheTenderPathAfterADiscount()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _h.SeedCashierAsync(terminalId, Discount, Split);
        var billId = await _h.SeedBillAsync(100m);
        var discount = await _h.PostAsync($"/api/v1/terminals/{terminalId:D}/billing/bills/{billId:D}/discount", cookie,
            new { IdempotencyKey = "p14-d-" + Guid.NewGuid().ToString("N"), CalculationType = "Percentage", Value = 10m, ReasonCode = "PromotionalOffer" });
        Assert.Equal(HttpStatusCode.OK, discount.StatusCode);

        var tenderPayable = (await ProbeHarness.JsonAsync(await _h.GetAsync(Tenders(terminalId, billId), cookie)))
            .GetProperty("payableAmount").GetDecimal();
        var designPath = $"/api/v1/terminals/{terminalId:D}/billing/bills/{billId:D}/split-design";
        var design = await ProbeHarness.JsonAsync(await _h.GetAsync(designPath, cookie));
        var designPayable = design.GetProperty("payableAmount").GetDecimal();

        var half = Math.Round(designPayable / 2m, 2);
        var save = await _h.PutAsync(designPath + "/amounts", cookie, new
        {
            ExpectedBillRowVersion = design.GetProperty("billRowVersion").GetInt64(),
            ExpectedAllocations = Array.Empty<object>(),
            Targets = new object[]
            {
                new { Owner = new { Kind = "Person", OwnerId = Guid.NewGuid() }, Amount = half },
                new { Owner = new { Kind = "Person", OwnerId = Guid.NewGuid() }, Amount = designPayable - half },
            },
        });

        Assert.True(designPayable == tenderPayable && save.StatusCode == HttpStatusCode.OK,
            $"Split design shows payable {designPayable:0.00}, tender path enforces {tenderPayable:0.00}; " +
            $"saving a ByAmount split that sums to the displayed payable returned {(int)save.StatusCode}.");
    }
}
