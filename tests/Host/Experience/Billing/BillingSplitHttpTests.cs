using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ALKAROS.Audit.EventStore;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Experience.Billing;
using ALKAROS.Host.Experience.Orders;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Orders.OrderAggregate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.Billing.Tests;

[Collection("Billing split PostgreSQL HTTP")]
public sealed class BillingSplitHttpTests : IAsyncLifetime
{
    private readonly BillingSplitTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task EndpointsEnforceSessionPermissionAndAtomicConcurrency()
    {
        var terminalId = Guid.NewGuid();
        var mutableCookie = await _database.SeedSessionAsync(terminalId, canMutate: true);
        var readOnlyCookie = await _database.SeedSessionAsync(terminalId, canMutate: false);
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var path = Path(terminalId, seeded.BillId);

        using (var missing = await client.GetAsync(path))
            Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);

        using (var readableRequest = Request(HttpMethod.Get, path, readOnlyCookie))
        using (var readable = await client.SendAsync(readableRequest))
        {
            Assert.Equal(HttpStatusCode.OK, readable.StatusCode);
            Assert.Empty((await readable.Content.ReadFromJsonAsync<BillSplitDesignDto>())!.AllowedCommands);
        }

        // Owner options come from the server (table seats), not client-fabricated
        // GUIDs (deep-analysis finding F-2).
        using (var ownersRequest = Request(HttpMethod.Get, path + "/owners", readOnlyCookie))
        using (var owners = await client.SendAsync(ownersRequest))
        {
            Assert.Equal(HttpStatusCode.OK, owners.StatusCode);
            var options = await owners.Content.ReadFromJsonAsync<IReadOnlyList<BillSplitOwnerOptionDto>>();
            Assert.Contains(options!, option => option.Kind == "Seat" && option.Id == seeded.SeatId);
        }

        var equalRequest = new SaveEqualSplitRequest(
            seeded.BillRowVersion,
            [],
            [new SplitOwnerRequest("Seat", seeded.SeatId), new SplitOwnerRequest("Person", Guid.NewGuid())]);
        using (var deniedRequest = JsonRequest(HttpMethod.Put, path + "/equal", readOnlyCookie, equalRequest))
        using (var denied = await client.SendAsync(deniedRequest))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var equal = await PutAsync<BillSplitDesignDto>(client, path + "/equal", mutableCookie, equalRequest);
        Assert.Equal("EqualByPerson", equal.Mode);
        Assert.Equal(equal.PayableAmount, equal.Allocations.Sum(allocation => allocation.Amount));
        Assert.Equal(equal.TaxTotal, equal.Allocations.Sum(allocation => allocation.TaxAmount));
        Assert.Contains(equal.Allocations, allocation => allocation.OwnerId == seeded.SeatId);

        var wrongVersions = Versions(equal);
        wrongVersions[0] = wrongVersions[0] with { RowVersion = wrongVersions[0].RowVersion + 1 };
        using (var staleAllocationRequest = JsonRequest(
            HttpMethod.Put,
            path + "/equal",
            mutableCookie,
            equalRequest with
            {
                ExpectedBillRowVersion = equal.BillRowVersion,
                ExpectedAllocations = wrongVersions,
            }))
        using (var staleAllocation = await client.SendAsync(staleAllocationRequest))
        {
            Assert.Equal(HttpStatusCode.Conflict, staleAllocation.StatusCode);
            var error = await staleAllocation.Content.ReadFromJsonAsync<BillingSplitErrorEnvelope>();
            Assert.Equal("allocation", error!.Error.Conflict!.Resource);
        }

