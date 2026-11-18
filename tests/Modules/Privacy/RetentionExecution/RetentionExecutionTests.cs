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

        Assert.Equal(3, plan.PolicyVersion);
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
        Assert.Equal(5, await _database.ScalarAsync<int>($"SELECT count(*)::int FROM privacy.retention_work_items WHERE run_id = '{run.RunId}' AND policy_version = 3 AND status = 'Pending'"));
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
        };

        var version = await _service.PublishPolicyAsync(years, "Staff window extended", actor);
        var plan = await _service.PlanAsync(AsOf);

        Assert.Equal(4, version);
        Assert.Equal(4, plan.PolicyVersion);
        Assert.DoesNotContain((RetentionClass.StaffAccount, due.StaffDue), Keys(plan));
        Assert.Equal(4, plan.Candidates.Count);

        years.Remove(RetentionClass.Supplier);
        await Assert.ThrowsAsync<RetentionPolicyIncompleteException>(() => _service.PublishPolicyAsync(years, "Missing", actor));
        Assert.Equal("23514", await SqlStateOfAsync(() => _database.ExecuteAsync(
            "INSERT INTO privacy.retention_policies (policy_version, note) VALUES (5, 'no rules')")));
        Assert.Equal(4, await _database.ScalarAsync<int>("SELECT max(policy_version) FROM privacy.retention_policies"));
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
