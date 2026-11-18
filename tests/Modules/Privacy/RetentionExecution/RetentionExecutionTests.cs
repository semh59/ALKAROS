using System.Globalization;
using ALKAROS.Privacy.RetentionExecution.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace ALKAROS.Privacy.RetentionExecution.Tests;

/// <summary>
/// On a real PostgreSQL with every migration: the plan and the executed run select the same records, a legal hold keeps a
/// record out and blocks its completion, and repeating a run changes nothing.
/// </summary>
public sealed class RetentionExecutionTests : IAsyncLifetime
{
    private static readonly DateTimeOffset AsOf = new(2030, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly RetentionTestDatabase _database = new();
    private PostgresRetentionExecutionService _service = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _service = new PostgresRetentionExecutionService(_database.DataSource);
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    private static DateTimeOffset Ago(int years, int months = 0) => AsOf.AddYears(-years).AddMonths(-months);

    private static HashSet<(RetentionClass, Guid)> Keys(RetentionPlan plan)
        => plan.Candidates.Select(candidate => (candidate.DataClass, candidate.SubjectId)).ToHashSet();

    private async Task<(Guid StaffDue, Guid OrderDue, Guid ReservationDue, Guid CustomerDue, Guid SupplierDue)> SeedOneDuePerClassAsync()
    {
        var staff = await _database.SeedUserAsync(active: false, Ago(1, 2));
        var order = await _database.SeedOrderAsync("Completed", Ago(5, 2), orderNote: "Ahmet Bey", itemNote: "penceresiz");
        var reservation = await _database.SeedReservationAsync("Expired", Ago(5, 2));
        var customer = await _database.SeedCustomerAsync(Ago(12), lastTransaction: Ago(2, 2));
        var supplier = await _database.SeedSupplierAsync(Ago(12), lastOrder: Ago(10, 2));
        return (staff, order, reservation, customer, supplier);
    }

    [Fact]
    public async Task OnlyRecordsPastTheirWindowAreSelectedAndTheDueDateIsWindowEnd()
    {
        var due = await SeedOneDuePerClassAsync();
        await _database.SeedUserAsync(active: false, Ago(0, 11));
        await _database.SeedUserAsync(active: true, Ago(3));
        await _database.SeedUserAsync(active: false, Ago(3), displayName: "[anonymized]");
        await _database.SeedOrderAsync("Completed", Ago(4, 11), orderNote: "yakin");
        await _database.SeedOrderAsync("Draft", Ago(8), orderNote: "taslak");
        await _database.SeedOrderAsync("Completed", Ago(8), orderNote: null);
        await _database.SeedOrderAsync("Completed", Ago(8), orderNote: "[anonymized]", itemNote: "[anonymized]");
        await _database.SeedReservationAsync("Active", Ago(8));
        await _database.SeedReservationAsync("Expired", Ago(4));
        await _database.SeedCustomerAsync(Ago(12), lastTransaction: Ago(1, 11));
        await _database.SeedSupplierAsync(Ago(12), lastOrder: Ago(9, 11));

        var plan = await _service.PlanAsync(AsOf);

        Assert.Equal(4, plan.PolicyVersion);
        Assert.Equal(
            new HashSet<(RetentionClass, Guid)>
            {
                (RetentionClass.StaffAccount, due.StaffDue),
                (RetentionClass.OrderNotes, due.OrderDue),
                (RetentionClass.ReservationReason, due.ReservationDue),
                (RetentionClass.CustomerProfile, due.CustomerDue),
                (RetentionClass.Supplier, due.SupplierDue),
            },
            Keys(plan));
        Assert.Equal(Ago(1, 2).AddYears(1), plan.Candidates.Single(c => c.DataClass == RetentionClass.StaffAccount).DueSince);
        Assert.Equal(Ago(2, 2).AddYears(2), plan.Candidates.Single(c => c.DataClass == RetentionClass.CustomerProfile).DueSince);
    }

    [Fact]
    public async Task AProfileWithoutAnInvoiceIsDueTwoYearsAfterTheLastMovementAndOneTiedToAnInvoiceTenYearsAfterItsLastActivity()
    {
        var plain = await _database.SeedCustomerAsync(Ago(12), lastTransaction: Ago(2, 2));
        await _database.SeedCustomerAsync(Ago(12), lastTransaction: Ago(1, 11));
        await _database.SeedCustomerAsync(Ago(12), lastTransaction: Ago(3), invoicedAt: Ago(2, 2));
        var invoiced = await _database.SeedCustomerAsync(Ago(12), lastTransaction: Ago(11), invoicedAt: Ago(10, 2));

        var plan = await _service.PlanAsync(AsOf);

        Assert.Equal(
            new HashSet<(RetentionClass, Guid)>
            {
                (RetentionClass.CustomerProfile, plain),
                (RetentionClass.InvoicedCustomerProfile, invoiced),
            },
            Keys(plan));
        Assert.Equal(Ago(2, 2).AddYears(2), plan.Candidates.Single(c => c.SubjectId == plain).DueSince);
        Assert.Equal(Ago(10, 2).AddYears(10), plan.Candidates.Single(c => c.SubjectId == invoiced).DueSince);
    }

    [Fact]
    public async Task ThePublishedPolicyGivesCustomersWithoutAnInvoiceTwoYearsAndKeepsTheEarlierVersionAsItWas()
    {
        Assert.Equal(6, await _database.ScalarAsync<int>("SELECT count(*)::int FROM privacy.retention_rules WHERE policy_version = 2"));
        Assert.Equal(2, await _database.ScalarAsync<int>("SELECT retention_years FROM privacy.retention_rules WHERE policy_version = 2 AND data_class = 'CustomerProfile'"));
        Assert.Equal(10, await _database.ScalarAsync<int>("SELECT retention_years FROM privacy.retention_rules WHERE policy_version = 2 AND data_class = 'InvoicedCustomerProfile'"));
        Assert.Equal(10, await _database.ScalarAsync<int>("SELECT retention_years FROM privacy.retention_rules WHERE policy_version = 1 AND data_class = 'CustomerProfile'"));
        Assert.Equal(5, await _database.ScalarAsync<int>("SELECT count(*)::int FROM privacy.retention_rules WHERE policy_version = 1"));
    }

    [Fact]
    public async Task ThePublishedPolicyThreeAddsTheBillClassWithTheOrderWindowAndKeepsTheEarlierVersions()
    {
        Assert.Equal(7, await _database.ScalarAsync<int>("SELECT count(*)::int FROM privacy.retention_rules WHERE policy_version = 3"));
        Assert.Equal(5, await _database.ScalarAsync<int>("SELECT retention_years FROM privacy.retention_rules WHERE policy_version = 3 AND data_class = 'BillFreeText'"));
        Assert.Equal(0, await _database.ScalarAsync<int>(
            """
            SELECT count(*)::int FROM privacy.retention_rules n JOIN privacy.retention_rules o ON o.data_class = n.data_class AND o.policy_version = 2
            WHERE n.policy_version = 3 AND n.retention_years <> o.retention_years
            """));
        Assert.Equal(6, await _database.ScalarAsync<int>("SELECT count(*)::int FROM privacy.retention_rules WHERE policy_version = 2"));
    }

    [Fact]
    public async Task AClosedBillIsDueFiveYearsAfterCreationOnlyWhileAFreeTextRemains()
    {
        var byReason = await SeedBillAsync("Paid", Ago(5, 2), cancellationReason: "Ali Bey");
        var byItem = await SeedBillAsync("Cancelled", Ago(5, 2), itemNote: "sogansiz");
        var byAdjustment = await SeedBillAsync("Paid", Ago(5, 2), adjustmentNote: "tanidik");
        var byRefundReason = await SeedBillAsync("Paid", Ago(5, 2), refundStatus: "Completed", refundReason: "Ali Bey", decisionNote: "[anonymized]");
        var byDecisionNote = await SeedBillAsync("Paid", Ago(5, 2), refundStatus: "Rejected", refundReason: "[anonymized]", decisionNote: "Ali Bey");
        await SeedBillAsync("Paid", Ago(4, 11), cancellationReason: "yakin");
        await SeedBillAsync("Open", Ago(8), cancellationReason: "acik");
        await SeedBillAsync("Paid", Ago(8), cancellationReason: "[anonymized]", itemNote: "[anonymized]", adjustmentNote: "[anonymized]", refundStatus: "Completed", refundReason: "[anonymized]", decisionNote: "[anonymized]");
        await SeedBillAsync("Paid", Ago(8));
        await SeedBillAsync("Paid", Ago(8), refundStatus: "Requested", refundReason: "bekleyen talep");
        await SeedBillAsync("Paid", Ago(8), cancellationReason: "acik talebi var", refundStatus: "AwaitingManualRefund", refundReason: "bekleyen talep");

        var plan = await _service.PlanAsync(AsOf);

        Assert.Equal(
            new HashSet<(RetentionClass, Guid)>
            {
                (RetentionClass.BillFreeText, byReason),
                (RetentionClass.BillFreeText, byItem),
                (RetentionClass.BillFreeText, byAdjustment),
                (RetentionClass.BillFreeText, byRefundReason),
                (RetentionClass.BillFreeText, byDecisionNote),
            },
            Keys(plan));
        Assert.Equal(Ago(5, 2).AddYears(5), plan.Candidates.First(c => c.SubjectId == byReason).DueSince);
    }

    [Fact]
    public async Task AnOldClosedOrderWhoseOnlyRemainingTextIsAStatusHistoryReasonIsDue()
    {
        var withReason = await _database.SeedOrderAsync("Completed", Ago(5, 2), orderNote: "[anonymized]", itemNote: "[anonymized]");
        var scrubbed = await _database.SeedOrderAsync("Completed", Ago(5, 2), orderNote: "[anonymized]", itemNote: "[anonymized]");
        var withoutReason = await _database.SeedOrderAsync("Completed", Ago(5, 2), orderNote: "[anonymized]", itemNote: "[anonymized]");
        var draft = await _database.SeedOrderAsync("Draft", Ago(8), orderNote: "[anonymized]", itemNote: "[anonymized]");
        foreach (var (order, reason) in new[] { (withReason, "Ali Bey"), (scrubbed, "[anonymized]"), (withoutReason, null), (draft, "Ali Bey") })
        {
            await _database.ExecuteAsync(
                """
                SET LOCAL session_replication_role = replica;
                INSERT INTO orders.order_status_history (order_status_history_id, order_id, old_status, new_status, reason, changed_at)
                VALUES (gen_random_uuid(), @o, 'Active', 'Cancelled', @r, now());
                """,
                ("o", order), ("r", (object?)reason ?? DBNull.Value));
        }

        var plan = await _service.PlanAsync(AsOf);

        Assert.Equal([(RetentionClass.OrderNotes, withReason)], Keys(plan));
    }

    [Fact]
    public async Task AnOldClosedOrderIsDueWhileATextRemainsOnTheKitchenSideAndRunningPrintsDoNotCount()
    {
        const string Slip = "SIPARIS\n      NOT: {0}\n---";
        var byTicket = await SeedKitchenOrderAsync(ticketReason: "Ali Bey");
        var byItem = await SeedKitchenOrderAsync(itemNote: "Ali Bey");
        var byItemReason = await SeedKitchenOrderAsync(itemReason: "Ali Bey");
        var byJob = await SeedKitchenOrderAsync(jobStatus: "Printed", jobPayload: string.Format(CultureInfo.InvariantCulture, Slip, "Ali Bey"));
        var bySnapshot = await SeedKitchenOrderAsync(deliveryStatus: "Printed", snapshot: string.Format(CultureInfo.InvariantCulture, Slip, "Ali Bey"));
        var byReprint = await SeedKitchenOrderAsync(deliveryStatus: "Reprinted", reprint: string.Format(CultureInfo.InvariantCulture, Slip, "Ali Bey"));
        var byOperator = await SeedKitchenOrderAsync(deliveryStatus: "ReprintRejected", operatorReason: "Ali Bey");
        await SeedKitchenOrderAsync(ticketReason: "[anonymized]", itemNote: "[anonymized]", itemReason: "[anonymized]", jobStatus: "Printed",
            jobPayload: string.Format(CultureInfo.InvariantCulture, Slip, "[anonymized]"), deliveryStatus: "Printed", snapshot: string.Format(CultureInfo.InvariantCulture, Slip, "[anonymized]"), operatorReason: "[anonymized]");
        var byLookalike = await SeedKitchenOrderAsync(jobStatus: "Printed", jobPayload: string.Format(CultureInfo.InvariantCulture, Slip, "[anonymized] Ali Bey"));
        await SeedKitchenOrderAsync(jobStatus: "Printed", jobPayload: "SIPARIS\r\n      NOT: [anonymized]\r\n1     Kuru NOT: fasulye\r\n---");
        var byBanner = await SeedKitchenOrderAsync(deliveryStatus: "Reprinted", reprint: "####\nNEDEN: Ali Bey\n----\nSIPARIS");
        var settledJobs = new List<Guid>();
        foreach (var settled in new[] { "DeadLetter", "Cancelled", "Failed", "AwaitingOperatorReview" })
            settledJobs.Add(await SeedKitchenOrderAsync(jobStatus: settled, jobPayload: string.Format(CultureInfo.InvariantCulture, Slip, "Ali Bey")));
        foreach (var running in new[] { "Pending", "Leased", "Printing" })
            await SeedKitchenOrderAsync(jobStatus: running, jobPayload: string.Format(CultureInfo.InvariantCulture, Slip, "Ali Bey"));
        foreach (var running in new[] { "InFlight", "Unknown", "ReprintInFlight" })
            await SeedKitchenOrderAsync(deliveryStatus: running, snapshot: string.Format(CultureInfo.InvariantCulture, Slip, "Ali Bey"), operatorReason: "Ali Bey");
        await SeedKitchenOrderAsync(deliveryStatus: "ReprintApproved", reprint: string.Format(CultureInfo.InvariantCulture, Slip, "Ali Bey"));
        await SeedKitchenOrderAsync(ticketReason: "yakin", ago: Ago(4, 11));
        await SeedKitchenOrderAsync(ticketReason: "taslak", status: "Draft");

        var plan = await _service.PlanAsync(AsOf);

        Assert.Equal(
            new HashSet<(RetentionClass, Guid)>
            {
                (RetentionClass.OrderNotes, byTicket), (RetentionClass.OrderNotes, byItem), (RetentionClass.OrderNotes, byItemReason),
                (RetentionClass.OrderNotes, byJob), (RetentionClass.OrderNotes, bySnapshot), (RetentionClass.OrderNotes, byReprint),
                (RetentionClass.OrderNotes, byOperator), (RetentionClass.OrderNotes, byLookalike), (RetentionClass.OrderNotes, byBanner),
            }.Concat(settledJobs.Select(order => (RetentionClass.OrderNotes, order))).ToHashSet(),
            Keys(plan));
    }

    private async Task<Guid> SeedKitchenOrderAsync(
        string? ticketReason = null, string? itemNote = null, string? itemReason = null, string? jobStatus = null, string? jobPayload = null,
        string? deliveryStatus = null, string? snapshot = null, string? reprint = null, string? operatorReason = null,
        string status = "Completed", DateTimeOffset? ago = null)
    {
        var at = ago ?? Ago(5, 2);
        var order = await _database.SeedOrderAsync(status, at, orderNote: "[anonymized]", itemNote: "[anonymized]");
        await _database.ExecuteAsync(
            """
            SET LOCAL session_replication_role = replica;
            INSERT INTO kitchen.kitchen_tickets (id, order_id, ticket_number, station_id, status, created_at, cancellation_reason)
            VALUES (@t::uuid, @o::uuid, @tn::text, 'hot', 'Cancelled', @at::timestamptz, @tr::text);
            INSERT INTO kitchen.kitchen_ticket_items (id, ticket_id, order_item_id, product_id, product_name_snapshot, quantity, status, notes, created_at, cancellation_reason)
            VALUES (gen_random_uuid(), @t::uuid, gen_random_uuid(), gen_random_uuid(), 'Test', 1, 'Cancelled', @in::text, @at::timestamptz, @ir::text);
            INSERT INTO kitchen.print_jobs (id, ticket_id, printer_id, idempotency_key, payload, status, created_at)
            VALUES (@j::uuid, @t::uuid, gen_random_uuid(), 'k-' || @j::text, COALESCE(@jp::text, 'SIPARIS'), COALESCE(@js::text, 'Printed'), @at::timestamptz);
            INSERT INTO kitchen.physical_print_deliveries (id, print_job_id, ticket_id, printer_id, status, operator_reason, payload_snapshot, reprint_payload, created_at)
            SELECT gen_random_uuid(), @j::uuid, @t::uuid, gen_random_uuid(), @ds::text, @opr::text, COALESCE(@snap::text, 'SIPARIS'), @rp::text, @at::timestamptz WHERE @ds::text IS NOT NULL;
            """,
            ("t", Guid.NewGuid()), ("o", order), ("tn", $"T-{order:N}"[..20]), ("at", at), ("tr", (object?)ticketReason ?? DBNull.Value),
            ("in", (object?)itemNote ?? DBNull.Value), ("ir", (object?)itemReason ?? DBNull.Value), ("j", Guid.NewGuid()),
            ("jp", (object?)jobPayload ?? DBNull.Value), ("js", (object?)jobStatus ?? DBNull.Value), ("ds", (object?)deliveryStatus ?? DBNull.Value),
            ("opr", (object?)operatorReason ?? DBNull.Value), ("snap", (object?)snapshot ?? DBNull.Value), ("rp", (object?)reprint ?? DBNull.Value));
        return order;
    }

    [Fact]
    public async Task AClosedBillIsDueWhileASettledPaymentTextRemainsAndTheTextsOfAnUnsettledOrPendingOnesDoNotCount()
    {
        var byHistory = await SeedBillWithPaymentAsync(historyReason: "Ali Bey");
        var byReversal = await SeedBillWithPaymentAsync(reversalReason: "Ali Bey");
        var byRequestNote = await SeedBillWithPaymentAsync(confirmationStatus: "Approved", requestNote: "Ali Bey");
        var byDecisionNote = await SeedBillWithPaymentAsync(confirmationStatus: "Rejected", decisionNote: "Ali Bey");
        await SeedBillWithPaymentAsync(historyReason: "[anonymized]", reversalReason: "[anonymized]", confirmationStatus: "Approved", requestNote: "[anonymized]", decisionNote: "[anonymized]");
        await SeedBillWithPaymentAsync(paymentStatus: "Approved", confirmationStatus: "Pending", requestNote: "bekliyor");
        foreach (var open in new[] { "Initiated", "Pending", "Unknown", "ReconciliationRequired" })
            await SeedBillWithPaymentAsync(historyReason: "Ali Bey", paymentStatus: open);
        await SeedBillWithPaymentAsync(historyReason: "yakin", ago: Ago(4, 11));
        var mixed = await SeedBillWithPaymentAsync(historyReason: "Ali Bey");
        await SeedPaymentAsync(mixed, "Pending", historyReason: "acik odeme");

        var plan = await _service.PlanAsync(AsOf);

        Assert.Equal(
            new HashSet<(RetentionClass, Guid)>
            {
                (RetentionClass.BillFreeText, byHistory), (RetentionClass.BillFreeText, byReversal),
                (RetentionClass.BillFreeText, byRequestNote), (RetentionClass.BillFreeText, byDecisionNote),
                (RetentionClass.BillFreeText, mixed),
            },
            Keys(plan));
    }

    private async Task<Guid> SeedBillWithPaymentAsync(
        string? historyReason = null, string? reversalReason = null, string? confirmationStatus = null, string? requestNote = null,
        string? decisionNote = null, string paymentStatus = "Refunded", DateTimeOffset? ago = null)
    {
        var bill = await SeedBillAsync("Paid", ago ?? Ago(5, 2));
        await SeedPaymentAsync(bill, paymentStatus, historyReason, reversalReason, confirmationStatus, requestNote, decisionNote);
        return bill;
    }

    private async Task SeedPaymentAsync(
        Guid bill, string paymentStatus, string? historyReason = null, string? reversalReason = null, string? confirmationStatus = null,
        string? requestNote = null, string? decisionNote = null)
    {
        await _database.ExecuteAsync(
            """
            SET LOCAL session_replication_role = replica;
            INSERT INTO payments.payments (payment_id, bill_id, status, requested_amount, tendered_amount, approved_amount, initiated_at, created_at, updated_at)
            VALUES (@p, @b, @st::text, 50, CASE WHEN @st::text <> 'Initiated' THEN 50 END, CASE WHEN @st::text IN ('Approved', 'Refunded', 'PartiallyRefunded') THEN 50 END, now(), now(), now());
            INSERT INTO payments.payment_status_history (payment_status_history_id, payment_id, old_status, new_status, reason, changed_at)
            VALUES (gen_random_uuid(), @p, 'Approved', 'Refunded', @hr, now());
            INSERT INTO payments.payment_reversals (reversal_id, payment_id, reversal_type, amount, reason, refunded_by, idempotency_key, occurred_at)
            SELECT gen_random_uuid(), @p, 'FullRefund', 50, @rr, gen_random_uuid(), 'k-' || @p::text, now() WHERE @rr::text IS NOT NULL;
            INSERT INTO payments.manual_card_confirmations (confirmation_id, payment_id, bill_id, slip_number, amount, status, requested_by, requested_at, request_note, decided_by, decided_at, decision_note)
            SELECT gen_random_uuid(), @p, @b, 'S-' || @p::text, 50, @cs, gen_random_uuid(), now(), @rn,
                   CASE WHEN @cs <> 'Pending' THEN gen_random_uuid() END, CASE WHEN @cs <> 'Pending' THEN now() END, @dn
            WHERE @cs::text IS NOT NULL;
            """,
            ("p", Guid.NewGuid()), ("b", bill), ("st", paymentStatus), ("hr", (object?)historyReason ?? DBNull.Value),
            ("rr", (object?)reversalReason ?? DBNull.Value), ("cs", (object?)confirmationStatus ?? DBNull.Value),
            ("rn", (object?)requestNote ?? DBNull.Value), ("dn", (object?)decisionNote ?? DBNull.Value));
    }

    [Fact]
    public async Task ThePublishedPolicyFourAddsTheCashAndTableMovementClassesWithTheOrderWindowAndKeepsTheEarlierVersions()
    {
        Assert.Equal(9, await _database.ScalarAsync<int>("SELECT count(*)::int FROM privacy.retention_rules WHERE policy_version = 4"));
        Assert.Equal(2, await _database.ScalarAsync<int>(
            "SELECT count(*)::int FROM privacy.retention_rules WHERE policy_version = 4 AND data_class IN ('CashFreeText', 'TableMovementReason') AND retention_years = 5"));
        Assert.Equal(0, await _database.ScalarAsync<int>(
            """
            SELECT count(*)::int FROM privacy.retention_rules n JOIN privacy.retention_rules o ON o.data_class = n.data_class AND o.policy_version = 3
            WHERE n.policy_version = 4 AND n.retention_years <> o.retention_years
            """));
        Assert.Equal(7, await _database.ScalarAsync<int>("SELECT count(*)::int FROM privacy.retention_rules WHERE policy_version = 3"));
    }

    [Fact]
    public async Task AClosedCashSessionIsDueFiveYearsAfterItWasClosedWhileATextRemainsAndAnOpenOrPendingOneNever()
    {
        var byOverride = await SeedCashSessionAsync("Closed", Ago(5, 2), overrideReason: "Ali Bey izin verdi");
        var byReconciliation = await SeedCashSessionAsync("Reconciled", Ago(5, 2), reconciliationNotes: "Ali Bey sayti");
        var byExplanation = await SeedCashSessionAsync("Closed", Ago(5, 2), differenceExplanation: "Ali Bey eksik verdi");
        var byCount = await SeedCashSessionAsync("Closed", Ago(5, 2), countNote: "Ali Bey saydi");
        var byTransaction = await SeedCashSessionAsync("Closed", Ago(5, 2), transactionNote: "Ali Bey avans");
        var byRequestReason = await SeedCashSessionAsync("Closed", Ago(5, 2), requestStatus: "Rejected", requestReason: "Ali Bey avans", decisionNote: "[anonymized]");
        var byDecisionNote = await SeedCashSessionAsync("Reconciled", Ago(5, 2), requestStatus: "Rejected", requestReason: "[anonymized]", decisionNote: "Ali Bey reddetti");
        await SeedCashSessionAsync("Closed", Ago(5, 2), overrideReason: "[anonymized]", reconciliationNotes: "[anonymized]", differenceExplanation: "[anonymized]",
            countNote: "[anonymized]", transactionNote: "[anonymized]", requestStatus: "Rejected", requestReason: "[anonymized]", decisionNote: "[anonymized]");
        await SeedCashSessionAsync("Closed", Ago(5, 2));
        await SeedCashSessionAsync("Closed", Ago(4, 11), transactionNote: "yakin");
        await SeedCashSessionAsync("Reconciled", Ago(6), transactionNote: "yeni mutabakat", reconciledAt: Ago(2));
        foreach (var live in new[] { "Open", "Counting", "Closing" })
            await SeedCashSessionAsync(live, null, overrideReason: "acik", countNote: "acik", transactionNote: "acik", opened: Ago(8));
        await SeedCashSessionAsync("Closed", Ago(8), transactionNote: "bekleyen talep", requestStatus: "Requested", requestReason: "bekliyor");

        var plan = await _service.PlanAsync(AsOf);

        Assert.Equal(
            new HashSet<(RetentionClass, Guid)>
            {
                (RetentionClass.CashFreeText, byOverride), (RetentionClass.CashFreeText, byReconciliation), (RetentionClass.CashFreeText, byExplanation),
                (RetentionClass.CashFreeText, byCount), (RetentionClass.CashFreeText, byTransaction), (RetentionClass.CashFreeText, byRequestReason),
                (RetentionClass.CashFreeText, byDecisionNote),
            },
            Keys(plan));
    }

    [Fact]
    public async Task ATableTransferIsDueFiveYearsAfterItsDateWhileATextRemains()
    {
        var due = await SeedTableTransferAsync(Ago(5, 2), "Ali Bey istedi");
        await SeedTableTransferAsync(Ago(5, 2), "[anonymized]");
        await SeedTableTransferAsync(Ago(4, 11), "yakin");

        var plan = await _service.PlanAsync(AsOf);

        Assert.Equal(new HashSet<(RetentionClass, Guid)> { (RetentionClass.TableMovementReason, due) }, Keys(plan));
    }

    private async Task<Guid> SeedCashSessionAsync(
        string status, DateTimeOffset? closedAt, string? overrideReason = null, string? reconciliationNotes = null, string? differenceExplanation = null,
        string? countNote = null, string? transactionNote = null, string? requestStatus = null, string? requestReason = null, string? decisionNote = null,
        DateTimeOffset? opened = null, DateTimeOffset? reconciledAt = null)
    {
        var session = Guid.NewGuid();
        var openedAt = opened ?? closedAt!.Value.AddHours(-8);
        await _database.ExecuteAsync(
            """
            SET LOCAL session_replication_role = replica;
            INSERT INTO cash.cash_sessions (cash_session_id, cashier_user_id, terminal_id, status, opening_balance, opened_at, closed_at,
                                            is_supervisor_override, override_reason, reconciliation_notes, difference_explanation, created_at, updated_at, reconciled_at)
            VALUES (@s, gen_random_uuid(), gen_random_uuid(), @st::text, 0, @op, @cl, @ovr::text IS NOT NULL, @ovr::text, @rec::text, @dif::text, @op, @op, @rc);
            INSERT INTO cash.cash_counts (cash_count_id, cash_session_id, counted_amount, counted_by, notes, counted_at)
            SELECT gen_random_uuid(), @s, 10, gen_random_uuid(), @cn::text, @op WHERE @cn::text IS NOT NULL;
            INSERT INTO cash.cash_transactions (cash_transaction_id, cash_session_id, type, direction, amount, notes, occurred_at)
            SELECT gen_random_uuid(), @s, 'CashIn', 'In', 5, @tn::text, @op WHERE @tn::text IS NOT NULL;
            INSERT INTO cash.cash_movement_requests (request_id, cash_session_id, amount, reason, idempotency_key, status, requested_by, requested_at, decided_by, decided_at, decision_note)
            SELECT gen_random_uuid(), @s, 5, @rr::text, 'k-' || @s::text, @rs::text, gen_random_uuid(), @op,
                   CASE WHEN @rs::text <> 'Requested' THEN gen_random_uuid() END, CASE WHEN @rs::text <> 'Requested' THEN @op END, @dn::text
            WHERE @rs::text IS NOT NULL;
            """,
            ("s", session), ("st", status), ("op", openedAt), ("cl", (object?)closedAt ?? DBNull.Value), ("ovr", (object?)overrideReason ?? DBNull.Value),
            ("rec", (object?)reconciliationNotes ?? DBNull.Value), ("dif", (object?)differenceExplanation ?? DBNull.Value), ("cn", (object?)countNote ?? DBNull.Value),
            ("tn", (object?)transactionNote ?? DBNull.Value), ("rs", (object?)requestStatus ?? DBNull.Value), ("rr", (object?)requestReason ?? "x"),
            ("dn", (object?)decisionNote ?? DBNull.Value), ("rc", (object?)reconciledAt ?? DBNull.Value));
        return session;
    }

    private async Task<Guid> SeedTableTransferAsync(DateTimeOffset at, string reason)
    {
        var id = Guid.NewGuid();
        await _database.ExecuteAsync(
            """
            SET LOCAL session_replication_role = replica;
            INSERT INTO table_mgmt.table_transfers (table_transfer_id, source_table_id, target_table_id, reason, transferred_by, transferred_at)
            VALUES (@id, gen_random_uuid(), gen_random_uuid(), @r, gen_random_uuid(), @at);
            """,
            ("id", id), ("r", reason), ("at", at));
        return id;
    }

    private async Task<Guid> SeedBillAsync(
        string status, DateTimeOffset createdAt, string? cancellationReason = null, string? itemNote = null, string? adjustmentNote = null,
        string? refundStatus = null, string? refundReason = null, string? decisionNote = null)
    {
        var billId = Guid.NewGuid();
        await _database.ExecuteAsync(
            """
            SET LOCAL session_replication_role = replica;
            INSERT INTO billing.bills (bill_id, bill_number, status, opened_at, created_at, updated_at, cancellation_reason)
            VALUES (@b, @num, @st, @at, @at, @at, @cr);
            INSERT INTO billing.bill_items (bill_item_id, bill_id, order_item_id, product_id, product_name_snapshot, quantity, unit_price, tax_rate, tax_amount, net_amount, gross_amount, line_type, notes, created_at, updated_at)
            VALUES (gen_random_uuid(), @b, gen_random_uuid(), gen_random_uuid(), 'Test', 1, 10, 10, 1, 10, 11, 'Sale', @item, @at, @at);
            INSERT INTO billing.bill_adjustments (bill_adjustment_id, bill_id, adjustment_type, calculation_type, amount, net_amount, gross_amount, is_deduction, reason, authorized_by, notes, created_at)
            VALUES (gen_random_uuid(), @b, 'DiscountAmount', 'FixedAmount', 1, 1, 1, true, 'Sadakat', gen_random_uuid(), @adj, @at);
            INSERT INTO payments.refund_requests (refund_request_id, bill_id, payment_id, amount, method, reason, status, requested_by, requested_at, decision_note, row_version)
            SELECT gen_random_uuid(), @b, gen_random_uuid(), 1, 'Cash', @rr::text, @rs::text, gen_random_uuid(), @at, @dn::text, 1 WHERE @rs::text IS NOT NULL;
            """,
            ("b", billId), ("num", $"B-{billId:N}"[..20]), ("st", status), ("at", createdAt),
            ("cr", (object?)cancellationReason ?? DBNull.Value), ("item", (object?)itemNote ?? DBNull.Value), ("adj", (object?)adjustmentNote ?? DBNull.Value),
            ("rs", (object?)refundStatus ?? DBNull.Value), ("rr", (object?)refundReason ?? "x"), ("dn", (object?)decisionNote ?? DBNull.Value));
        return billId;
    }

    [Fact]
    public async Task ALegalHoldOnAnInvoicedCustomerKeepsItOutOfThePlan()
    {
        var invoiced = await _database.SeedCustomerAsync(Ago(12), lastTransaction: Ago(11), invoicedAt: Ago(10, 2));
        await _service.PlaceHoldAsync(RetentionClass.InvoicedCustomerProfile, invoiced, "Vergi incelemesi", Guid.NewGuid());

        Assert.Empty((await _service.PlanAsync(AsOf)).Candidates);
    }

    [Fact]
    public async Task ACustomerIsNotDueWhileItHasABalanceARecentInvoiceOrAPendingAnonymization()
    {
        var owes = await _database.SeedCustomerAsync(Ago(12), lastTransaction: Ago(11), balance: 25m);
        var credit = await _database.SeedCustomerAsync(Ago(12), lastTransaction: Ago(11), balance: -5m);
        var invoiced = await _database.SeedCustomerAsync(Ago(12), lastTransaction: Ago(11), invoicedAt: Ago(2));
        var requested = await _database.SeedCustomerAsync(Ago(12), lastTransaction: Ago(11));
        await _database.RequestAnonymizationAsync(requested);
        var never = await _database.SeedCustomerAsync(Ago(11));
        var settled = await _database.SeedCustomerAsync(Ago(12), lastTransaction: Ago(11));
        var supplierActive = await _database.SeedSupplierAsync(Ago(12), lastOrder: Ago(1));

        var keys = Keys(await _service.PlanAsync(AsOf));

        Assert.Equal(
            new HashSet<(RetentionClass, Guid)> { (RetentionClass.CustomerProfile, never), (RetentionClass.CustomerProfile, settled) },
            keys);
        Assert.DoesNotContain((RetentionClass.CustomerProfile, owes), keys);
        Assert.DoesNotContain((RetentionClass.CustomerProfile, credit), keys);
        Assert.DoesNotContain((RetentionClass.CustomerProfile, invoiced), keys);
        Assert.DoesNotContain((RetentionClass.CustomerProfile, requested), keys);
        Assert.DoesNotContain((RetentionClass.Supplier, supplierActive), keys);
    }

    [Fact]
    public async Task ASupplierWithARecentPurchaseInvoiceOrGoodsReceiptIsNotDueEvenWithoutARecentOrder()
    {
        var invoiced = await _database.SeedSupplierAsync(Ago(12), lastOrder: Ago(10, 2), lastInvoice: Ago(1));
        var received = await _database.SeedSupplierAsync(Ago(12), lastOrder: Ago(10, 2), lastReceipt: Ago(1));
        var idle = await _database.SeedSupplierAsync(Ago(12), lastOrder: Ago(10, 2));

        var keys = Keys(await _service.PlanAsync(AsOf));

        Assert.Contains((RetentionClass.Supplier, idle), keys);
        Assert.DoesNotContain((RetentionClass.Supplier, invoiced), keys);
        Assert.DoesNotContain((RetentionClass.Supplier, received), keys);
    }

    [Fact]
    public async Task ThePlanWritesNothingAndTheExecutedRunSelectsExactlyTheSameRecords()
    {
        await SeedOneDuePerClassAsync();

        var plan = await _service.PlanAsync(AsOf);
        Assert.Equal(0, await _database.CountAsync("privacy.retention_runs"));
        Assert.Equal(0, await _database.CountAsync("privacy.retention_work_items"));

        var run = await _service.ExecuteAsync(AsOf, "tester");

        Assert.Equal(Keys(plan), Keys(run.Plan));
        Assert.Equal(plan.Candidates.Select(c => c.DueSince).Order(), run.Plan.Candidates.Select(c => c.DueSince).Order());
        Assert.Equal(1, await _database.CountAsync("privacy.retention_runs"));
        Assert.Equal(5, await _database.CountAsync("privacy.retention_run_items"));
        Assert.Equal(5, await _database.CountAsync("privacy.retention_work_items"));
        Assert.Equal(5, await _database.ScalarAsync<int>($"SELECT item_count FROM privacy.retention_runs WHERE run_id = '{run.RunId}'"));
        Assert.Equal(5, await _database.ScalarAsync<int>($"SELECT count(*)::int FROM privacy.retention_work_items WHERE run_id = '{run.RunId}' AND policy_version = 4 AND status = 'Pending'"));
    }

    [Fact]
    public async Task RepeatingARunAddsNoWorkItemsAndOnlyRecordsTheEmptyRun()
    {
        await SeedOneDuePerClassAsync();
        await _service.ExecuteAsync(AsOf, "tester");

        var again = await _service.ExecuteAsync(AsOf, "tester");
        var later = await _service.ExecuteAsync(AsOf.AddYears(1), "tester");

        Assert.Empty(again.Plan.Candidates);
        Assert.Empty(later.Plan.Candidates);
        Assert.Equal(5, await _database.CountAsync("privacy.retention_work_items"));
        Assert.Equal(5, await _database.CountAsync("privacy.retention_run_items"));
        Assert.Equal(3, await _database.CountAsync("privacy.retention_runs"));
    }

    [Fact]
    public async Task ConcurrentRunsCreateEachWorkItemOnce()
    {
        await SeedOneDuePerClassAsync();

        var runs = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => _service.ExecuteAsync(AsOf, "tester")));