        using (var staleRequest = JsonRequest(HttpMethod.Put, path + "/equal", mutableCookie, equalRequest))
        using (var stale = await client.SendAsync(staleRequest))
        {
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            var error = await stale.Content.ReadFromJsonAsync<BillingSplitErrorEnvelope>();
            Assert.Equal("CONCURRENT_MODIFICATION", error!.Error.Code);
            Assert.NotNull(error.Error.Conflict);
        }
        Assert.Equal(
            equal.Allocations.Select(allocation => allocation.AllocationId),
            await _database.AllocationIdsAsync(seeded.BillId));
    }

    [Fact]
    public async Task MalformedJsonBodyReturns400WithoutReachingTheEndpointFilter()
    {
        // An independent audit (2026-09-06) claimed BadHttpRequestException
        // fell through this module's Map() to 500 INTERNAL_ERROR. Verified
        // false: ASP.NET Core's minimal-API JSON body binder catches a parse
        // failure and writes its own empty-bodied 400 before the request
        // delegate (and so this module's IEndpointFilter) ever runs — proven
        // by temporarily reverting the BadHttpRequestException switch arm in
        // BillingSplitApplication.Map() and confirming this test's outcome
        // was unchanged. The switch arm stays for defensive consistency with
        // the same case already present in Catalog/Kitchen/Roles/Authorization,
        // not because this scenario reaches it.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedSessionAsync(terminalId, canMutate: true);
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var request = Request(HttpMethod.Put, Path(terminalId, seeded.BillId) + "/equal", cookie);
        request.Content = new StringContent("{ not valid json", Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AllModesPersistRejectOverflowSurviveRestartAndClear()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedSessionAsync(terminalId, canMutate: true);
        var seeded = await _database.SeedBillAsync();
        var path = Path(terminalId, seeded.BillId);
        BillSplitDesignDto current;

        await using (var app = await StartAsync())
        using (var client = CreateClient(app))
        {
            current = await PutAsync<BillSplitDesignDto>(
                client,
                path + "/items",
                cookie,
                new SaveItemSplitRequest(
                    seeded.BillRowVersion,
                    [],
                    [
                        new ItemSplitTargetRequest(new SplitOwnerRequest("Seat", seeded.SeatId), seeded.FirstItemId, 1m),
                        new ItemSplitTargetRequest(new SplitOwnerRequest("Person", Guid.NewGuid()), seeded.FirstItemId, 1m),
                        new ItemSplitTargetRequest(new SplitOwnerRequest("Seat", seeded.SeatId), seeded.SecondItemId, 1m),
                    ]));
            Assert.Equal("ByItem", current.Mode);

            var expected = Versions(current);
            using (var overflowRequest = JsonRequest(
                HttpMethod.Put,
                path + "/items",
                cookie,
                new SaveItemSplitRequest(
                    current.BillRowVersion,
                    expected,
                    [
                        new ItemSplitTargetRequest(new SplitOwnerRequest("Seat", seeded.SeatId), seeded.FirstItemId, 2m),
                        new ItemSplitTargetRequest(new SplitOwnerRequest("Person", Guid.NewGuid()), seeded.FirstItemId, 1m),
                    ])))
            using (var overflow = await client.SendAsync(overflowRequest))
                Assert.Equal(HttpStatusCode.BadRequest, overflow.StatusCode);
            Assert.Equal(current.Allocations.Select(allocation => allocation.AllocationId), await _database.AllocationIdsAsync(seeded.BillId));

            current = await PutAsync<BillSplitDesignDto>(
                client,
                path + "/amounts",
                cookie,
                new SaveAmountSplitRequest(
                    current.BillRowVersion,
                    Versions(current),
                    [
                        new AmountSplitTargetRequest(new SplitOwnerRequest("Seat", seeded.SeatId), 100m),
                        new AmountSplitTargetRequest(new SplitOwnerRequest("Person", Guid.NewGuid()), 175m),
                    ]));
            Assert.Equal("ByAmount", current.Mode);

            current = await PutAsync<BillSplitDesignDto>(
                client,
                path + "/custom",
                cookie,
                new SaveCustomSplitRequest(
                    current.BillRowVersion,
                    Versions(current),
                    [
                        new CustomSplitTargetRequest(new SplitOwnerRequest("Seat", seeded.SeatId), 120m, null, null),
                        new CustomSplitTargetRequest(new SplitOwnerRequest("Person", Guid.NewGuid()), 155m, null, null),
                    ]));
            Assert.Equal("Custom", current.Mode);
        }

        await using (var restarted = await StartAsync())
        using (var client = CreateClient(restarted))
        {
            using var getRequest = Request(HttpMethod.Get, path, cookie);
            using var get = await client.SendAsync(getRequest);
            Assert.Equal(HttpStatusCode.OK, get.StatusCode);
            current = (await get.Content.ReadFromJsonAsync<BillSplitDesignDto>())!;
            Assert.Equal("Custom", current.Mode);
            Assert.Equal(2, current.Allocations.Count);

            var cleared = await PostAsync<BillSplitDesignDto>(
                client,
                path + "/clear",
                cookie,
                new ClearSplitDesignRequest(current.BillRowVersion, Versions(current)));
            Assert.Equal("None", cleared.Mode);
            Assert.Empty(cleared.Allocations);

            await _database.SetBillStatusAsync(seeded.BillId, "Paid");
            using var paidRequest = JsonRequest(
                HttpMethod.Put,
                path + "/equal",
                cookie,
                new SaveEqualSplitRequest(
                    cleared.BillRowVersion,
                    [],
                    [new SplitOwnerRequest("Seat", seeded.SeatId), new SplitOwnerRequest("Person", Guid.NewGuid())]));
            using var paid = await client.SendAsync(paidRequest);
            Assert.Equal(HttpStatusCode.Conflict, paid.StatusCode);
            Assert.Equal(
                "UNSUPPORTED_BILL_STATE",
                (await paid.Content.ReadFromJsonAsync<BillingSplitErrorEnvelope>())!.Error.Code);
        }
    }

    [Fact]
    public async Task ConcurrentBillCreationForTheSameOrderHasOneWinnerAndNoDuplicateBill()
    {
        // Regression test for an independent audit finding (2026-09-05, H1):
        // CreateBillFromOrderAsync used to catch (Exception) around the bill
        // insert, swallowing any failure (not just the expected concurrent
        // unique-violation) and handing back whatever bill already existed
        // as if the caller's own request had succeeded. The catch is now
        // narrowed to the specific bills_bill_number_key race; this proves
        // the narrowed catch still recovers correctly and that exactly one
        // bill row is created when two requests race for the same order.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedSessionAsync(terminalId, canMutate: true);
        var orderId = await _database.SeedOrderWithoutBillAsync();
        await using var app = await StartAsync();
        using var firstClient = CreateClient(app);
        using var secondClient = CreateClient(app);
        var path = $"/api/v1/terminals/{terminalId:D}/billing/bills/from-order/{orderId:D}";

        var responses = await Task.WhenAll(
            firstClient.SendAsync(Request(HttpMethod.Post, path, cookie)),
            secondClient.SendAsync(Request(HttpMethod.Post, path, cookie)));
        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
            var bills = await Task.WhenAll(responses.Select(response => response.Content.ReadFromJsonAsync<BillSplitDesignDto>()));
            Assert.Equal(bills[0]!.BillId, bills[1]!.BillId);
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }

        Assert.Equal(1L, await _database.BillCountForOrderAsync(orderId));
    }

    /// <summary>
    /// V1-RMD-103 (B1): `AdjustmentCalculator`/`BillAdjustment`/
    /// `IBillAdjustmentRepository` (V1-BIL-003) were domain-complete and
    /// unit-tested but had zero DI registration, zero callers, and no HTTP
    /// endpoint — a discount could not be entered anywhere, found by an
    /// independent audit (2026-09-05). This is the first real caller,
    /// mirroring V1-BIL-005's bills.comp pattern for the sibling
    /// bills.discount grant-class permission.
    /// </summary>
    [Fact]
    public async Task ARoleThatHoldsBillsDiscountAppliesDirectly()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "supervisor", "bills.discount");
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var request = JsonRequest(
            HttpMethod.Post,
            $"/api/v1/terminals/{terminalId:D}/billing/bills/{seeded.BillId:D}/discount",
            cookie,
            new ApplyBillDiscountRequestV1(Guid.NewGuid().ToString(), "Percentage", 10m, "PromotionalOffer"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApplyBillDiscountResultV1>();
        Assert.Equal("Applied", body!.Status);
        Assert.NotNull(body.AdjustmentId);
        Assert.NotNull(body.Summary);
        Assert.Equal(
            body.Summary!.OriginalPayableAmount - body.Summary.TotalDiscounts,
            body.Summary.AdjustedPayableAmount);
        Assert.True(body.Summary.TotalDiscounts > 0);

        using var adjustmentsResponse = await client.SendAsync(Request(
            HttpMethod.Get, $"/api/v1/terminals/{terminalId:D}/billing/bills/{seeded.BillId:D}/adjustments", cookie));
        Assert.Equal(HttpStatusCode.OK, adjustmentsResponse.StatusCode);
        using var adjustmentsDoc = JsonDocument.Parse(await adjustmentsResponse.Content.ReadAsStringAsync());
        Assert.Equal(1, adjustmentsDoc.RootElement.GetProperty("adjustments").GetArrayLength());
        Assert.Equal(
            body.Summary.AdjustedPayableAmount,
            adjustmentsDoc.RootElement.GetProperty("summary").GetProperty("adjustedPayableAmount").GetDecimal());
    }

    [Fact]
    public async Task ADiscountThatWouldUndercutWhatWasAlreadyCollectedIsRefused()
    {
        // V1-RMD-410 (V1-RMD-393 F-05): 250 of a 275 bill already collected; a 50 discount would leave 225 payable.
        var terminalId = Guid.NewGuid();
        var (userId, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "supervisor", "bills.discount");
        var seeded = await _database.SeedBillAsync();
        await _database.SeedCollectedAsync(seeded.BillId, 250m, userId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var path = $"/api/v1/terminals/{terminalId:D}/billing/bills/{seeded.BillId:D}/discount";

        using var tooMuch = await client.SendAsync(JsonRequest(HttpMethod.Post, path, cookie,
            new ApplyBillDiscountRequestV1(Guid.NewGuid().ToString(), "FixedAmount", 50m, "PromotionalOffer")));
        Assert.Equal(HttpStatusCode.Conflict, tooMuch.StatusCode);
        using (var error = JsonDocument.Parse(await tooMuch.Content.ReadAsStringAsync()))
            Assert.Equal("DISCOUNT_BELOW_COLLECTED", error.RootElement.GetProperty("error").GetProperty("code").GetString());

        using var withinRemaining = await client.SendAsync(JsonRequest(HttpMethod.Post, path, cookie,
            new ApplyBillDiscountRequestV1(Guid.NewGuid().ToString(), "FixedAmount", 20m, "PromotionalOffer")));
        Assert.Equal(HttpStatusCode.OK, withinRemaining.StatusCode);
        var body = await withinRemaining.Content.ReadFromJsonAsync<ApplyBillDiscountResultV1>();
        Assert.Equal(255m, body!.Summary!.AdjustedPayableAmount);

        using var adjustments = await client.SendAsync(Request(
            HttpMethod.Get, $"/api/v1/terminals/{terminalId:D}/billing/bills/{seeded.BillId:D}/adjustments", cookie));
        using var adjustmentsDoc = JsonDocument.Parse(await adjustments.Content.ReadAsStringAsync());
        Assert.Equal(1, adjustmentsDoc.RootElement.GetProperty("adjustments").GetArrayLength());
    }

    /// <summary>
    /// V1-RMD-237: IAuditEventStore existed since V1-OPS-001 with zero real
    /// callers anywhere in the codebase — a discounted bill left no audit
    /// trail at all despite the read-side endpoints
    /// (KitchenOperationsEndpoints' /audit/aggregate, /audit/correlation)
    /// already existing and being correctly permission-gated. This is the
    /// first real write.
    /// </summary>
    [Fact]
    public async Task ApplyingADiscountWritesARealAuditEvent()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "supervisor", "bills.discount");
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var request = JsonRequest(
            HttpMethod.Post,
            $"/api/v1/terminals/{terminalId:D}/billing/bills/{seeded.BillId:D}/discount",
            cookie,
            new ApplyBillDiscountRequestV1(Guid.NewGuid().ToString(), "Percentage", 10m, "PromotionalOffer"));
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var auditDataSource = NpgsqlDataSource.Create(_database.ConnectionString);
        var auditEvents = new PostgresAuditEventStore(auditDataSource);
        var events = await auditEvents.GetByAggregateAsync("Bill", seeded.BillId);

        var applied = Assert.Single(events);
        Assert.Equal("bill.discount.applied", applied.EventName);
        Assert.Equal("Bill", applied.AggregateType);
        Assert.Equal(seeded.BillId, applied.AggregateId);
        Assert.Equal("User", applied.ActorType);
        Assert.NotNull(applied.ActorId);
        Assert.Equal("PromotionalOffer", applied.Reason);
        Assert.NotNull(applied.BeforeStateJson);
        Assert.NotNull(applied.AfterStateJson);
    }

    [Fact]
    public async Task RetryingADiscountWithTheSameIdempotencyKeyDoesNotDuplicateTheAdjustment()
    {
        // V1-RMD-112 (independent audit, 2026-09-06): unlike /comp and
        // /void-sent (protected "by accident" by their own
        // ExpectedRowVersion), ApplyBillDiscountRequestV1 carries no row
        // version and a fresh Guid.NewGuid() was used for every adjustment
        // id regardless of the caller's own idempotency key — a network
        // retry of an identical request appended a second discount line.
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "supervisor", "bills.discount");
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var body = new ApplyBillDiscountRequestV1(Guid.NewGuid().ToString(), "Percentage", 10m, "PromotionalOffer");

        using var first = await client.SendAsync(JsonRequest(
            HttpMethod.Post, $"/api/v1/terminals/{terminalId:D}/billing/bills/{seeded.BillId:D}/discount", cookie, body));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstResult = await first.Content.ReadFromJsonAsync<ApplyBillDiscountResultV1>();

        using var retry = await client.SendAsync(JsonRequest(
            HttpMethod.Post, $"/api/v1/terminals/{terminalId:D}/billing/bills/{seeded.BillId:D}/discount", cookie, body));
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var retryResult = await retry.Content.ReadFromJsonAsync<ApplyBillDiscountResultV1>();

        Assert.Equal("Applied", retryResult!.Status);
        Assert.Equal(firstResult!.AdjustmentId, retryResult.AdjustmentId);
        Assert.Equal(1L, await _database.BillAdjustmentCountAsync(seeded.BillId));
    }

    /// <summary>V1-WTR-020: bills.split (already held outright by cashier) records a tip directly, no grant escalation.</summary>
    [Fact]
    public async Task ARoleThatHoldsBillsSplitRecordsATipDirectly()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "bills.split");
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var request = JsonRequest(
            HttpMethod.Post,
            $"/api/v1/terminals/{terminalId:D}/billing/bills/{seeded.BillId:D}/tip",
            cookie,
            new ApplyBillTipRequestV1(Guid.NewGuid().ToString(), 50m));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApplyBillTipResultV1>();
        Assert.NotEqual(Guid.Empty, body!.AdjustmentId);
        Assert.Equal(50m, body.Summary.TotalTips);
        Assert.Equal(
            body.Summary.OriginalPayableAmount + body.Summary.TotalTips,
            body.Summary.AdjustedPayableAmount);
    }

    [Fact]
    public async Task RetryingATipWithTheSameIdempotencyKeyDoesNotDuplicateTheAdjustment()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "bills.split");
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var body = new ApplyBillTipRequestV1(Guid.NewGuid().ToString(), 25m);

        using var first = await client.SendAsync(JsonRequest(
            HttpMethod.Post, $"/api/v1/terminals/{terminalId:D}/billing/bills/{seeded.BillId:D}/tip", cookie, body));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstResult = await first.Content.ReadFromJsonAsync<ApplyBillTipResultV1>();

        using var retry = await client.SendAsync(JsonRequest(
            HttpMethod.Post, $"/api/v1/terminals/{terminalId:D}/billing/bills/{seeded.BillId:D}/tip", cookie, body));
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var retryResult = await retry.Content.ReadFromJsonAsync<ApplyBillTipResultV1>();

        Assert.Equal(firstResult!.AdjustmentId, retryResult!.AdjustmentId);
        Assert.Equal(1L, await _database.BillAdjustmentCountAsync(seeded.BillId));
    }

    [Fact]
    public async Task ARoleWithoutBillsSplitIsRejectedFromRecordingATip()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "waiter");
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            HttpMethod.Post,
            $"/api/v1/terminals/{terminalId:D}/billing/bills/{seeded.BillId:D}/tip",
            cookie,
            new ApplyBillTipRequestV1(Guid.NewGuid().ToString(), 20m)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// V1-WTR-021: end-to-end across both endpoints this session added -
    /// a tip recorded through V1-WTR-020's endpoint shows up in the
    /// waiter's own shift summary the same UTC day, with the single waiter
    /// who served today taking the whole equal-pool share.
    /// </summary>
    [Fact]
    public async Task AVoluntaryTipTodayAppearsInTheServingWaitersShiftSummary()
    {
        var terminalId = Guid.NewGuid();
        var (waiterId, waiterCookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "waiter");
        var seeded = await _database.SeedBillAsync();
        await _database.SetOrderServingUserAsync(seeded.BillId, waiterId);
        var (_, cashierCookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "bills.split");
        await using var app = await StartAsyncWithOrders();
        using var client = CreateClient(app);

        using var tipResponse = await client.SendAsync(JsonRequest(
            HttpMethod.Post,
            $"/api/v1/terminals/{terminalId:D}/billing/bills/{seeded.BillId:D}/tip",
            cashierCookie,
            new ApplyBillTipRequestV1(Guid.NewGuid().ToString(), 40m)));
        Assert.Equal(HttpStatusCode.OK, tipResponse.StatusCode);

        using var summaryResponse = await client.SendAsync(
            Request(HttpMethod.Get, $"/api/v1/terminals/{terminalId:D}/orders/my-shift-summary", waiterCookie));

        Assert.Equal(HttpStatusCode.OK, summaryResponse.StatusCode);
        var summary = await summaryResponse.Content.ReadFromJsonAsync<MyShiftSummaryV1>();
        Assert.True(summary!.SalesTotal > 0m);
        Assert.Equal(0m, summary.CompUsed);
        Assert.Equal(40m, summary.TipPoolTotal);
        Assert.Equal(1, summary.WaitersWorkedToday);
        Assert.Equal(40m, summary.TipPoolShare);
    }

    /// <summary>
    /// Found in an independent review (2026-09-11): the "how many waiters
    /// worked today" divisor counted every distinct serving_user_id
    /// regardless of order status, while the sales-total query (same
    /// response) excludes Cancelled orders - a waiter whose only order today
    /// was cancelled diluted every other waiter's tip-pool share despite not
    /// counting as "worked" anywhere else in this same response.
    /// </summary>
    [Fact]
    public async Task AWaiterWhoseOnlyOrderTodayWasCancelledDoesNotDiluteTheTipPool()
    {
        var terminalId = Guid.NewGuid();
        var (activeWaiterId, activeWaiterCookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "waiter");
        var activeBill = await _database.SeedBillAsync();
        await _database.SetOrderServingUserAsync(activeBill.BillId, activeWaiterId);

        var (cancelledWaiterId, _) = await _database.SeedSessionWithPermissionsAsync(Guid.NewGuid(), "waiter");
        var cancelledBill = await _database.SeedBillAsync();
        await _database.SetOrderServingUserAsync(cancelledBill.BillId, cancelledWaiterId);
        await _database.SetOrderStatusAsync(cancelledBill.BillId, "Cancelled");

        var (_, cashierCookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "bills.split");
        await using var app = await StartAsyncWithOrders();
        using var client = CreateClient(app);

        using var tipResponse = await client.SendAsync(JsonRequest(
            HttpMethod.Post,
            $"/api/v1/terminals/{terminalId:D}/billing/bills/{activeBill.BillId:D}/tip",
            cashierCookie,
            new ApplyBillTipRequestV1(Guid.NewGuid().ToString(), 40m)));
        Assert.Equal(HttpStatusCode.OK, tipResponse.StatusCode);

        using var summaryResponse = await client.SendAsync(
            Request(HttpMethod.Get, $"/api/v1/terminals/{terminalId:D}/orders/my-shift-summary", activeWaiterCookie));
        Assert.Equal(HttpStatusCode.OK, summaryResponse.StatusCode);
        var summary = await summaryResponse.Content.ReadFromJsonAsync<MyShiftSummaryV1>();

        // Only the active waiter counts - the cancelled-order waiter must
        // not appear in the divisor, so the full 40 TRY goes to the one
        // waiter who actually worked today, not split into 20/20.
        Assert.Equal(1, summary!.WaitersWorkedToday);
        Assert.Equal(40m, summary.TipPoolShare);
    }

    [Fact]
    public async Task ARoleWithoutBillsDiscountAndNoPolicyOrDelegationEscalatesToPending()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "waiter");
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var request = JsonRequest(
            HttpMethod.Post,
            $"/api/v1/terminals/{terminalId:D}/billing/bills/{seeded.BillId:D}/discount",
            cookie,
            new ApplyBillDiscountRequestV1(Guid.NewGuid().ToString(), "Percentage", 10m, "PromotionalOffer"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApplyBillDiscountResultV1>();
        Assert.Equal("Pending", body!.Status);
        Assert.NotNull(body.GrantId);
        Assert.Equal(0L, await _database.BillAdjustmentCountAsync(seeded.BillId));
    }

    [Fact]
    public async Task ResendingAnApprovedIdempotencyKeyWithATamperedAmountIsRejectedNotApplied()
    {
        // Found by an independent audit (2026-09-07): AuthorizationGrantService.
        // MatchesReplay compared permission/subject/requester only. A manager
        // approves the requested 10% here, but if the client (or an attacker
        // with the same key) resent the identical idempotency key carrying a
        // different Value, the replay lookup matched anyway and the endpoint
        // applied the *new*, never-reviewed amount below — not the 10% a
        // manager actually approved. It must now be refused outright.
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "waiter");
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var idempotencyKey = Guid.NewGuid().ToString();
        var path = $"/api/v1/terminals/{terminalId:D}/billing/bills/{seeded.BillId:D}/discount";

        using var pendingResponse = await client.SendAsync(JsonRequest(
            HttpMethod.Post, path, cookie,
            new ApplyBillDiscountRequestV1(idempotencyKey, "Percentage", 10m, "PromotionalOffer")));
        var pendingBody = await pendingResponse.Content.ReadFromJsonAsync<ApplyBillDiscountResultV1>();
        Assert.Equal("Pending", pendingBody!.Status);
        await _database.ApproveGrantAsync(pendingBody.GrantId!.Value);

        using var tamperedResponse = await client.SendAsync(JsonRequest(
            HttpMethod.Post, path, cookie,
            new ApplyBillDiscountRequestV1(idempotencyKey, "Percentage", 95m, "PromotionalOffer")));

        Assert.Equal(HttpStatusCode.Conflict, tamperedResponse.StatusCode);
        var tamperedError = await tamperedResponse.Content.ReadFromJsonAsync<BillingSplitErrorEnvelope>();
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", tamperedError!.Error.Code);
        Assert.Equal(0L, await _database.BillAdjustmentCountAsync(seeded.BillId));

        using var genuineRetry = await client.SendAsync(JsonRequest(
            HttpMethod.Post, path, cookie,
            new ApplyBillDiscountRequestV1(idempotencyKey, "Percentage", 10m, "PromotionalOffer")));

        Assert.Equal(HttpStatusCode.OK, genuineRetry.StatusCode);
        var genuineBody = await genuineRetry.Content.ReadFromJsonAsync<ApplyBillDiscountResultV1>();
        Assert.Equal("Applied", genuineBody!.Status);
        Assert.Equal(1L, await _database.BillAdjustmentCountAsync(seeded.BillId));
    }

    [Fact]
    public async Task AnInvalidDiscountReasonCodeIsRejected()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "supervisor", "bills.discount");
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var request = JsonRequest(
            HttpMethod.Post,
            $"/api/v1/terminals/{terminalId:D}/billing/bills/{seeded.BillId:D}/discount",
            cookie,
            new ApplyBillDiscountRequestV1(Guid.NewGuid().ToString(), "Percentage", 10m, "NotARealReason"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0L, await _database.BillAdjustmentCountAsync(seeded.BillId));
    }

    [Fact]
    public async Task ADiscountExceedingThePayableAmountIsRejectedAndNotPersisted()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "supervisor", "bills.discount");
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var request = JsonRequest(
            HttpMethod.Post,
            $"/api/v1/terminals/{terminalId:D}/billing/bills/{seeded.BillId:D}/discount",
            cookie,
            new ApplyBillDiscountRequestV1(Guid.NewGuid().ToString(), "FixedAmount", 100000m, "PromotionalOffer"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0L, await _database.BillAdjustmentCountAsync(seeded.BillId));
    }

    [Fact]
    public async Task ADiscountOnAPaidBillIsRejected()
    {
        // Regression test for an independent audit finding (2026-09-06):
        // ApplyDiscountAsync never checked bill.Status, so money already
        // collected could be discounted after the fact.
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "supervisor", "bills.discount");
        var seeded = await _database.SeedBillAsync();
        await _database.SetBillStatusAsync(seeded.BillId, "Paid");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var request = JsonRequest(
            HttpMethod.Post,
            $"/api/v1/terminals/{terminalId:D}/billing/bills/{seeded.BillId:D}/discount",
            cookie,
            new ApplyBillDiscountRequestV1(Guid.NewGuid().ToString(), "Percentage", 10m, "PromotionalOffer"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            "UNSUPPORTED_BILL_STATE",
            (await response.Content.ReadFromJsonAsync<BillingSplitErrorEnvelope>())!.Error.Code);
        Assert.Equal(0L, await _database.BillAdjustmentCountAsync(seeded.BillId));
    }

    [Fact]
    public async Task ConcurrentDiscountsOnTheSameBillDoNotExceedThePayableAmount()
    {
        // Regression test for an independent audit finding (2026-09-06): a
        // bare read-validate-write let two concurrent discount requests both
        // read the same (empty) adjustment set and both pass validation,
        // letting total discounts exceed the payable amount. Two requests
        // each individually valid (60% + 60%) but jointly invalid (120%)
        // must yield exactly one success and one rejection.
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "supervisor", "bills.discount");
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var firstClient = CreateClient(app);
        using var secondClient = CreateClient(app);
        var path = $"/api/v1/terminals/{terminalId:D}/billing/bills/{seeded.BillId:D}/discount";

        var responses = await Task.WhenAll(
            firstClient.SendAsync(JsonRequest(HttpMethod.Post, path, cookie,
                new ApplyBillDiscountRequestV1(Guid.NewGuid().ToString(), "Percentage", 60m, "PromotionalOffer"))),
            secondClient.SendAsync(JsonRequest(HttpMethod.Post, path, cookie,
                new ApplyBillDiscountRequestV1(Guid.NewGuid().ToString(), "Percentage", 60m, "PromotionalOffer"))));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.BadRequest);
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }

        Assert.Equal(1L, await _database.BillAdjustmentCountAsync(seeded.BillId));
    }

    private async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(NpgsqlDataSource.Create(_database.ConnectionString));
        builder.Services.AddBillingSplitExperience();
        var app = builder.Build();
        app.MapBillingSplitApi();
        await app.StartAsync();
        return app;
    }

    /// <summary>
    /// V1-WTR-021: a second, test-local host composition that also maps
    /// OrderManagementApi (my-shift-summary reads across orders, identity
    /// and billing schemas — this project's own full-migration-directory
    /// fixture already has all three, unlike a per-file fixture-list
    /// project, so this is a self-contained addition here rather than a
    /// new test project). Deliberately separate from the shared
    /// <see cref="StartAsync"/> above so this does not touch the other 19
    /// tests that already depend on it.
    /// </summary>
    private async Task<WebApplication> StartAsyncWithOrders()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(NpgsqlDataSource.Create(_database.ConnectionString));
        builder.Services.AddBillingSplitExperience();
        builder.Services.AddOrderManagementExperience();
        var app = builder.Build();
        app.MapBillingSplitApi();
        app.MapOrderManagementApi();
        await app.StartAsync();
        return app;
    }

    private static List<AllocationVersionRequest> Versions(BillSplitDesignDto design)
        => design.Allocations.Select(allocation => new AllocationVersionRequest(allocation.AllocationId, allocation.RowVersion)).ToList();

    private static async Task<T> PutAsync<T>(HttpClient client, string path, string cookie, object body)
    {
        using var request = JsonRequest(HttpMethod.Put, path, cookie, body);
        using var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string path, string cookie, object body)
    {
        using var request = JsonRequest(HttpMethod.Post, path, cookie, body);
        using var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, string cookie)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return request;
    }

    private static HttpRequestMessage JsonRequest(HttpMethod method, string path, string cookie, object body)
    {
        var request = Request(method, path, cookie);
        request.Content = JsonContent.Create(body);
        return request;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient { BaseAddress = new Uri(address) };
    }

    private static string Path(Guid terminalId, Guid billId)
        => $"/api/v1/terminals/{terminalId:D}/billing/bills/{billId:D}/split-design";
}

