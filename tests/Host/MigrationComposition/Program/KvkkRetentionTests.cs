using ALKAROS.Host.Composition;
using ALKAROS.Host.Tests.Fixtures;
using ALKAROS.Privacy.RetentionExecution;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Tests.Program;

[Collection("Host database password environment")]
public sealed class KvkkRetentionTests : IAsyncLifetime
{
    private const string Marker = "[anonymized]";

    private readonly TestDatabase _database = new();
    private NpgsqlDataSource? _dataSource;
    private string? _originalPassword;

    public async Task InitializeAsync()
    {
        _originalPassword = Environment.GetEnvironmentVariable("ALKAROS_DB_PASSWORD");
        await _database.InitializeAsync();

        var root = FindRepositoryRoot();
        var exit = HostComposition.Run(
            new HostCompositionOptions(
                Path.Combine(root, "database", "MigrationComposition", "order.json"),
                Path.Combine(root, "database", "migrations"),
                _database.PsqlOptions),
            TextWriter.Null);
        Assert.Equal(HostExitCode.Success, exit);

        _dataSource = NpgsqlDataSource.Create(ConnectionString());
        Environment.SetEnvironmentVariable("ALKAROS_DB_PASSWORD", _database.PsqlOptions.Password);
    }

    public async Task DisposeAsync()
    {
        Environment.SetEnvironmentVariable("ALKAROS_DB_PASSWORD", _originalPassword);
        if (_dataSource is not null)
            await _dataSource.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task DryRunReportsCountsButChangesNothingThenApplyAnonymizesAndIsIdempotent()
    {
        var oldStaff = await SeedUserAsync(active: false, updatedAgoDays: 800);
        var recentStaff = await SeedUserAsync(active: false, updatedAgoDays: 30);
        var activeStaff = await SeedUserAsync(active: true, updatedAgoDays: 800);

        var productId = await SeedProductAsync();
        var oldOrder = await SeedOrderAsync(productId, status: "Completed", createdAgoYears: 6, note: "Ahmet Bey, penceresiz masa");
        var recentOrder = await SeedOrderAsync(productId, status: "Completed", createdAgoYears: 1, note: "yakın tarih notu");
        var oldDraft = await SeedOrderAsync(productId, status: "Draft", createdAgoYears: 6, note: "taslak notu");
        var heldOrder = await SeedOrderAsync(productId, status: "Completed", createdAgoYears: 6, note: "davalı - saklanacak");

        await SeedHandoffNoteAsync(oldStaff, activeStaff, "Ahmet Bey şikayetçi");
        await SeedHandoffNoteAsync(activeStaff, oldStaff, "Teşekkürler");
        await SeedHandoffNoteAsync(recentStaff, activeStaff, "Masa 4 tatlı bekliyor");
        await SeedPushSubscriptionAsync(oldStaff, "old-staff");
        await SeedPushSubscriptionAsync(activeStaff, "active-staff");

        var tableId = await SeedTableAsync();
        await SeedReservationAsync(tableId, status: "Expired", reservedAgoYears: 6, reason: "Mehmet Aile - 6 kişi");
        await SeedReservationAsync(tableId, status: "Active", reservedAgoYears: 0, reason: "aktif rezervasyon");

        var oldCustomer = await SeedOldCustomerAndSupplierAsync();

        var excludeFile = Path.Combine(Path.GetTempPath(), $"kvkk-hold-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(excludeFile, $"# legal hold\n{heldOrder}\n");

        try
        {
            // --- dry run: reports counts, changes nothing ---
            using var dryOut = new StringWriter();
            var originalOut = Console.Out;
            int dryExit;
            try
            {
                Console.SetOut(dryOut);
                dryExit = ALKAROS.Host.Program.Main(
                    ["kvkk-retention", "--db-url", _database.Url, "--exclude-order-ids-file", excludeFile]);
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            Assert.Equal((int)HostExitCode.Success, dryExit);
            var dryLine = dryOut.ToString();
            Assert.Contains("staff=1", dryLine, StringComparison.Ordinal);
            Assert.Contains("order_notes=1", dryLine, StringComparison.Ordinal);
            Assert.Contains("item_notes=1", dryLine, StringComparison.Ordinal);
            Assert.Contains("reservation_reasons=1", dryLine, StringComparison.Ordinal);
            Assert.Contains("apply=false", dryLine, StringComparison.Ordinal);
            Assert.Contains("customers=1 suppliers=1 bill_texts=0 cash_texts=0 table_texts=0 policy=4", dryLine, StringComparison.Ordinal);
            Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM privacy.retention_work_items;"));

            Assert.NotEqual(Marker, await ScalarAsync<string>(
                "SELECT notes FROM orders.orders WHERE order_id = @id;", ("id", oldOrder)));
            Assert.NotEqual("anon-" + oldStaff.ToString()[..8], await ScalarAsync<string>(
                "SELECT username FROM identity.users WHERE user_id = @id;", ("id", oldStaff)));

            // --- apply ---
            using var applyErr = new StringWriter();
            var originalErr = Console.Error;
            int applyExit;
            try
            {
                Console.SetError(applyErr);
                applyExit = ALKAROS.Host.Program.Main(
                    ["kvkk-retention", "--db-url", _database.Url, "--apply", "--exclude-order-ids-file", excludeFile]);
            }
            finally
            {
                Console.SetError(originalErr);
            }
            Assert.True(applyExit == (int)HostExitCode.Success, $"apply failed: {applyErr}");

            // old rows anonymized
            Assert.Equal(Marker, await ScalarAsync<string>(
                "SELECT notes FROM orders.orders WHERE order_id = @id;", ("id", oldOrder)));
            Assert.Equal(Marker, await ScalarAsync<string>(
                "SELECT notes FROM orders.order_items WHERE order_id = @id;", ("id", oldOrder)));
            Assert.Equal(Marker, await ScalarAsync<string>(
                "SELECT display_name FROM identity.users WHERE user_id = @id;", ("id", oldStaff)));
            Assert.True(await ScalarAsync<bool>(
                "SELECT email IS NULL AND phone IS NULL FROM identity.users WHERE user_id = @id;", ("id", oldStaff)));
            Assert.True(await ScalarAsync<bool>(
                "SELECT pin_hash IS NULL FROM identity.users WHERE user_id = @id;", ("id", oldStaff)));
            Assert.Equal(2L, await ScalarAsync<long>(
                "SELECT count(*) FROM notifications.serving_handoff_notes WHERE note = @m;", ("m", Marker)));
            Assert.Equal(0L, await ScalarAsync<long>(
                "SELECT count(*) FROM notifications.push_subscriptions WHERE user_id = @id;", ("id", oldStaff)));

            // the notes and devices of people who were not anonymized stay
            Assert.Equal("Masa 4 tatlı bekliyor", await ScalarAsync<string>(
                "SELECT note FROM notifications.serving_handoff_notes WHERE from_user_id = @id;", ("id", recentStaff)));
            Assert.Equal(1L, await ScalarAsync<long>(
                "SELECT count(*) FROM notifications.push_subscriptions WHERE user_id = @id;", ("id", activeStaff)));
            Assert.Equal("pin-hash", await ScalarAsync<string>(
                "SELECT pin_hash FROM identity.users WHERE user_id = @id;", ("id", recentStaff)));
            Assert.Equal(1L, await ScalarAsync<long>(
                "SELECT count(*) FROM table_mgmt.table_reservations WHERE reason = @m AND status = 'Expired';", ("m", Marker)));

            // customers and suppliers past their window go through the same store-by-store workflow
            Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM privacy.retention_work_items WHERE status = 'Pending';"));
            Assert.Equal(5L, await ScalarAsync<long>("SELECT count(*) FROM privacy.retention_work_items WHERE status = 'Done';"));
            Assert.True(await ScalarAsync<bool>(
                "SELECT anonymized FROM customer_data.profiles WHERE customer_id = @id;", ("id", oldCustomer)));
            Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM purchasing.suppliers WHERE name = @m;", ("m", Marker)));

            // untouched: recent, active, wrong-status, legal-held
            Assert.NotEqual(Marker, await ScalarAsync<string>(
                "SELECT notes FROM orders.orders WHERE order_id = @id;", ("id", recentOrder)));
            Assert.NotEqual(Marker, await ScalarAsync<string>(
                "SELECT notes FROM orders.orders WHERE order_id = @id;", ("id", oldDraft)));
            Assert.NotEqual(Marker, await ScalarAsync<string>(
                "SELECT notes FROM orders.orders WHERE order_id = @id;", ("id", heldOrder)));
            Assert.NotEqual(Marker, await ScalarAsync<string>(
                "SELECT display_name FROM identity.users WHERE user_id = @id;", ("id", recentStaff)));
            Assert.NotEqual(Marker, await ScalarAsync<string>(
                "SELECT display_name FROM identity.users WHERE user_id = @id;", ("id", activeStaff)));
            Assert.Equal(1L, await ScalarAsync<long>(
                "SELECT count(*) FROM table_mgmt.table_reservations WHERE reason <> @m;", ("m", Marker)));

            // --- second apply: idempotent, everything reports zero ---
            using var reOut = new StringWriter();
            var originalOut2 = Console.Out;
            try
            {
                Console.SetOut(reOut);
                var reExit = ALKAROS.Host.Program.Main(
                    ["kvkk-retention", "--db-url", _database.Url, "--apply", "--exclude-order-ids-file", excludeFile]);
                Assert.Equal((int)HostExitCode.Success, reExit);
            }
            finally
            {
                Console.SetOut(originalOut2);
            }

            var reLine = reOut.ToString();
            Assert.Contains("staff=0 order_notes=0 item_notes=0 reservation_reasons=0", reLine, StringComparison.Ordinal);
            Assert.Contains("blocked=0 failed=0", reLine, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(excludeFile);
        }
    }

    [Fact]
    public async Task AnApplyFinishesWorkItemsThatWereQueuedButNeverScrubbed()
    {
        var oldStaff = await SeedUserAsync(active: false, updatedAgoDays: 800);
        var order = await SeedOrderAsync(await SeedProductAsync(), status: "Completed", createdAgoYears: 6, note: "Ahmet Bey");
        var queued = await new PostgresRetentionExecutionService(_dataSource!).ExecuteAsync(DateTimeOffset.UtcNow, "interrupted-run");
        Assert.Equal(2, queued.Plan.Candidates.Count);
        Assert.NotEqual(Marker, await ScalarAsync<string>("SELECT notes FROM orders.orders WHERE order_id = @id;", ("id", order)));

        using var applyOut = new StringWriter();
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(applyOut);
            Assert.Equal((int)HostExitCode.Success, ALKAROS.Host.Program.Main(["kvkk-retention", "--db-url", _database.Url, "--apply"]));
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Contains("staff=1 order_notes=1 item_notes=1 reservation_reasons=0", applyOut.ToString(), StringComparison.Ordinal);
        Assert.Equal(Marker, await ScalarAsync<string>("SELECT notes FROM orders.orders WHERE order_id = @id;", ("id", order)));
        Assert.Equal(Marker, await ScalarAsync<string>("SELECT display_name FROM identity.users WHERE user_id = @id;", ("id", oldStaff)));
        Assert.Equal(2L, await ScalarAsync<long>("SELECT count(*) FROM privacy.retention_work_items WHERE status = 'Done';"));
    }

    [Fact]
    public async Task CustomerAndSupplierAnonymizationKeepsLegalIdentifiersAndTheLedgerAndWaitsForAnOpenBalance()
    {
        var customerId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO customer_data.profiles (customer_id, envelope_bytes, created_at) VALUES (@c, ''::bytea, now() - interval '12 years');
            INSERT INTO customer_data.anonymization_requests (id, customer_id, status, requested_at, blocked_reason) VALUES (gen_random_uuid(), @c, 'RetentionBlocked', now(), 'open balance');
            INSERT INTO customer_account.account_transactions (id, customer_id, transaction_type, amount, source_reference_type, source_reference_id, occurred_at)
            VALUES (@tx, @c, 'Charge', 10, 'Test', @tx, now() - interval '10 years 6 months');
            INSERT INTO customer_account.balances (customer_id, current_balance, last_transaction_at, updated_at)
            VALUES (@c, 0, now() - interval '10 years 6 months', now() - interval '10 years 6 months')
            ON CONFLICT (customer_id) DO UPDATE SET current_balance = 0;
            INSERT INTO purchasing.suppliers (supplier_id, code, name, tax_number, tax_office, phone, email, created_at, updated_at)
            VALUES (@s, @code, 'Ahmet Gida', '1234567890', 'Kadikoy', '05001112233', 'a@b.com', now() - interval '12 years', now() - interval '12 years');
            """,
            ("c", customerId), ("tx", Guid.NewGuid()), ("s", supplierId), ("code", $"S-{supplierId:N}"[..16]));
        await new PostgresRetentionExecutionService(_dataSource!).ExecuteAsync(DateTimeOffset.UtcNow, "queued");
        await ExecuteAsync("UPDATE customer_account.balances SET current_balance = 25 WHERE customer_id = @c;", ("c", customerId));

        var held = Apply();

        Assert.Contains("blocked=1 failed=0", held, StringComparison.Ordinal);
        Assert.False(await ScalarAsync<bool>("SELECT anonymized FROM customer_data.profiles WHERE customer_id = @c;", ("c", customerId)));
        Assert.True(await ScalarAsync<bool>(
            "SELECT name = @m AND phone IS NULL AND email IS NULL AND tax_number = '1234567890' AND tax_office = 'Kadikoy' FROM purchasing.suppliers WHERE supplier_id = @s;",
            ("m", Marker), ("s", supplierId)));

        await ExecuteAsync("UPDATE customer_account.balances SET current_balance = 0 WHERE customer_id = @c;", ("c", customerId));
        var released = Apply();

        Assert.Contains("blocked=0 failed=0", released, StringComparison.Ordinal);
        Assert.True(await ScalarAsync<bool>("SELECT anonymized FROM customer_data.profiles WHERE customer_id = @c;", ("c", customerId)));
        Assert.Equal(1L, await ScalarAsync<long>(
            "SELECT count(*) FROM customer_data.anonymization_requests WHERE customer_id = @c AND status = 'Anonymized';", ("c", customerId)));
        Assert.Equal(1L, await ScalarAsync<long>(
            "SELECT count(*) FROM customer_account.account_transactions WHERE customer_id = @c AND amount = 10;", ("c", customerId)));
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM privacy.retention_work_items WHERE status = 'Pending';"));
    }

    [Fact]
    public async Task AProfileWithoutAnInvoiceIsAnonymizedAfterTwoYearsAndOneTiedToAnInvoiceOnlyAfterTen()
    {
        var plain = Guid.NewGuid();
        var recent = Guid.NewGuid();
        var invoicedOld = Guid.NewGuid();
        var invoicedRecent = Guid.NewGuid();
        await ExecuteAsync(
            """
            SET LOCAL session_replication_role = replica;
            INSERT INTO customer_data.profiles (customer_id, envelope_bytes, created_at) VALUES
                (@plain, '\x00'::bytea, now() - interval '5 years'), (@recent, '\x00'::bytea, now() - interval '5 years'),
                (@old, '\x00'::bytea, now() - interval '12 years'), (@new, '\x00'::bytea, now() - interval '12 years');
            INSERT INTO customer_account.account_transactions (id, customer_id, transaction_type, amount, source_reference_type, source_reference_id, occurred_at)
            VALUES (gen_random_uuid(), @plain, 'Charge', 10, 'Test', gen_random_uuid(), now() - interval '2 years 3 months'),
                   (gen_random_uuid(), @recent, 'Charge', 10, 'Test', gen_random_uuid(), now() - interval '1 year 9 months');
            INSERT INTO invoicing.invoices
                (invoice_id, source_set_id, period_id, customer_id, profile, ubl_profile_id, invoice_type_code, currency_code,
                 issue_date, status, buyer_envelope, line_extension_amount, tax_total, payable_amount, created_at, created_by)
            VALUES (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(), @old, 'EFatura', 'TEMELFATURA', 'SATIS', 'TRY',
                    (now() - interval '10 years 3 months')::date, 'Draft', '\x00'::bytea, 100, 0, 100, now() - interval '10 years 3 months', gen_random_uuid()),
                   (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(), @new, 'EFatura', 'TEMELFATURA', 'SATIS', 'TRY',
                    (now() - interval '3 years')::date, 'Draft', '\x00'::bytea, 100, 0, 100, now() - interval '3 years', gen_random_uuid());
            """,
            ("plain", plain), ("recent", recent), ("old", invoicedOld), ("new", invoicedRecent));

        Assert.Contains("failed=0", Apply(), StringComparison.Ordinal);

        Assert.True(await ScalarAsync<bool>("SELECT anonymized FROM customer_data.profiles WHERE customer_id = @c;", ("c", plain)));
        Assert.False(await ScalarAsync<bool>("SELECT anonymized FROM customer_data.profiles WHERE customer_id = @c;", ("c", recent)));
        Assert.True(await ScalarAsync<bool>("SELECT anonymized FROM customer_data.profiles WHERE customer_id = @c;", ("c", invoicedOld)));
        Assert.False(await ScalarAsync<bool>("SELECT anonymized FROM customer_data.profiles WHERE customer_id = @c;", ("c", invoicedRecent)));
    }

    [Fact]
    public async Task AProfileQueuedWithoutAnInvoiceIsLeftAloneAndNotCountedBlockedWhenAnInvoiceAppearsBeforeTheApply()
    {
        var customerId = Guid.NewGuid();
        await ExecuteAsync(
            """
            SET LOCAL session_replication_role = replica;
            INSERT INTO customer_data.profiles (customer_id, envelope_bytes, created_at) VALUES (@c, '\x00'::bytea, now() - interval '5 years');
            INSERT INTO customer_account.account_transactions (id, customer_id, transaction_type, amount, source_reference_type, source_reference_id, occurred_at)
            VALUES (gen_random_uuid(), @c, 'Charge', 10, 'Test', gen_random_uuid(), now() - interval '2 years 3 months');
            """,
            ("c", customerId));
        await new PostgresRetentionExecutionService(_dataSource!).ExecuteAsync(DateTimeOffset.UtcNow, "queued");
        await ExecuteAsync(
            """
            SET LOCAL session_replication_role = replica;
            INSERT INTO invoicing.invoices
                (invoice_id, source_set_id, period_id, customer_id, profile, ubl_profile_id, invoice_type_code, currency_code,
                 issue_date, status, buyer_envelope, line_extension_amount, tax_total, payable_amount, created_at, created_by)
            VALUES (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(), @c, 'EFatura', 'TEMELFATURA', 'SATIS', 'TRY',
                    now()::date, 'Draft', '\x00'::bytea, 100, 0, 100, now(), gen_random_uuid());
            """,
            ("c", customerId));

        var result = Apply();

        Assert.Contains("blocked=0 failed=0", result, StringComparison.Ordinal);
        Assert.False(await ScalarAsync<bool>("SELECT anonymized FROM customer_data.profiles WHERE customer_id = @c;", ("c", customerId)));
        Assert.Equal(
            "Pending",
            await ScalarAsync<string>("SELECT status FROM privacy.retention_work_items WHERE subject_id = @c;", ("c", customerId)));
    }

    [Fact]
    public async Task AProfileThatGetsAnInvoiceAfterItWasReadAsPendingIsStillNotAnonymized()
    {
        var customerId = Guid.NewGuid();
        await ExecuteAsync(
            """
            SET LOCAL session_replication_role = replica;
            INSERT INTO customer_data.profiles (customer_id, envelope_bytes, created_at) VALUES (@c, '\x00'::bytea, now() - interval '5 years');
            INSERT INTO customer_account.account_transactions (id, customer_id, transaction_type, amount, source_reference_type, source_reference_id, occurred_at)
            VALUES (gen_random_uuid(), @c, 'Charge', 10, 'Test', gen_random_uuid(), now() - interval '2 years 3 months');
            """,
            ("c", customerId));
        await new PostgresRetentionExecutionService(_dataSource!).ExecuteAsync(DateTimeOffset.UtcNow, "queued");
        // The invoice lands between the pending read and the anonymization: the job row is the first thing the workflow writes.
        await ExecuteAsync(
            """
            CREATE FUNCTION public.late_invoice() RETURNS trigger LANGUAGE plpgsql AS $f$
            BEGIN
                SET LOCAL session_replication_role = replica;
                INSERT INTO invoicing.invoices
                    (invoice_id, source_set_id, period_id, customer_id, profile, ubl_profile_id, invoice_type_code, currency_code,
                     issue_date, status, buyer_envelope, line_extension_amount, tax_total, payable_amount, created_at, created_by)
                VALUES (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(), NEW.subject_id, 'EFatura', 'TEMELFATURA', 'SATIS', 'TRY',
                        now()::date, 'Draft', '\x00'::bytea, 100, 0, 100, now(), gen_random_uuid());
                SET LOCAL session_replication_role = origin;
                RETURN NEW;
            END $f$;
            CREATE TRIGGER late_invoice AFTER INSERT ON privacy.anonymization_jobs FOR EACH ROW EXECUTE FUNCTION public.late_invoice();
            """);

        var result = Apply();

        Assert.Contains("blocked=1 failed=0", result, StringComparison.Ordinal);
        Assert.False(await ScalarAsync<bool>("SELECT anonymized FROM customer_data.profiles WHERE customer_id = @c;", ("c", customerId)));
    }

    [Fact]
    public async Task StatusHistoryReasonsOfAnOldClosedOrderAreScrubbedAndEverythingElseStays()
    {
        var productId = await SeedProductAsync();
        var oldOrder = await SeedOrderAsync(productId, status: "Completed", createdAgoYears: 6, note: Marker);
        var recentOrder = await SeedOrderAsync(productId, status: "Completed", createdAgoYears: 1, note: Marker);
        var oldDraft = await SeedOrderAsync(productId, status: "Draft", createdAgoYears: 6, note: Marker);
        var history = new Dictionary<Guid, Guid>();
        foreach (var order in new[] { oldOrder, recentOrder, oldDraft })
        {
            history[order] = Guid.NewGuid();
            await ExecuteAsync(
                """
                INSERT INTO orders.order_status_history (order_status_history_id, order_id, old_status, new_status, reason, changed_by, changed_at)
                VALUES (@h, @o, 'Active', 'Cancelled', 'Ahmet Bey vazgecti', @by, now() - interval '6 years');
                """,
                ("h", history[order]), ("o", order), ("by", Guid.NewGuid()));
        }

        Assert.Contains("failed=0", Apply(), StringComparison.Ordinal);

        const string Row = "SELECT reason || '|' || old_status || '|' || new_status || '|' || changed_by || '|' || changed_at FROM orders.order_status_history WHERE order_status_history_id = @h;";
        var scrubbed = await ScalarAsync<string>(Row, ("h", history[oldOrder]));
        Assert.StartsWith(Marker + "|Active|Cancelled|", scrubbed, StringComparison.Ordinal);
        Assert.StartsWith("Ahmet Bey vazgecti|", await ScalarAsync<string>(Row, ("h", history[recentOrder])), StringComparison.Ordinal);
        Assert.StartsWith("Ahmet Bey vazgecti|", await ScalarAsync<string>(Row, ("h", history[oldDraft])), StringComparison.Ordinal);

        Assert.Contains("order_notes=0 item_notes=0", Apply(), StringComparison.Ordinal);
        Assert.Equal(scrubbed, await ScalarAsync<string>(Row, ("h", history[oldOrder])));
    }

    [Fact]
    public async Task EveryBillFreeTextOfAnOldClosedBillIsScrubbedWhileAmountsAndStatesStayAndASecondRunChangesNothing()
    {
        var old = await SeedBillAsync("Paid", 6, "Ali Bey vazgecti");
        var cancelled = await SeedBillAsync("Cancelled", 6, "Veli Bey iptal etti", decisionNote: null);
        var recent = await SeedBillAsync("Paid", 1, "yakin iptal");
        var open = await SeedBillAsync("Open", 6, "acik adisyon");
        var pending = await SeedBillAsync("Paid", 6, "iade bekliyor");
        var openRefund = Guid.NewGuid();
        await ExecuteAsync(
            """
            SET LOCAL session_replication_role = replica;
            INSERT INTO payments.refund_requests (refund_request_id, bill_id, payment_id, amount, method, reason, status, requested_by, requested_at, row_version)
            VALUES (@r, @b, gen_random_uuid(), 5, 'Cash', 'Ali Bey ister', 'Requested', gen_random_uuid(), now() - interval '6 years', 1);
            """,
            ("r", openRefund), ("b", pending.BillId));

        var before = new Dictionary<Guid, string>();
        foreach (var bill in new[] { old, cancelled, recent, open })
            before[bill.BillId] = await BillAmountsAsync(bill);

        var first = Apply();
        Assert.Contains("bill_texts=2", first, StringComparison.Ordinal);

        foreach (var bill in new[] { old, cancelled })
        {
            Assert.Equal(Marker, await ScalarAsync<string>("SELECT notes FROM billing.bill_items WHERE bill_item_id = @id;", ("id", bill.ItemId)));
            Assert.Equal(Marker, await ScalarAsync<string>("SELECT notes FROM billing.bill_adjustments WHERE bill_adjustment_id = @id;", ("id", bill.AdjustmentId)));
            Assert.Equal(Marker, await ScalarAsync<string>("SELECT cancellation_reason FROM billing.bills WHERE bill_id = @id;", ("id", bill.BillId)));
            Assert.Equal(Marker, await ScalarAsync<string>("SELECT reason FROM payments.refund_requests WHERE refund_request_id = @id;", ("id", bill.RefundId)));
            Assert.Equal(bill == cancelled, await ScalarAsync<bool>("SELECT decision_note IS NULL FROM payments.refund_requests WHERE refund_request_id = @id;", ("id", bill.RefundId)));
            if (bill != cancelled)
                Assert.Equal(Marker, await ScalarAsync<string>("SELECT decision_note FROM payments.refund_requests WHERE refund_request_id = @id;", ("id", bill.RefundId)));
            Assert.Equal(before[bill.BillId], await BillAmountsAsync(bill));
        }

        foreach (var bill in new[] { recent, open })
        {
            Assert.NotEqual(Marker, await ScalarAsync<string>("SELECT notes FROM billing.bill_items WHERE bill_item_id = @id;", ("id", bill.ItemId)));
            Assert.NotEqual(Marker, await ScalarAsync<string>("SELECT notes FROM billing.bill_adjustments WHERE bill_adjustment_id = @id;", ("id", bill.AdjustmentId)));
            Assert.NotEqual(Marker, await ScalarAsync<string>("SELECT cancellation_reason FROM billing.bills WHERE bill_id = @id;", ("id", bill.BillId)));
            Assert.NotEqual(Marker, await ScalarAsync<string>("SELECT reason FROM payments.refund_requests WHERE refund_request_id = @id;", ("id", bill.RefundId)));
            Assert.NotEqual(Marker, await ScalarAsync<string>("SELECT decision_note FROM payments.refund_requests WHERE refund_request_id = @id;", ("id", bill.RefundId)));
            Assert.Equal(before[bill.BillId], await BillAmountsAsync(bill));
        }

        Assert.Equal("Ali Bey ister", await ScalarAsync<string>("SELECT reason FROM payments.refund_requests WHERE refund_request_id = @id;", ("id", openRefund)));
        Assert.Equal("iade bekliyor", await ScalarAsync<string>("SELECT cancellation_reason FROM billing.bills WHERE bill_id = @id;", ("id", pending.BillId)));

        var second = Apply();
        Assert.Contains("bill_texts=0", second, StringComparison.Ordinal);
        Assert.Contains("blocked=0 failed=0", second, StringComparison.Ordinal);
        Assert.Equal(before[old.BillId], await BillAmountsAsync(old));

        await ExecuteAsync("UPDATE payments.refund_requests SET status = 'Rejected' WHERE refund_request_id = @r;", ("r", openRefund));
        Assert.Contains("bill_texts=1", Apply(), StringComparison.Ordinal);
        Assert.Equal(Marker, await ScalarAsync<string>("SELECT cancellation_reason FROM billing.bills WHERE bill_id = @id;", ("id", pending.BillId)));
        Assert.Equal(Marker, await ScalarAsync<string>("SELECT reason FROM payments.refund_requests WHERE refund_request_id = @id;", ("id", openRefund)));
    }

    [Fact]
    public async Task ABillUnderALegalHoldKeepsItsTexts()
    {
        var held = await SeedBillAsync("Paid", 6, "dava konusu");
        await ExecuteAsync(
            "INSERT INTO privacy.legal_holds (hold_id, data_class, subject_id, reason, placed_by) VALUES (gen_random_uuid(), 'BillFreeText', @b, 'Vergi incelemesi', gen_random_uuid());",
            ("b", held.BillId));

        Assert.Contains("bill_texts=0", Apply(), StringComparison.Ordinal);
        Assert.Equal("dava konusu", await ScalarAsync<string>("SELECT cancellation_reason FROM billing.bills WHERE bill_id = @id;", ("id", held.BillId)));
    }

    [Fact]
    public async Task ABillMadeOfAnExcludedOrderKeepsItsTextsWhetherLinkedByTheBillOrByAnItem()
    {
        var productId = await SeedProductAsync();
        var heldOrder = await SeedOrderAsync(productId, status: "Completed", createdAgoYears: 6, note: Marker);
        var byBill = await SeedBillAsync("Paid", 6, "dava konusu");
        var byItem = await SeedBillAsync("Paid", 6, "dava konusu");
        var free = await SeedBillAsync("Paid", 6, "serbest");
        await ExecuteAsync(
            """
            SET LOCAL session_replication_role = replica;
            UPDATE billing.bills SET order_id = @o WHERE bill_id = @b;
            UPDATE billing.bill_items SET order_item_id = (SELECT order_item_id FROM orders.order_items WHERE order_id = @o) WHERE bill_item_id = @i;
            """,
            ("o", heldOrder), ("b", byBill.BillId), ("i", byItem.ItemId));
        var excludeFile = Path.Combine(Path.GetTempPath(), $"kvkk-hold-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(excludeFile, heldOrder + Environment.NewLine);
        try
        {
            Assert.Contains("bill_texts=1", Apply(excludeFile), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(excludeFile);
        }

        Assert.Equal("dava konusu", await ScalarAsync<string>("SELECT cancellation_reason FROM billing.bills WHERE bill_id = @id;", ("id", byBill.BillId)));
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM billing.bill_items WHERE bill_item_id = @id AND notes <> @m;", ("id", byItem.ItemId), ("m", Marker)));
        Assert.Equal(Marker, await ScalarAsync<string>("SELECT cancellation_reason FROM billing.bills WHERE bill_id = @id;", ("id", free.BillId)));
    }

    [Fact]
    public async Task ARefundRequestOpenedAfterTheBillWasQueuedKeepsItsTextsWhileTheBillIsScrubbed()
    {
        var bill = await SeedBillAsync("Paid", 6, "Ali Bey vazgecti");
        var late = Guid.NewGuid();
        await new PostgresRetentionExecutionService(_dataSource!).ExecuteAsync(DateTimeOffset.UtcNow, "queued");
        await ExecuteAsync(
            """
            CREATE FUNCTION public.late_refund() RETURNS trigger LANGUAGE plpgsql AS $f$
            BEGIN
                SET LOCAL session_replication_role = replica;
                INSERT INTO payments.refund_requests (refund_request_id, bill_id, payment_id, amount, method, reason, status, requested_by, requested_at, row_version)
                VALUES (@@late@@, NEW.subject_id, gen_random_uuid(), 5, 'Cash', 'sonradan acildi', 'AwaitingManualRefund', gen_random_uuid(), now(), 1);
                SET LOCAL session_replication_role = origin;
                RETURN NEW;
            END $f$;
            CREATE TRIGGER late_refund AFTER INSERT ON privacy.anonymization_jobs FOR EACH ROW EXECUTE FUNCTION public.late_refund();
            """.Replace("@@late@@", $"'{late}'", StringComparison.Ordinal));

        Assert.Contains("failed=0", Apply(), StringComparison.Ordinal);

        Assert.Equal(Marker, await ScalarAsync<string>("SELECT cancellation_reason FROM billing.bills WHERE bill_id = @id;", ("id", bill.BillId)));
        Assert.Equal("sonradan acildi", await ScalarAsync<string>("SELECT reason FROM payments.refund_requests WHERE refund_request_id = @id;", ("id", late)));
    }

    private const string SlipLines = "SIPARIS\r\n1     Kuru NOT: fasulye\r\n      + az sut\r\n      NOT: {0}\n      NOT: az\n      NOT: acisiz\n      NOT: [anonymized] sizinti\n---";

    private static string Slip(string note) => SlipLines.Replace("{0}", note, StringComparison.Ordinal);

    private const string ReprintReason = "Ali Bey istedi\nmasa 4";

    private static string Banner(string reason, string slip) => "####\r\nONAYLAYAN: u1\r\nNEDEN: " + reason + "\r\nTEKRAR TARIHI: x\r\n----\r\n" + slip;

    private sealed record SeededKitchen(Guid TicketId, Guid ItemId, Guid PrintedJobId, Guid PendingJobId, Guid ReprintedId, Guid InFlightId, Guid LookalikeJobId);

    private async Task<SeededKitchen> SeedKitchenAsync(Guid orderId, int createdAgoYears, string note = "az pisirin\nAli Bey'in masasi")
    {
        var k = new SeededKitchen(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await ExecuteAsync(
            """
            SET LOCAL session_replication_role = replica;
            INSERT INTO kitchen.kitchen_tickets (id, order_id, ticket_number, station_id, status, created_at, cancelled_at, cancellation_reason)
            VALUES (@t, @o, @tn, 'hot', 'Cancelled', now() - make_interval(years => @ago), now() - make_interval(years => @ago), 'Ali Bey gitti');
            INSERT INTO kitchen.kitchen_ticket_items (id, ticket_id, order_item_id, product_id, product_name_snapshot, quantity, status, notes, created_at, cancelled_at, cancellation_reason)
            VALUES (@i, @t, gen_random_uuid(), gen_random_uuid(), 'Test', 1, 'Cancelled', @note, now() - make_interval(years => @ago), now() - make_interval(years => @ago), 'Ali Bey gitti');
            INSERT INTO kitchen.kitchen_ticket_items (id, ticket_id, order_item_id, product_id, product_name_snapshot, quantity, status, notes, created_at)
            VALUES (gen_random_uuid(), @t, gen_random_uuid(), gen_random_uuid(), 'Test', 1, 'Cancelled', 'az', now() - make_interval(years => @ago));
            INSERT INTO kitchen.print_jobs (id, ticket_id, printer_id, idempotency_key, payload, status, created_at)
            VALUES (@pj, @t, gen_random_uuid(), 'k-' || @pj::text, @payload, 'Printed', now() - make_interval(years => @ago)),
                   (@qj, @t, gen_random_uuid(), 'k-' || @qj::text, @payload, 'Pending', now() - make_interval(years => @ago)),
                   (@lj, @t, gen_random_uuid(), 'k-' || @lj::text, @lookalike, 'Cancelled', now() - make_interval(years => @ago));
            INSERT INTO kitchen.physical_print_deliveries (id, print_job_id, ticket_id, printer_id, status, operator_reason, payload_snapshot, reprint_payload, created_at)
            VALUES (@rd, @pj, @t, gen_random_uuid(), 'Reprinted', @reason, @payload, @banner, now() - make_interval(years => @ago)),
                   (@fd, @qj, @t, gen_random_uuid(), 'InFlight', 'Ali Bey istedi', @payload, NULL, now() - make_interval(years => @ago));
            """,
            ("t", k.TicketId), ("o", orderId), ("tn", $"T-{k.TicketId:N}"[..20]), ("ago", createdAgoYears), ("i", k.ItemId), ("note", note),
            ("pj", k.PrintedJobId), ("qj", k.PendingJobId), ("rd", k.ReprintedId), ("fd", k.InFlightId), ("lj", k.LookalikeJobId),
            ("payload", Slip(note)), ("reason", ReprintReason), ("banner", Banner(ReprintReason, Slip(note))), ("lookalike", "SIPARIS\n      NOT: [anonymized] sizinti\n---"));
        return k;
    }

    private Task<string> KitchenRowsAsync(SeededKitchen k) => ScalarAsync<string>(
        """
        SELECT concat_ws('|',
            (SELECT concat_ws(',', status, created_at, cancelled_at, row_version, cancellation_reason) FROM kitchen.kitchen_tickets WHERE id = @t),
            (SELECT concat_ws(',', notes, cancellation_reason) FROM kitchen.kitchen_ticket_items WHERE id = @i),
            (SELECT concat_ws(',', status, payload) FROM kitchen.print_jobs WHERE id = @pj),
            (SELECT concat_ws(',', status, payload) FROM kitchen.print_jobs WHERE id = @qj),
            (SELECT concat_ws(',', status, operator_reason, payload_snapshot, reprint_payload) FROM kitchen.physical_print_deliveries WHERE id = @rd),
            (SELECT concat_ws(',', status, operator_reason, payload_snapshot) FROM kitchen.physical_print_deliveries WHERE id = @fd));
        """,
        ("t", k.TicketId), ("i", k.ItemId), ("pj", k.PrintedJobId), ("qj", k.PendingJobId), ("rd", k.ReprintedId), ("fd", k.InFlightId));

    [Fact]
    public async Task KitchenTextsOfAnOldClosedOrderAreScrubbedWhileTheRestOfTheSlipAndRunningPrintsStay()
    {
        var productId = await SeedProductAsync();
        var old = await SeedOrderAsync(productId, "Completed", 6, Marker);
        var recent = await SeedOrderAsync(productId, "Completed", 1, Marker);
        var draft = await SeedOrderAsync(productId, "Draft", 6, Marker);
        var oldKitchen = await SeedKitchenAsync(old, 6);
        var recentKitchen = await SeedKitchenAsync(recent, 1);
        var draftKitchen = await SeedKitchenAsync(draft, 6);
        var recentBefore = await KitchenRowsAsync(recentKitchen);
        var draftBefore = await KitchenRowsAsync(draftKitchen);
        var runningBefore = await ScalarAsync<string>(
            "SELECT concat_ws('|', (SELECT payload FROM kitchen.print_jobs WHERE id = @q), (SELECT concat_ws(',', operator_reason, payload_snapshot) FROM kitchen.physical_print_deliveries WHERE id = @f));",
            ("q", oldKitchen.PendingJobId), ("f", oldKitchen.InFlightId));

        Assert.Contains("failed=0", Apply(), StringComparison.Ordinal);
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM privacy.retention_work_items WHERE data_class = 'OrderNotes';"));

        const string ScrubbedSlip = "SIPARIS\r\n1     Kuru NOT: fasulye\r\n      + az sut\r\n      NOT: [anonymized]\n      NOT: [anonymized]\n      NOT: [anonymized]\n      NOT: [anonymized]\n---";
        Assert.Equal(Marker + "," + Marker, await ScalarAsync<string>("SELECT concat_ws(',', notes, cancellation_reason) FROM kitchen.kitchen_ticket_items WHERE id = @i;", ("i", oldKitchen.ItemId)));
        Assert.Equal(Marker, await ScalarAsync<string>("SELECT cancellation_reason FROM kitchen.kitchen_tickets WHERE id = @t;", ("t", oldKitchen.TicketId)));
        Assert.Equal("Cancelled", await ScalarAsync<string>("SELECT status FROM kitchen.kitchen_tickets WHERE id = @t;", ("t", oldKitchen.TicketId)));
        Assert.Equal(ScrubbedSlip, await ScalarAsync<string>("SELECT payload FROM kitchen.print_jobs WHERE id = @j;", ("j", oldKitchen.PrintedJobId)));
        Assert.Equal("SIPARIS\n      NOT: [anonymized]\n---", await ScalarAsync<string>("SELECT payload FROM kitchen.print_jobs WHERE id = @j;", ("j", oldKitchen.LookalikeJobId)));
        Assert.Equal(
            "2,2,1",
            await ScalarAsync<string>(
                "SELECT (SELECT row_version FROM kitchen.print_jobs WHERE id = @j) || ',' || (SELECT row_version FROM kitchen.physical_print_deliveries WHERE id = @d) || ',' || (SELECT row_version FROM kitchen.print_jobs WHERE id = @q);",
                ("j", oldKitchen.PrintedJobId), ("d", oldKitchen.ReprintedId), ("q", oldKitchen.PendingJobId)));
        Assert.Equal(
            Marker + "|" + ScrubbedSlip + "|" + Banner(Marker, ScrubbedSlip),
            await ScalarAsync<string>("SELECT concat_ws('|', operator_reason, payload_snapshot, reprint_payload) FROM kitchen.physical_print_deliveries WHERE id = @d;", ("d", oldKitchen.ReprintedId)));
        Assert.Equal(runningBefore, await ScalarAsync<string>(
            "SELECT concat_ws('|', (SELECT payload FROM kitchen.print_jobs WHERE id = @q), (SELECT concat_ws(',', operator_reason, payload_snapshot) FROM kitchen.physical_print_deliveries WHERE id = @f));",
            ("q", oldKitchen.PendingJobId), ("f", oldKitchen.InFlightId)));
        Assert.Equal(recentBefore, await KitchenRowsAsync(recentKitchen));
        Assert.Equal(draftBefore, await KitchenRowsAsync(draftKitchen));

        var oldAfter = await KitchenRowsAsync(oldKitchen);
        Assert.Contains("failed=0", Apply(), StringComparison.Ordinal);
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM privacy.retention_work_items WHERE data_class = 'OrderNotes';"));
        Assert.Equal(oldAfter, await KitchenRowsAsync(oldKitchen));
    }

    [Fact]
    public async Task AnOrderWhoseOnlyRemainingTextIsOnTheKitchenSideIsScrubbedAndAHeldOrderKeepsItsKitchenTexts()
    {
        var productId = await SeedProductAsync();
        var onlyKitchen = await SeedOrderAsync(productId, "Completed", 6, Marker);
        var held = await SeedOrderAsync(productId, "Completed", 6, Marker);
        var kitchen = await SeedKitchenAsync(onlyKitchen, 6);
        var heldKitchen = await SeedKitchenAsync(held, 6);
        var heldBefore = await KitchenRowsAsync(heldKitchen);
        var excludeFile = Path.Combine(Path.GetTempPath(), $"kvkk-hold-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(excludeFile, held + Environment.NewLine);
        try
        {
            Assert.Contains("failed=0", Apply(excludeFile), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(excludeFile);
        }

        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM privacy.retention_work_items WHERE data_class = 'OrderNotes';"));
        Assert.Equal(Marker, await ScalarAsync<string>("SELECT notes FROM kitchen.kitchen_ticket_items WHERE id = @i;", ("i", kitchen.ItemId)));
        Assert.Equal(heldBefore, await KitchenRowsAsync(heldKitchen));
    }

    private sealed record SeededPayment(Guid PaymentId, Guid HistoryId, Guid ReversalId, Guid ConfirmationId);

    private async Task<SeededPayment> SeedPaymentAsync(Guid billId, string status, int agoYears, string confirmationStatus = "Approved")
    {
        var p = new SeededPayment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await ExecuteAsync(
            """
            SET LOCAL session_replication_role = replica;
            INSERT INTO payments.payments (payment_id, bill_id, status, requested_amount, tendered_amount, approved_amount, initiated_at, created_at, updated_at)
            VALUES (@p, @b, @st::text, 50, CASE WHEN @st::text <> 'Initiated' THEN 50 END, CASE WHEN @st::text IN ('Approved', 'Refunded', 'PartiallyRefunded') THEN 50 END,
                    now() - make_interval(years => @ago), now() - make_interval(years => @ago), now() - make_interval(years => @ago));
            INSERT INTO payments.payment_status_history (payment_status_history_id, payment_id, old_status, new_status, reason, changed_at)
            VALUES (@h, @p, 'Approved', 'Refunded', 'Ali Bey memnun kalmadi', now() - make_interval(years => @ago));
            INSERT INTO payments.payment_reversals (reversal_id, payment_id, reversal_type, amount, reason, refunded_by, idempotency_key, occurred_at)
            VALUES (@r, @p, 'FullRefund', 50, 'Ali Bey memnun kalmadi', gen_random_uuid(), 'k-' || @r::text, now() - make_interval(years => @ago));
            INSERT INTO payments.manual_card_confirmations (confirmation_id, payment_id, bill_id, slip_number, amount, status, requested_by, requested_at, request_note, decided_by, decided_at, decision_note)
            VALUES (@c, @p, @b, 'S-' || @c::text, 50, @cs, gen_random_uuid(), now() - make_interval(years => @ago), 'Ali Bey istedi',
                    CASE WHEN @cs <> 'Pending' THEN gen_random_uuid() END, CASE WHEN @cs <> 'Pending' THEN now() - make_interval(years => @ago) END,
                    CASE WHEN @cs <> 'Pending' THEN 'Ali Bey ile konusuldu' END);
            """,
            ("p", p.PaymentId), ("b", billId), ("st", status), ("ago", agoYears), ("h", p.HistoryId), ("r", p.ReversalId), ("c", p.ConfirmationId), ("cs", confirmationStatus));
        return p;
    }

    private Task<string> PaymentRowsAsync(SeededPayment p) => ScalarAsync<string>(
        """
        SELECT concat_ws('|',
            (SELECT concat_ws(',', status, requested_amount, approved_amount, initiated_at, row_version) FROM payments.payments WHERE payment_id = @p),
            (SELECT concat_ws(',', old_status, new_status, changed_at, reason) FROM payments.payment_status_history WHERE payment_status_history_id = @h),
            (SELECT concat_ws(',', reversal_type, amount, occurred_at, idempotency_key, reason) FROM payments.payment_reversals WHERE reversal_id = @r),
            (SELECT concat_ws(',', slip_number, amount, status, decided_at, row_version, request_note, decision_note) FROM payments.manual_card_confirmations WHERE confirmation_id = @c));
        """,
        ("p", p.PaymentId), ("h", p.HistoryId), ("r", p.ReversalId), ("c", p.ConfirmationId));

    [Fact]
    public async Task PaymentTextsOfAnOldClosedBillAreScrubbedWhileAmountsStatesAndDatesStayAndASecondRunChangesNothing()
    {
        var old = await SeedBillAsync("Paid", 6, "iptal yok");
        var recent = await SeedBillAsync("Paid", 1, "yakin");
        var oldPayment = await SeedPaymentAsync(old.BillId, "Refunded", 6);
        var rejected = await SeedPaymentAsync(old.BillId, "Declined", 6, "Rejected");
        var recentPayment = await SeedPaymentAsync(recent.BillId, "Refunded", 1);
        var recentBefore = await PaymentRowsAsync(recentPayment);
        var amountsBefore = await ScalarAsync<string>(
            "SELECT concat_ws('|', (SELECT concat_ws(',', status, requested_amount, approved_amount, initiated_at, row_version) FROM payments.payments WHERE payment_id = @p), (SELECT concat_ws(',', reversal_type, amount, occurred_at, idempotency_key) FROM payments.payment_reversals WHERE reversal_id = @r));",
            ("p", oldPayment.PaymentId), ("r", oldPayment.ReversalId));

        Assert.Contains("bill_texts=1", Apply(), StringComparison.Ordinal);

        foreach (var payment in new[] { oldPayment, rejected })
        {
            Assert.Equal(Marker, await ScalarAsync<string>("SELECT reason FROM payments.payment_status_history WHERE payment_status_history_id = @h;", ("h", payment.HistoryId)));
            Assert.Equal(Marker, await ScalarAsync<string>("SELECT reason FROM payments.payment_reversals WHERE reversal_id = @r;", ("r", payment.ReversalId)));
            Assert.Equal(Marker + "," + Marker, await ScalarAsync<string>("SELECT concat_ws(',', request_note, decision_note) FROM payments.manual_card_confirmations WHERE confirmation_id = @c;", ("c", payment.ConfirmationId)));
        }

        Assert.Equal(amountsBefore, await ScalarAsync<string>(
            "SELECT concat_ws('|', (SELECT concat_ws(',', status, requested_amount, approved_amount, initiated_at, row_version) FROM payments.payments WHERE payment_id = @p), (SELECT concat_ws(',', reversal_type, amount, occurred_at, idempotency_key) FROM payments.payment_reversals WHERE reversal_id = @r));",
            ("p", oldPayment.PaymentId), ("r", oldPayment.ReversalId)));
        Assert.Equal(recentBefore, await PaymentRowsAsync(recentPayment));

        var after = await PaymentRowsAsync(oldPayment);
        Assert.Contains("bill_texts=0", Apply(), StringComparison.Ordinal);
        Assert.Equal(after, await PaymentRowsAsync(oldPayment));
    }

    [Fact]
    public async Task ABillWithAPendingCardConfirmationOrAnUnsettledPaymentKeepsThoseTextsWhileTheRestOfTheBillIsScrubbed()
    {
        var pending = await SeedBillAsync("Paid", 6, "iptal yok");
        var unsettled = await SeedBillAsync("Paid", 6, "iptal yok");
        var pendingPayment = await SeedPaymentAsync(pending.BillId, "Approved", 6, "Pending");
        var unsettledPayment = await SeedPaymentAsync(unsettled.BillId, "ReconciliationRequired", 6);

        Assert.Contains("bill_texts=2", Apply(), StringComparison.Ordinal);

        foreach (var bill in new[] { pending, unsettled })
            Assert.Equal(Marker, await ScalarAsync<string>("SELECT cancellation_reason FROM billing.bills WHERE bill_id = @id;", ("id", bill.BillId)));
        Assert.Equal(Marker, await ScalarAsync<string>("SELECT reason FROM payments.payment_reversals WHERE reversal_id = @r;", ("r", pendingPayment.ReversalId)));
        Assert.Equal(Marker, await ScalarAsync<string>("SELECT reason FROM payments.payment_status_history WHERE payment_status_history_id = @h;", ("h", pendingPayment.HistoryId)));
        Assert.Equal(
            "Ali Bey istedi",
            await ScalarAsync<string>("SELECT request_note FROM payments.manual_card_confirmations WHERE confirmation_id = @c;", ("c", pendingPayment.ConfirmationId)));
        Assert.Equal("Ali Bey memnun kalmadi", await ScalarAsync<string>("SELECT reason FROM payments.payment_status_history WHERE payment_status_history_id = @h;", ("h", unsettledPayment.HistoryId)));
    }

    [Fact]
    public async Task APaymentTextOfABillUnderALegalHoldStays()
    {
        var held = await SeedBillAsync("Paid", 6, "dava konusu");
        var payment = await SeedPaymentAsync(held.BillId, "Refunded", 6);
        await ExecuteAsync(
            "INSERT INTO privacy.legal_holds (hold_id, data_class, subject_id, reason, placed_by) VALUES (gen_random_uuid(), 'BillFreeText', @b, 'Vergi incelemesi', gen_random_uuid());",
            ("b", held.BillId));
        var before = await PaymentRowsAsync(payment);

        Assert.Contains("bill_texts=0", Apply(), StringComparison.Ordinal);
        Assert.Equal(before, await PaymentRowsAsync(payment));
    }

    [Fact]
    public async Task ThePaymentReversalLedgerAcceptsOnlyTheMarkerAsATextChangeAndStillRefusesEverythingElse()
    {
        var bill = await SeedBillAsync("Paid", 6, "x");
        var payment = await SeedPaymentAsync(bill.BillId, "Refunded", 6);
        static string Update(string set) => $"UPDATE payments.payment_reversals SET {set} WHERE reversal_id = @r;";
        var r = ("r", (object)payment.ReversalId);

        foreach (var tamper in new[] { "amount = 49", "reversal_type = 'PartialRefund'", "reason = 'baska metin'", "reason = '[anonymized]', amount = 49", "reason = '[anonymized]', occurred_at = now()" })
            Assert.Equal("23000", await SqlStateAsync(() => ExecuteAsync(Update(tamper), r)));
        Assert.Equal("23000", await SqlStateAsync(() => ExecuteAsync("DELETE FROM payments.payment_reversals WHERE reversal_id = @r;", r)));

        await ExecuteAsync(Update("reason = '[anonymized]'"), r);
        await ExecuteAsync(Update("reason = '[anonymized]'"), r);
        Assert.Equal(Marker, await ScalarAsync<string>("SELECT reason FROM payments.payment_reversals WHERE reversal_id = @r;", r));
    }

    private static async Task<string> SqlStateAsync(Func<Task> action)
    {
        var failure = await Assert.ThrowsAsync<PostgresException>(action);
        return failure.SqlState;
    }

    [Fact]
    public async Task EveryCashTextOfAnOldClosedSessionIsScrubbedWhileAmountsStayAndASecondRunChangesNothing()
    {
        var old = await SeedCashSessionAsync("Closed", 6);
        var reconciled = await SeedCashSessionAsync("Reconciled", 6);
        var recent = await SeedCashSessionAsync("Closed", 1);
        var live = await SeedCashSessionAsync("Open", 6);
        var pending = await SeedCashSessionAsync("Closed", 6, requestStatus: "Requested");
        var held = await SeedCashSessionAsync("Closed", 6);
        await ExecuteAsync(
            "INSERT INTO privacy.legal_holds (hold_id, data_class, subject_id, reason, placed_by) VALUES (gen_random_uuid(), 'CashFreeText', @s, 'Vergi incelemesi', gen_random_uuid());",
            ("s", held));

        var before = new Dictionary<Guid, string>();
        foreach (var session in new[] { old, reconciled, recent, live, pending, held })
            before[session] = await CashFiguresAsync(session);

        var first = Apply();
        Assert.Contains("cash_texts=2", first, StringComparison.Ordinal);

        foreach (var session in new[] { old, reconciled })
        {
            Assert.Equal(0L, await CashUnscrubbedAsync(session));
            Assert.Equal(before[session], await CashFiguresAsync(session));
            Assert.Equal(2L, await ScalarAsync<long>("SELECT row_version FROM cash.cash_sessions WHERE cash_session_id = @s;", ("s", session)));
        }

        foreach (var session in new[] { recent, live, pending, held })
        {
            Assert.Equal(5L, await CashUnscrubbedAsync(session));
            Assert.Equal(before[session], await CashFiguresAsync(session));
        }

        var second = Apply();
        Assert.Contains("cash_texts=0", second, StringComparison.Ordinal);
        Assert.Contains("blocked=0 failed=0", second, StringComparison.Ordinal);
        Assert.Equal(before[old], await CashFiguresAsync(old));

        await ExecuteAsync("SET LOCAL session_replication_role = replica; UPDATE cash.cash_movement_requests SET status = 'Rejected', decided_by = gen_random_uuid(), decided_at = now() WHERE cash_session_id = @s;", ("s", pending));
        Assert.Contains("cash_texts=1", Apply(), StringComparison.Ordinal);
        Assert.Equal(0L, await CashUnscrubbedAsync(pending));
    }

    [Fact]
    public async Task ATableTransferReasonIsScrubbedWhileTheTransfersOfAnExcludedOrderOrItsBillAHeldOneAndARecentOneStay()
    {
        var productId = await SeedProductAsync();
        var heldOrder = await SeedOrderAsync(productId, status: "Completed", createdAgoYears: 6, note: Marker);
        var heldBill = await SeedBillAsync("Paid", 6, "dava konusu");
        await ExecuteAsync("SET LOCAL session_replication_role = replica; UPDATE billing.bills SET order_id = @o WHERE bill_id = @b;", ("o", heldOrder), ("b", heldBill.BillId));
        var transfer = await SeedTransferAsync(6);
        var recent = await SeedTransferAsync(1);
        var byOrder = await SeedTransferAsync(6, orderId: heldOrder);
        var byBill = await SeedTransferAsync(6, billId: heldBill.BillId);
        var byHold = await SeedTransferAsync(6);
        await ExecuteAsync(
            "INSERT INTO privacy.legal_holds (hold_id, data_class, subject_id, reason, placed_by) VALUES (gen_random_uuid(), 'TableMovementReason', @t, 'Vergi incelemesi', gen_random_uuid());",
            ("t", byHold));
        var excludeFile = Path.Combine(Path.GetTempPath(), $"kvkk-hold-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(excludeFile, heldOrder + Environment.NewLine);
        try
        {
            Assert.Contains("table_texts=1", Apply(excludeFile), StringComparison.Ordinal);
            Assert.Contains("table_texts=0", Apply(excludeFile), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(excludeFile);
        }

        Assert.Equal(Marker, await ScalarAsync<string>("SELECT reason FROM table_mgmt.table_transfers WHERE table_transfer_id = @id;", ("id", transfer)));
        foreach (var kept in new[] { recent, byOrder, byBill, byHold })
            Assert.Equal("Ali Bey istedi", await ScalarAsync<string>("SELECT reason FROM table_mgmt.table_transfers WHERE table_transfer_id = @id;", ("id", kept)));
    }

    [Fact]
    public async Task ARequestOpenedAfterTheSessionWasQueuedHoldsTheSessionBackUntilItIsDecided()
    {
        var session = await SeedCashSessionAsync("Closed", 6);
        await new PostgresRetentionExecutionService(_dataSource!).ExecuteAsync(DateTimeOffset.UtcNow, "queued");
        await ExecuteAsync(
            """
            CREATE FUNCTION public.late_request() RETURNS trigger LANGUAGE plpgsql AS $f$
            BEGIN
                SET LOCAL session_replication_role = replica;
                INSERT INTO cash.cash_movement_requests (request_id, cash_session_id, amount, reason, idempotency_key, status, requested_by, requested_at)
                VALUES (gen_random_uuid(), NEW.subject_id, 5, 'sonradan acildi', 'late-key', 'Requested', gen_random_uuid(), now());
                SET LOCAL session_replication_role = origin;
                RETURN NEW;
            END $f$;
            CREATE TRIGGER late_request AFTER INSERT ON privacy.anonymization_jobs FOR EACH ROW EXECUTE FUNCTION public.late_request();
            """);

        Assert.Contains("blocked=1", Apply(), StringComparison.Ordinal);
        Assert.Equal(6L, await CashUnscrubbedAsync(session));

        await ExecuteAsync("DROP TRIGGER late_request ON privacy.anonymization_jobs;");
        await ExecuteAsync("SET LOCAL session_replication_role = replica; UPDATE cash.cash_movement_requests SET status = 'Rejected', decided_by = gen_random_uuid(), decided_at = now() WHERE reason = 'sonradan acildi';");
        Assert.Contains("failed=0", Apply(), StringComparison.Ordinal);
        Assert.Equal(0L, await CashUnscrubbedAsync(session));
    }

    private async Task<Guid> SeedCashSessionAsync(string status, int closedAgoYears, string? requestStatus = null)
    {
        var session = Guid.NewGuid();
        var closed = status is "Closed" or "Reconciled";
        await ExecuteAsync(
            """
            SET LOCAL session_replication_role = replica;
            INSERT INTO cash.cash_sessions (cash_session_id, cashier_user_id, terminal_id, status, opening_balance, expected_cash, actual_cash, difference, opened_at, closed_at,
                                            is_supervisor_override, override_reason, reconciliation_notes, difference_explanation, created_at, updated_at)
            VALUES (@s, gen_random_uuid(), gen_random_uuid(), @st, 100, 150, 149, -1, now() - make_interval(years => @ago, hours => 8),
                    CASE WHEN @closed THEN now() - make_interval(years => @ago) END, true, 'Ali Bey izin verdi', 'Ali Bey sayti', 'Ali Bey eksik verdi',
                    now() - make_interval(years => @ago, hours => 8), now() - make_interval(years => @ago));
            INSERT INTO cash.cash_counts (cash_count_id, cash_session_id, counted_amount, counted_by, notes, counted_at)
            VALUES (gen_random_uuid(), @s, 149, gen_random_uuid(), 'Ali Bey saydi', now() - make_interval(years => @ago));
            INSERT INTO cash.cash_transactions (cash_transaction_id, cash_session_id, type, direction, amount, notes, occurred_at)
            VALUES (gen_random_uuid(), @s, 'Opening', 'In', 100, NULL, now() - make_interval(years => @ago, hours => 8)),
                   (gen_random_uuid(), @s, 'CashIn', 'In', 50, 'Ali Bey avans', now() - make_interval(years => @ago, hours => 6)),
                   (gen_random_uuid(), @s, 'CashOut', 'Out', 1, 'Ali Bey sut aldi', now() - make_interval(years => @ago, hours => 5));
            INSERT INTO cash.cash_movement_requests (request_id, cash_session_id, amount, reason, idempotency_key, status, requested_by, requested_at, decided_by, decided_at, decision_note)
            SELECT gen_random_uuid(), @s, 5, 'Ali Bey ister', 'k-' || @s::text, @rs, gen_random_uuid(), now() - make_interval(years => @ago, hours => 7),
                   CASE WHEN @rs <> 'Requested' THEN gen_random_uuid() END, CASE WHEN @rs <> 'Requested' THEN now() - make_interval(years => @ago, hours => 7) END,
                   CASE WHEN @rs <> 'Requested' THEN 'Ali Bey reddetti' END
            WHERE @rs IS NOT NULL;
            """,
            ("s", session), ("st", status), ("ago", closedAgoYears), ("closed", closed), ("rs", (object?)requestStatus ?? "Rejected"));
        return session;
    }

    private Task<long> CashUnscrubbedAsync(Guid session) => ScalarAsync<long>(
        """
        SELECT (SELECT count(*) FROM cash.cash_transactions WHERE cash_session_id = @s AND notes IS NOT NULL AND notes <> @m)
             + (SELECT count(*) FROM cash.cash_counts WHERE cash_session_id = @s AND notes IS NOT NULL AND notes <> @m)
             + (SELECT count(*) FROM cash.cash_movement_requests WHERE cash_session_id = @s AND (reason <> @m OR (decision_note IS NOT NULL AND decision_note <> @m)))
             + (SELECT count(*) FROM cash.cash_sessions WHERE cash_session_id = @s
                  AND ((override_reason IS NOT NULL AND override_reason <> @m) OR (reconciliation_notes IS NOT NULL AND reconciliation_notes <> @m)
                       OR (difference_explanation IS NOT NULL AND difference_explanation <> @m)));
        """,
        ("s", session), ("m", Marker));

    private Task<string> CashFiguresAsync(Guid session) => ScalarAsync<string>(
        """
        SELECT concat_ws('|',
            (SELECT concat_ws(',', status, opening_balance, expected_cash, actual_cash, difference, opened_at, closed_at, is_supervisor_override) FROM cash.cash_sessions WHERE cash_session_id = @s),
            (SELECT string_agg(concat_ws(',', type, direction, amount, occurred_at), ';' ORDER BY occurred_at) FROM cash.cash_transactions WHERE cash_session_id = @s),
            (SELECT string_agg(concat_ws(',', counted_amount, counted_at), ';') FROM cash.cash_counts WHERE cash_session_id = @s),
            (SELECT string_agg(concat_ws(',', amount, status, requested_at, decided_at), ';') FROM cash.cash_movement_requests WHERE cash_session_id = @s));
        """,
        ("s", session));

    private async Task<Guid> SeedTransferAsync(int agoYears, Guid? orderId = null, Guid? billId = null)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            """
            SET LOCAL session_replication_role = replica;
            INSERT INTO table_mgmt.table_transfers (table_transfer_id, source_table_id, target_table_id, order_id, bill_id, reason, transferred_by, transferred_at)
            VALUES (@id, gen_random_uuid(), gen_random_uuid(), @o, @bl, 'Ali Bey istedi', gen_random_uuid(), now() - make_interval(years => @ago));
            """,
            ("id", id), ("o", (object?)orderId ?? DBNull.Value), ("bl", (object?)billId ?? DBNull.Value), ("ago", agoYears));
        return id;
    }

    private sealed record SeededBill(Guid BillId, Guid ItemId, Guid AdjustmentId, Guid RefundId);

    private async Task<SeededBill> SeedBillAsync(string status, int createdAgoYears, string cancellationReason, string? decisionNote = "Ali Bey ile konusuldu")
    {
        var bill = new SeededBill(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await ExecuteAsync(
            """
            SET LOCAL session_replication_role = replica;
            INSERT INTO billing.bills (bill_id, bill_number, status, subtotal, tax_total, payable_amount, paid_amount, opened_at, created_at, updated_at, cancellation_reason)
            VALUES (@b, @num, @st, 100, 10, 110, 110, now() - make_interval(years => @ago), now() - make_interval(years => @ago), now() - make_interval(years => @ago), @cr);
            INSERT INTO billing.bill_items (bill_item_id, bill_id, order_item_id, product_id, product_name_snapshot, quantity, unit_price, tax_rate, tax_amount, net_amount, gross_amount, line_type, notes, created_at, updated_at)
            VALUES (@i, @b, gen_random_uuid(), gen_random_uuid(), 'Test', 1, 100, 10, 10, 100, 110, 'Sale', 'sogansiz, Ali Bey icin', now() - make_interval(years => @ago), now() - make_interval(years => @ago));
            INSERT INTO billing.bill_adjustments (bill_adjustment_id, bill_id, adjustment_type, calculation_type, amount, net_amount, gross_amount, is_deduction, reason, authorized_by, notes, created_at)
            VALUES (@a, @b, 'DiscountAmount', 'FixedAmount', 5, 5, 5, true, 'Sadakat', gen_random_uuid(), 'Ali Bey tanidik', now() - make_interval(years => @ago));
            INSERT INTO payments.refund_requests (refund_request_id, bill_id, payment_id, amount, method, reason, status, requested_by, requested_at, decided_by, decided_at, decision_note, row_version)
            VALUES (@r, @b, gen_random_uuid(), 5, 'Cash', 'Ali Bey ister', 'Completed', gen_random_uuid(), now() - make_interval(years => @ago), gen_random_uuid(), now() - make_interval(years => @ago), @dn, 1);
            """,
            ("b", bill.BillId), ("i", bill.ItemId), ("a", bill.AdjustmentId), ("r", bill.RefundId),
            ("num", $"B-{bill.BillId:N}"[..20]), ("st", status), ("ago", createdAgoYears), ("cr", cancellationReason), ("dn", (object?)decisionNote ?? DBNull.Value));
        return bill;
    }

    private Task<string> BillAmountsAsync(SeededBill bill) => ScalarAsync<string>(
        """
        SELECT concat_ws('|',
            (SELECT concat_ws(',', status, subtotal, tax_total, payable_amount, paid_amount, opened_at, created_at, row_version) FROM billing.bills WHERE bill_id = @b),
            (SELECT concat_ws(',', quantity, unit_price, gross_amount, line_type) FROM billing.bill_items WHERE bill_item_id = @i),
            (SELECT concat_ws(',', adjustment_type, amount, reason, authorized_by) FROM billing.bill_adjustments WHERE bill_adjustment_id = @a),
            (SELECT concat_ws(',', amount, method, status, decided_by) FROM payments.refund_requests WHERE refund_request_id = @r));
        """,
        ("b", bill.BillId), ("i", bill.ItemId), ("a", bill.AdjustmentId), ("r", bill.RefundId));

    private string Apply(string? excludeFile = null)
    {
        using var output = new StringWriter();
        using var errors = new StringWriter();
        var original = Console.Out;
        var originalErrors = Console.Error;
        try
        {
            Console.SetOut(output);
            Console.SetError(errors);
            var exitCode = ALKAROS.Host.Program.Main(excludeFile is null
                ? ["kvkk-retention", "--db-url", _database.Url, "--apply"]
                : ["kvkk-retention", "--db-url", _database.Url, "--apply", "--exclude-order-ids-file", excludeFile]);
            Assert.True(exitCode == (int)HostExitCode.Success, output.ToString() + errors.ToString());
        }
        finally
        {
            Console.SetOut(original);
            Console.SetError(originalErrors);
        }

        return output.ToString();
    }

    private async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = _dataSource!.CreateCommand(sql);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public void MissingDbUrlFailsClosed()
    {
        var exit = ALKAROS.Host.Program.Main(["kvkk-retention"]);
        Assert.Equal((int)HostExitCode.StartupFailed, exit);
    }

    [Fact]
    public void UnknownArgumentFailsClosed()
    {
        var exit = ALKAROS.Host.Program.Main(["kvkk-retention", "--db-url", _database.Url, "--nope"]);
        Assert.Equal((int)HostExitCode.StartupFailed, exit);
    }

    private async Task<Guid> SeedUserAsync(bool active, int updatedAgoDays)
    {
        var id = Guid.NewGuid();
        await using var command = _dataSource!.CreateCommand(
            """
            INSERT INTO identity.users (user_id, username, password_hash, pin_hash, display_name, email, phone, active, created_at, updated_at)
            VALUES (@id, @u, 'hash', 'pin-hash', 'Gerçek Ad', 'a@b.com', '05001112233', @active,
                    now() - interval '900 days', now() - make_interval(days => @ago));
            """);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("u", $"user-{id:N}"[..24]);
        command.Parameters.AddWithValue("active", active);
        command.Parameters.AddWithValue("ago", updatedAgoDays);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    private async Task SeedHandoffNoteAsync(Guid from, Guid to, string note)
    {
        await using var command = _dataSource!.CreateCommand(
            """
            INSERT INTO notifications.serving_handoff_notes (handoff_note_id, from_user_id, to_user_id, note, consumed_at)
            VALUES (@id, @from, @to, @note, now());
            """);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("from", from);
        command.Parameters.AddWithValue("to", to);
        command.Parameters.AddWithValue("note", note);
        await command.ExecuteNonQueryAsync();
    }

    private async Task SeedPushSubscriptionAsync(Guid userId, string name)
    {
        await using var command = _dataSource!.CreateCommand(
            """
            INSERT INTO notifications.push_subscriptions (subscription_id, endpoint, p256dh, auth, user_id, terminal_id)
            VALUES (@id, @endpoint, 'k', 'a', @user, @terminal);
            """);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("endpoint", $"https://push.example.net/{name}");
        command.Parameters.AddWithValue("user", userId);
        command.Parameters.AddWithValue("terminal", Guid.NewGuid());
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Guid> SeedOldCustomerAndSupplierAsync()
    {
        var customerId = Guid.NewGuid();
        await using (var customer = _dataSource!.CreateCommand(
            "INSERT INTO customer_data.profiles (customer_id, envelope_bytes, created_at) VALUES (@id, '\\x00'::bytea, now() - interval '11 years');"))
        {
            customer.Parameters.AddWithValue("id", customerId);
            await customer.ExecuteNonQueryAsync();
        }

        await using var supplier = _dataSource!.CreateCommand(
            "INSERT INTO purchasing.suppliers (supplier_id, code, name, created_at, updated_at) VALUES (@id, @code, 'Tedarikci', now() - interval '11 years', now() - interval '11 years');");
        supplier.Parameters.AddWithValue("id", Guid.NewGuid());
        supplier.Parameters.AddWithValue("code", $"S-{customerId:N}"[..16]);
        await supplier.ExecuteNonQueryAsync();
        return customerId;
    }

    private async Task<Guid> SeedProductAsync()
    {
        var id = Guid.NewGuid();
        await using var command = _dataSource!.CreateCommand(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active, display_order, current_price)
            VALUES (@id, @sku, 'Test', 1, 1, true, 0, 10.00);
            """);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("sku", $"K-{id:N}"[..20]);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    private async Task<Guid> SeedOrderAsync(Guid productId, string status, int createdAgoYears, string note)
    {
        var orderId = Guid.NewGuid();
        await using (var order = _dataSource!.CreateCommand(
            """
            INSERT INTO orders.orders
              (order_id, source, table_id, status, confirmation_status, order_number, currency_code, notes, created_at, updated_at, row_version)
            VALUES (@id, 'Cashier', NULL, @st, 'NotRequired', @num, 'TRY', @note,
                    now() - make_interval(years => @ago), now() - make_interval(years => @ago), 1);
            """))
        {
            order.Parameters.AddWithValue("id", orderId);
            order.Parameters.AddWithValue("st", status);
            order.Parameters.AddWithValue("num", $"K-{orderId:N}"[..20]);
            order.Parameters.AddWithValue("note", note);
            order.Parameters.AddWithValue("ago", createdAgoYears);
            await order.ExecuteNonQueryAsync();
        }

        await using var item = _dataSource!.CreateCommand(
            """
            INSERT INTO orders.order_items
              (order_item_id, order_id, product_id, product_name_snapshot, quantity, unit_price, tax_rate, tax_amount, net_amount, gross_amount, status, kitchen_state, portion_reservation_status, notes, created_at, updated_at, row_version)
            VALUES (@iid, @oid, @pid, 'Test', 1, 10, 10, 1, 10, 11, 'Active', 'Served', 'NotApplicable', @note,
                    now() - make_interval(years => @ago), now() - make_interval(years => @ago), 1);
            """);
        item.Parameters.AddWithValue("iid", Guid.NewGuid());
        item.Parameters.AddWithValue("oid", orderId);
        item.Parameters.AddWithValue("pid", productId);
        item.Parameters.AddWithValue("note", note);
        item.Parameters.AddWithValue("ago", createdAgoYears);
        await item.ExecuteNonQueryAsync();
        return orderId;
    }

    private async Task<Guid> SeedTableAsync()
    {
        var id = Guid.NewGuid();
        await using var command = _dataSource!.CreateCommand(
            """
            INSERT INTO table_mgmt.tables (table_id, zone_id, table_number, capacity, active, current_status, current_order_id, current_bill_id, row_version)
            VALUES (@id, NULL, @n, 2, true, 'Available', NULL, NULL, 1);
            """);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("n", $"K-{id:N}"[..12]);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    private async Task SeedReservationAsync(Guid tableId, string status, int reservedAgoYears, string reason)
    {
        await using var command = _dataSource!.CreateCommand(
            """
            INSERT INTO table_mgmt.table_reservations
              (table_reservation_id, table_id, actor_type, status, reason, party_size, reserved_at, row_version)
            VALUES (@id, @tid, 'User', @st, @reason, 4, now() - make_interval(years => @ago), 1);
            """);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("tid", tableId);
        command.Parameters.AddWithValue("st", status);
        command.Parameters.AddWithValue("reason", reason);
        command.Parameters.AddWithValue("ago", reservedAgoYears);
        await command.ExecuteNonQueryAsync();
    }


    private async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = _dataSource!.CreateCommand(sql);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        var result = await command.ExecuteScalarAsync();
        return (T)(result ?? throw new InvalidOperationException("Scalar returned null."));
    }

    private string ConnectionString()
    {
        var uri = new Uri(_database.Url);
        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port,
            Database = uri.AbsolutePath.Trim('/'),
            Username = uri.UserInfo,
            Password = _database.PsqlOptions.Password,
        }.ConnectionString;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "database", "MigrationComposition", "order.json")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }
}