        Assert.Equal(5, runs.Sum(run => run.Plan.Candidates.Count));
        Assert.Equal(5, await _database.CountAsync("privacy.retention_work_items"));
    }

    [Fact]
    public async Task ALegalHoldKeepsARecordOutOfBothThePlanAndTheRun()
    {
        var due = await SeedOneDuePerClassAsync();
        var actor = Guid.NewGuid();
        var hold = await _service.PlaceHoldAsync(RetentionClass.OrderNotes, due.OrderDue, "Dava dosyasi", actor);

        var plan = await _service.PlanAsync(AsOf);
        var run = await _service.ExecuteAsync(AsOf, "tester");

        Assert.DoesNotContain((RetentionClass.OrderNotes, due.OrderDue), Keys(plan));
        Assert.Equal(Keys(plan), Keys(run.Plan));
        Assert.Equal(4, await _database.CountAsync("privacy.retention_work_items"));

        await _service.ReleaseHoldAsync(hold, actor);
        var released = await _service.ExecuteAsync(AsOf, "tester");
        Assert.Equal([(RetentionClass.OrderNotes, due.OrderDue)], Keys(released.Plan));
    }

    [Fact]
    public async Task ARecordNamedByTheCallerIsHeldToo()
    {
        var due = await SeedOneDuePerClassAsync();

        var plan = await _service.PlanAsync(AsOf, new HashSet<Guid> { due.OrderDue, due.StaffDue });

        Assert.Equal(3, plan.Candidates.Count);
        Assert.DoesNotContain(plan.Candidates, candidate => candidate.SubjectId == due.OrderDue || candidate.SubjectId == due.StaffDue);
    }

    [Fact]
    public async Task TheDatabaseRefusesAWorkItemForAHeldRecordAndItsCompletionOnceHeld()
    {
        var due = await SeedOneDuePerClassAsync();
        var actor = Guid.NewGuid();
        var run = await _service.ExecuteAsync(AsOf, "tester");

        var hold = await _service.PlaceHoldAsync(RetentionClass.StaffAccount, due.StaffDue, "Is davasi", actor);

        Assert.DoesNotContain(await _service.PendingAsync(RetentionClass.StaffAccount, 10), item => item.SubjectId == due.StaffDue);
        await Assert.ThrowsAsync<RetentionSubjectHeldException>(
            () => _service.CompleteAsync(RetentionClass.StaffAccount, [due.StaffDue], "tester"));
        Assert.Equal("Pending", await _database.ScalarAsync<string>($"SELECT status FROM privacy.retention_work_items WHERE subject_id = '{due.StaffDue}'"));

        var held = await _database.ScalarAsync<Guid>("SELECT gen_random_uuid()");
        await _service.PlaceHoldAsync(RetentionClass.Supplier, held, "Denetim", actor);
        Assert.Equal("23000", await SqlStateOfAsync(() => _database.ExecuteAsync(
            "INSERT INTO privacy.retention_work_items (data_class, subject_id, run_id, policy_version, due_since) VALUES ('Supplier', @subject, @run, 2, now())",
            ("subject", held), ("run", run.RunId))));

        await _service.ReleaseHoldAsync(hold, actor);
        Assert.Equal(1, await _service.CompleteAsync(RetentionClass.StaffAccount, [due.StaffDue], "tester"));
    }

    [Fact]
    public async Task HoldsAreUniquePerRecordCanOnlyBeReleasedOnceAndNeverEdited()
    {
        var actor = Guid.NewGuid();
        var subject = Guid.NewGuid();
        var hold = await _service.PlaceHoldAsync(RetentionClass.CustomerProfile, subject, "Dava", actor);

        await Assert.ThrowsAsync<RetentionHoldAlreadyActiveException>(
            () => _service.PlaceHoldAsync(RetentionClass.CustomerProfile, subject, "Baska dava", actor));
        Assert.Equal("23000", await SqlStateOfAsync(() => _database.ExecuteAsync("UPDATE privacy.legal_holds SET reason = 'x'")));
        Assert.Equal("23000", await SqlStateOfAsync(() => _database.ExecuteAsync("DELETE FROM privacy.legal_holds")));

        await _service.ReleaseHoldAsync(hold, actor);
        await Assert.ThrowsAsync<RetentionHoldNotFoundException>(() => _service.ReleaseHoldAsync(hold, actor));
        await _service.PlaceHoldAsync(RetentionClass.CustomerProfile, subject, "Yeni dava", actor);
    }

    [Fact]
    public async Task ANewPolicyVersionChangesTheWindowsWhileAnIncompleteOneIsRefused()
    {
        var due = await SeedOneDuePerClassAsync();
        var actor = Guid.NewGuid();
        var years = new Dictionary<RetentionClass, int>
        {
            [RetentionClass.StaffAccount] = 3,
            [RetentionClass.OrderNotes] = 5,
            [RetentionClass.ReservationReason] = 5,
            [RetentionClass.CustomerProfile] = 2,
            [RetentionClass.InvoicedCustomerProfile] = 10,
            [RetentionClass.Supplier] = 10,
            [RetentionClass.BillFreeText] = 5,
            [RetentionClass.CashFreeText] = 5,
            [RetentionClass.TableMovementReason] = 5,
        };

        var version = await _service.PublishPolicyAsync(years, "Staff window extended", actor);
        var plan = await _service.PlanAsync(AsOf);

        Assert.Equal(5, version);
        Assert.Equal(5, plan.PolicyVersion);
        Assert.DoesNotContain((RetentionClass.StaffAccount, due.StaffDue), Keys(plan));
        Assert.Equal(4, plan.Candidates.Count);

        years.Remove(RetentionClass.Supplier);
        await Assert.ThrowsAsync<RetentionPolicyIncompleteException>(() => _service.PublishPolicyAsync(years, "Missing", actor));
        Assert.Equal("23514", await SqlStateOfAsync(() => _database.ExecuteAsync(
            "INSERT INTO privacy.retention_policies (policy_version, note) VALUES (6, 'no rules')")));
        Assert.Equal(5, await _database.ScalarAsync<int>("SELECT max(policy_version) FROM privacy.retention_policies"));
        Assert.Equal("23000", await SqlStateOfAsync(() => _database.ExecuteAsync("UPDATE privacy.retention_rules SET retention_years = 1")));
    }

    [Fact]
    public async Task WorkItemsMoveOnlyFromPendingToDoneAndTheAuditRowsAreImmutable()
    {
        var due = await SeedOneDuePerClassAsync();
        await _service.ExecuteAsync(AsOf, "tester");

        var pending = await _service.PendingAsync(RetentionClass.OrderNotes, 10);
        Assert.Equal([due.OrderDue], pending.Select(item => item.SubjectId));
        Assert.Equal(1, await _service.CompleteAsync(RetentionClass.OrderNotes, [due.OrderDue], "tester"));
        Assert.Equal(0, await _service.CompleteAsync(RetentionClass.OrderNotes, [due.OrderDue], "tester"));
        Assert.Empty(await _service.PendingAsync(RetentionClass.OrderNotes, 10));

        Assert.Equal("23000", await SqlStateOfAsync(() => _database.ExecuteAsync(
            "UPDATE privacy.retention_work_items SET status = 'Pending', completed_at = NULL, completed_by = NULL WHERE data_class = 'OrderNotes'")));
        Assert.Equal("23000", await SqlStateOfAsync(() => _database.ExecuteAsync("DELETE FROM privacy.retention_work_items")));
        Assert.Equal("23000", await SqlStateOfAsync(() => _database.ExecuteAsync("DELETE FROM privacy.retention_run_items")));
        Assert.Equal("23000", await SqlStateOfAsync(() => _database.ExecuteAsync("UPDATE privacy.retention_runs SET requested_by = 'x'")));
    }

    private static async Task<string?> SqlStateOfAsync(Func<Task> action)
        => (await Assert.ThrowsAsync<PostgresException>(action)).SqlState;
}