public sealed class BillingSplitRegistrationTests
{
    [Fact]
    public void RegistrationPublishesVersionedDesignOnlyRoutes()
    {
        using var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=not_opened");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.Services.AddSingleton(dataSource);
        builder.Services.AddBillingSplitExperience();
        using var app = builder.Build();
        app.MapBillingSplitApi();

        Assert.IsType<PostgresBillRepository>(app.Services.GetRequiredService<IBillRepository>());
        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => new
            {
                Route = endpoint.RoutePattern.RawText,
                Methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [],
            })
            .ToList();
        var prefix = "/api/v1/terminals/{terminalId:guid}/billing/bills/{billId:guid}/split-design";
        AssertRoute(routes, "GET", prefix);
        AssertRoute(routes, "PUT", prefix + "/equal");
        AssertRoute(routes, "PUT", prefix + "/items");
        AssertRoute(routes, "PUT", prefix + "/amounts");
        AssertRoute(routes, "PUT", prefix + "/custom");
        AssertRoute(routes, "POST", prefix + "/clear");
    }

    private static void AssertRoute(IEnumerable<dynamic> routes, string method, string pattern)
    {
        Assert.Contains(routes, route =>
            string.Equals(((string?)route.Route)?.TrimEnd('/'), pattern.TrimEnd('/'), StringComparison.Ordinal)
            && ((IReadOnlyList<string>)route.Methods).Contains(method, StringComparer.Ordinal));
    }
}

[CollectionDefinition("Billing split PostgreSQL HTTP", DisableParallelization = true)]
public sealed class BillingSplitPostgresqlDefinition;

internal sealed record SeededBill(
    Guid BillId,
    long BillRowVersion,
    Guid SeatId,
    Guid FirstItemId,
    Guid SecondItemId);

internal sealed class BillingSplitTestDatabase
{
    private readonly string _databaseName = "alkaros_rmd027_" + Guid.NewGuid().ToString("N")[..8];
    private NpgsqlDataSource? _dataSource;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var maintenanceConnection = new NpgsqlConnectionStringBuilder
        {
            Host = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_HOST") ?? "localhost",
            Port = int.TryParse(Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_PORT"), out var port) ? port : 5432,
            Username = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_USER") ?? "postgres",
            Password = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_PASSWORD"),
            Database = "postgres",
        }.ConnectionString;
        await using (var maintenance = NpgsqlDataSource.Create(maintenanceConnection))
        {
            await ExecuteAsync(maintenance, $"DROP DATABASE IF EXISTS {_databaseName} WITH (FORCE);");
            await ExecuteAsync(maintenance, $"CREATE DATABASE {_databaseName};");
        }

        ConnectionString = new NpgsqlConnectionStringBuilder(maintenanceConnection) { Database = _databaseName }.ConnectionString;
        _dataSource = NpgsqlDataSource.Create(ConnectionString);
        await ApplyMigrationsAsync();
    }

    public async Task DisposeAsync()
    {
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
            _dataSource = null;
        }
        if (string.IsNullOrWhiteSpace(ConnectionString))
            return;
        var maintenanceConnection = new NpgsqlConnectionStringBuilder(ConnectionString) { Database = "postgres" }.ConnectionString;
        await using var maintenance = NpgsqlDataSource.Create(maintenanceConnection);
        await ExecuteAsync(maintenance, $"DROP DATABASE IF EXISTS {_databaseName} WITH (FORCE);");
    }

    public async Task<string> SeedSessionAsync(Guid terminalId, bool canMutate)
    {
        var userId = Guid.NewGuid();
        var suffix = userId.ToString("N");
        var (raw, hash) = DeviceSessionToken.Create();
        await ExecuteAsync(
            DataSource,
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'not-used', 'Billing API Test', true);
            INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES (@session_id, @user_id, @device_id, @token_hash, now(), now() + interval '1 hour');
            """,
            ("user_id", userId),
            ("username", "billing-api-" + suffix),
            ("session_id", Guid.NewGuid()),
            ("device_id", $"cashier:{terminalId:D}"),
            ("token_hash", hash));

        if (canMutate)
        {
            var roleId = Guid.NewGuid();
            await ExecuteAsync(
                DataSource,
                """
                INSERT INTO identity.permissions (permission_id, code, name)
                VALUES (@permission_id, 'bills.split', 'Operational bill splitting') ON CONFLICT (code) DO NOTHING;
                INSERT INTO identity.roles (role_id, code, name) VALUES (@role_id, @role_code, 'Billing API Test Role');
                INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
                SELECT @role_permission_id, @role_id, permission_id FROM identity.permissions WHERE code = 'bills.split';
                INSERT INTO identity.user_roles (user_role_id, user_id, role_id)
                VALUES (@user_role_id, @user_id, @role_id);
                """,
                ("permission_id", Guid.NewGuid()),
                ("role_id", roleId),
                ("role_code", "billing-api-role-" + suffix),
                ("role_permission_id", Guid.NewGuid()),
                ("user_role_id", Guid.NewGuid()),
                ("user_id", userId));
        }

        return $"{DualScreenApplication.CashierCookieName}={raw}";
    }

    /// <summary>Seeds a cashier device session under the given role, with the given permission codes granted outright (mirrors OrderManagementCompTestDatabase's helper for V1-BIL-005).</summary>
    public async Task<(Guid UserId, string Cookie)> SeedSessionWithPermissionsAsync(
        Guid terminalId, string roleCode, params string[] permissionCodes)
    {
        var userId = Guid.NewGuid();
        var suffix = userId.ToString("N");
        var (raw, hash) = DeviceSessionToken.Create();

        await ExecuteAsync(
            DataSource,
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'not-used', 'Billing API Test', true);
            INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES (@session_id, @user_id, @device_id, @token_hash, now(), now() + interval '1 hour');
            """,
            ("user_id", userId),
            ("username", "rmd103-api-" + suffix),
            ("session_id", Guid.NewGuid()),
            ("device_id", $"cashier:{terminalId:D}"),
            ("token_hash", hash));

        var roleId = Guid.NewGuid();
        await ExecuteAsync(
            DataSource,
            "INSERT INTO identity.roles (role_id, code, name) VALUES (@role_id, @role_code, 'Billing API Test Role');",
            ("role_id", roleId),
            ("role_code", roleCode + "-" + suffix));
        await ExecuteAsync(
            DataSource,
            "INSERT INTO identity.user_roles (user_role_id, user_id, role_id) VALUES (@id, @user_id, @role_id);",
            ("id", Guid.NewGuid()),
            ("user_id", userId),
            ("role_id", roleId));

        foreach (var code in permissionCodes)
        {
            await ExecuteAsync(
                DataSource,
                """
                INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
                SELECT @id, @role_id, permission_id FROM identity.permissions WHERE code = @code;
                """,
                ("id", Guid.NewGuid()),
                ("role_id", roleId),
                ("code", code));
        }

        return (userId, $"{DualScreenApplication.CashierCookieName}={raw}");
    }

    /// <summary>V1-RMD-410: records an approved payment allocated to the bill through the real repositories.</summary>
    public async Task SeedCollectedAsync(Guid billId, decimal amount, Guid cashierUserId)
    {
        var bill = await new PostgresBillRepository(DataSource).GetByIdAsync(billId)
            ?? throw new InvalidOperationException("Seeded bill not found.");
        var actor = cashierUserId;
        var payment = new ALKAROS.Payments.PaymentAggregate.Payment(Guid.NewGuid(), billId, amount)
            .Tender(amount, changedBy: actor)
            .Approve(amount, changedBy: actor);
        await new ALKAROS.Payments.PaymentAggregate.PostgresPaymentRepository(DataSource).AddAsync(payment);
        await new ALKAROS.Payments.Allocations.Persistence.PostgresPaymentAllocationRepository(
                DataSource, new ALKAROS.Billing.Adjustments.PostgresBillAdjustmentRepository(DataSource))
            .AllocateAsync(payment, bill, amount, "rmd410-" + Guid.NewGuid().ToString("N"));
    }

    public async Task<SeededBill> SeedBillAsync()
    {
        var zoneId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        var firstProductId = Guid.NewGuid();
        var secondProductId = Guid.NewGuid();
        await ExecuteAsync(
            DataSource,
            """
            INSERT INTO table_mgmt.zones (zone_id, code, name) VALUES (@zone_id, @zone_code, 'Main Floor');
            INSERT INTO table_mgmt.tables (table_id, zone_id, table_number, capacity, current_status)
            VALUES (@table_id, @zone_id, @table_number, 2, 'Occupied');
            INSERT INTO table_mgmt.table_seats (seat_id, table_id, seat_number, label, x, y)
            VALUES (@seat_id, @table_id, 1, 'Seat 1', 0, 0);
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, current_price)
            VALUES (@first_product_id, @first_sku, 'Main course', 1, 1, 100),
                   (@second_product_id, @second_sku, 'Drink', 1, 1, 50);
            """,
            ("zone_id", zoneId),
            ("zone_code", "ZONE-" + zoneId.ToString("N")),
            ("table_id", tableId),
            ("table_number", "T-" + tableId.ToString("N")[..6]),
            ("seat_id", seatId),
            ("first_product_id", firstProductId),
            ("first_sku", "SKU-" + firstProductId.ToString("N")[..8]),
            ("second_product_id", secondProductId),
            ("second_sku", "SKU-" + secondProductId.ToString("N")[..8]));

        var orderId = Guid.NewGuid();
        var firstOrderItem = new OrderItem(Guid.NewGuid(), orderId, firstProductId, "Main course", 2m, 100m, 10m);
        var secondOrderItem = new OrderItem(Guid.NewGuid(), orderId, secondProductId, "Drink", 1m, 50m, 10m);
        var order = new Order(
            orderId,
            OrderSource.Cashier,
            "ORD-" + orderId.ToString("N"),
            [firstOrderItem, secondOrderItem],
            tableId);
        await new PostgresOrderRepository(DataSource).AddAsync(order);

        var bill = Bill.FromOrder(Guid.NewGuid(), "BIL-" + orderId.ToString("N"), order);
        await new PostgresBillRepository(DataSource).AddAsync(bill);
        return new SeededBill(bill.Id, bill.RowVersion, seatId, bill.Items[0].Id, bill.Items[1].Id);
    }

    public async Task<Guid> SeedOrderWithoutBillAsync()
    {
        var productId = Guid.NewGuid();
        await ExecuteAsync(
            DataSource,
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, current_price)
            VALUES (@product_id, @sku, 'Main course', 1, 1, 100);
            """,
            ("product_id", productId),
            ("sku", "SKU-" + productId.ToString("N")[..8]));

        var orderId = Guid.NewGuid();
        var orderItem = new OrderItem(Guid.NewGuid(), orderId, productId, "Main course", 1m, 100m, 10m);
        var order = new Order(orderId, OrderSource.Cashier, "ORD-" + orderId.ToString("N"), [orderItem]);
        await new PostgresOrderRepository(DataSource).AddAsync(order);
        return orderId;
    }

    public async Task<long> BillCountForOrderAsync(Guid orderId)
    {
        await using var command = DataSource.CreateCommand(
            "SELECT count(*) FROM billing.bills WHERE order_id = @order_id;");
        command.Parameters.AddWithValue("order_id", orderId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    public async Task<long> BillAdjustmentCountAsync(Guid billId)
    {
        await using var command = DataSource.CreateCommand(
            "SELECT count(*) FROM billing.bill_adjustments WHERE bill_id = @bill_id;");
        command.Parameters.AddWithValue("bill_id", billId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    public async Task<IReadOnlyList<Guid>> AllocationIdsAsync(Guid billId)
    {
        await using var command = DataSource.CreateCommand(
            "SELECT bill_allocation_id FROM billing.bill_allocations WHERE bill_id = @bill_id ORDER BY created_at, bill_allocation_id;");
        command.Parameters.AddWithValue("bill_id", billId);
        var result = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(reader.GetGuid(0));
        return result;
    }

    public Task SetBillStatusAsync(Guid billId, string status)
        => ExecuteAsync(
            DataSource,
            "UPDATE billing.bills SET status = @status WHERE bill_id = @bill_id;",
            ("status", status),
            ("bill_id", billId));

    /// <summary>
    /// Stands in for a manager approving a pending grant via
    /// AuthorizationDecisionEndpoints — only the resolution fields move
    /// (identity.authorization_grants' own trigger forbids changing
    /// amount/reason_code once inserted, so this cannot itself introduce
    /// the tampering the caller is testing against).
    /// </summary>
    public Task ApproveGrantAsync(Guid grantId)
    {
        var approverId = Guid.NewGuid();
        return ExecuteAsync(
            DataSource,
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@approver_id, @approver_username, 'not-used', 'Billing API Test Approver', true);
            UPDATE identity.authorization_grants
            SET status = 'granted', policy_path = 'manual',
                approver_user_id = @approver_id, resolved_at = now()
            WHERE grant_id = @grant_id;
            """,
            ("grant_id", grantId),
            ("approver_id", approverId),
            ("approver_username", "rmd103-approver-" + approverId.ToString("N")));
    }

    /// <summary>V1-WTR-021: attributes a seeded bill's underlying order to a waiter for the shift-summary test.</summary>
    public Task SetOrderServingUserAsync(Guid billId, Guid waiterUserId)
        => ExecuteAsync(
            DataSource,
            """
            UPDATE orders.orders SET serving_user_id = @waiter_user_id
            WHERE order_id = (SELECT order_id FROM billing.bills WHERE bill_id = @bill_id);
            """,
            ("waiter_user_id", waiterUserId),
            ("bill_id", billId));

    /// <summary>V1-WTR-021 review fix: sets a seeded bill's underlying order's own status directly (bypassing the aggregate) for the shift-summary Cancelled-exclusion test.</summary>
    public Task SetOrderStatusAsync(Guid billId, string status)
        => ExecuteAsync(
            DataSource,
            """
            UPDATE orders.orders SET status = @status
            WHERE order_id = (SELECT order_id FROM billing.bills WHERE bill_id = @bill_id);
            """,
            ("status", status),
            ("bill_id", billId));

    private NpgsqlDataSource DataSource
        => _dataSource ?? throw new InvalidOperationException("Test database is not initialized.");

    private async Task ApplyMigrationsAsync()
    {
        var root = FindRepositoryRoot();
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(
            System.IO.Path.Combine(root, "database", "MigrationComposition", "order.json")));
        var migrationRoot = System.IO.Path.Combine(root, "database", "migrations");
        foreach (var migration in manifest.RootElement.GetProperty("migrations").EnumerateArray())
        {
            var id = migration.GetProperty("id").GetString() ?? throw new InvalidOperationException("Migration ID is missing.");
            var files = Directory.GetFiles(migrationRoot, $"{id}-*.up.sql", SearchOption.AllDirectories);
            Assert.Single(files);
            await ExecuteAsync(DataSource, await File.ReadAllTextAsync(files[0]));
        }
    }

    private static async Task ExecuteAsync(
        NpgsqlDataSource dataSource,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = dataSource.CreateCommand(sql);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await command.ExecuteNonQueryAsync();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(System.IO.Path.Combine(directory.FullName, "database", "MigrationComposition", "order.json")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }
}
