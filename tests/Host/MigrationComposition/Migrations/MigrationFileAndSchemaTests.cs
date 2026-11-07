using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ALKAROS.Billing.Adjustments;
using ALKAROS.Host.Composition;
using ALKAROS.Host.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Tests.Migrations;

public sealed class MigrationFileLineEndingTests
{
    // These scripts were committed with Windows line endings before the policy existed. Their deployed checksums
    // are the SHA-256 of the raw bytes, so rewriting them would make every existing database refuse to start.
    private static readonly string[] LegacyWindowsScripts =
    [
        "V1/V1-KIT-001/014-kitchen-tickets.down.sql",
        "V1/V1-KIT-001/014-kitchen-tickets.up.sql",
        "V1/V1-OPS-001/015-audit-log.down.sql",
        "V1/V1-OPS-001/015-audit-log.up.sql",
        "V11/V11-MNU-001/067-daily-menus.down.sql",
        "V11/V11-MNU-001/067-daily-menus.up.sql",
        "V11/V11-MNU-003/066-static-menu.down.sql",
        "V11/V11-MNU-003/066-static-menu.up.sql",
        "V11/V11-PUR-001/069-purchase-orders-receipts.down.sql",
        "V11/V11-PUR-001/069-purchase-orders-receipts.up.sql",
        "V11/V11-PUR-002/068-suppliers.down.sql",
        "V11/V11-PUR-002/068-suppliers.up.sql",
        "V11/V11-RCP-002/070-recipe-cost-snapshots.down.sql",
        "V11/V11-RCP-002/070-recipe-cost-snapshots.up.sql",
    ];

    private static readonly string Root = MigrationTestPaths.FindRepositoryRoot();

    [Fact]
    public void EveryMigrationScriptUsesUnixLineEndingsExceptTheFrozenLegacyOnes()
    {
        var migrations = Path.Combine(Root, "database", "migrations");
        var offenders = Directory.EnumerateFiles(migrations, "*.sql", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(migrations, path).Replace('\\', '/'))
            .Where(relative => !LegacyWindowsScripts.Contains(relative, StringComparer.Ordinal))
            .Where(relative => File.ReadAllBytes(Path.Combine(migrations, relative)).Contains((byte)'\r'))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void TheFrozenLegacyListHasNoStaleEntries()
    {
        var migrations = Path.Combine(Root, "database", "migrations");

        Assert.All(LegacyWindowsScripts, relative =>
            Assert.Contains((byte)'\r', File.ReadAllBytes(Path.Combine(migrations, relative))));
    }

    [Fact]
    public void GitNeverRewritesTheLineEndingsOfAMigrationScript()
    {
        var rules = File.ReadAllLines(Path.Combine(Root, ".gitattributes")).Select(line => line.Trim()).ToList();

        Assert.Contains("database/migrations/** -text", rules);
    }
}

[Collection("Host database password environment")]
public sealed class MigrationSchemaTests : IAsyncLifetime
{
    private const string RowVersionType =
        """
        SELECT data_type FROM information_schema.columns
        WHERE table_schema = 'customer_display' AND table_name = 'screensaver_images' AND column_name = 'row_version';
        """;

    private static readonly string Root = MigrationTestPaths.FindRepositoryRoot();
    private readonly TestDatabase _database = new();
    private readonly string _scratch = Path.Combine(Path.GetTempPath(), $"alkaros-rmd698-{Guid.NewGuid():N}");

    public Task InitializeAsync() => _database.InitializeAsync();

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
        if (Directory.Exists(_scratch))
            Directory.Delete(_scratch, recursive: true);
    }

    [Fact]
    public async Task TheScreensaverTableRefusesUnsupportedTypesEmptyAndOversizedContent()
    {
        Migrate(OrderPath(), MigrationsPath());
        await using var source = NpgsqlDataSource.Create(ConnectionString());

        await AssertCheckViolationAsync(source, "text/plain", new byte[10]);
        await AssertCheckViolationAsync(source, "image/png", []);
        await AssertCheckViolationAsync(source, "video/mp4", new byte[20 * 1024 * 1024 + 1]);
        await InsertScreensaverAsync(source, "video/mp4", new byte[20 * 1024 * 1024]);

        Assert.Equal("bigint", await ScalarAsync<string>(source, RowVersionType));
    }

    [Fact]
    public async Task APairingCodeOrSessionCannotExpireBeforeItIsCreated()
    {
        Migrate(OrderPath(), MigrationsPath());
        await using var source = NpgsqlDataSource.Create(ConnectionString());
        await source.CreateCommand(
            "INSERT INTO customer_display.terminals (terminal_id, row_version, created_at, updated_at) " +
            "VALUES ('00000000-0000-0000-0000-000000000001', 1, now(), now());").ExecuteNonQueryAsync();

        var pairing = await Assert.ThrowsAsync<PostgresException>(() => source.CreateCommand(
            """
            INSERT INTO customer_display.pairing_requests
                (request_id, display_id, pairing_secret_hash, code_hash, expires_at, created_at)
            VALUES (gen_random_uuid(), gen_random_uuid(), repeat('a', 64), repeat('b', 64), now(), now());
            """).ExecuteNonQueryAsync());
        var session = await Assert.ThrowsAsync<PostgresException>(() => source.CreateCommand(
            """
            INSERT INTO customer_display.display_sessions
                (session_id, display_id, terminal_id, token_hash, created_at, expires_at)
            VALUES (gen_random_uuid(), gen_random_uuid(), '00000000-0000-0000-0000-000000000001', repeat('c', 64), now(), now());
            """).ExecuteNonQueryAsync());

        Assert.Equal(PostgresErrorCodes.CheckViolation, pairing.SqlState);
        Assert.Equal(PostgresErrorCodes.CheckViolation, session.SqlState);
    }

    [Fact]
    public async Task TheLookupIndexesExistAndRollingBackTheMigrationRemovesEverythingItAdded()
    {
        var (order, migrations) = CopyUpTo("201");
        Migrate(order, migrations);
        await using var source = NpgsqlDataSource.Create(ConnectionString());

        Assert.Equal(6, await SchemaObjectCountAsync(source));
        var terminals = await IndexDefinitionAsync(source, "ix_terminals_active_order");
        Assert.Contains("(active_order_id)", terminals, StringComparison.Ordinal);
        Assert.Contains("WHERE (active_order_id IS NOT NULL)", terminals, StringComparison.Ordinal);
        Assert.Contains(
            "(source_reference_id, source_reference_type)",
            await IndexDefinitionAsync(source, "ix_account_transactions_source_reference"),
            StringComparison.Ordinal);

        Rollback(order, migrations, "201");
        Assert.Equal(0, await SchemaObjectCountAsync(source));
        Assert.Equal("integer", await ScalarAsync<string>(source, RowVersionType));

        Migrate(order, migrations);
        Assert.Equal(6, await SchemaObjectCountAsync(source));
    }

    [Fact]
    public async Task TheTerminalKeyColumnExistsAndRollingBackTheMigrationRemovesIt()
    {
        const string columnCount = "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'customer_display' AND table_name = 'terminals' AND column_name = 'terminal_key_hash';";
        const string constraintCount = "SELECT count(*) FROM pg_constraint WHERE conname = 'ck_customer_display_terminal_key_hash';";
        Migrate(OrderPath(), MigrationsPath());
        await using var source = NpgsqlDataSource.Create(ConnectionString());
        Assert.Equal(1L, await ScalarAsync<long>(source, columnCount));
        Assert.Equal(1L, await ScalarAsync<long>(source, constraintCount));

        RollBackDownTo("202");
        Assert.Equal(0L, await ScalarAsync<long>(source, columnCount));
        Assert.Equal(0L, await ScalarAsync<long>(source, constraintCount));

        Migrate(OrderPath(), MigrationsPath());
        Assert.Equal(1L, await ScalarAsync<long>(source, columnCount));
    }

    [Fact]
    public async Task TheBillsIntegrityRulesExistAndRollingBackTheMigrationRemovesThem()
    {
        const string rules = "SELECT count(*) FROM pg_constraint WHERE conname IN ('ck_bills_nonnegative_amounts', 'fk_bills_cancelled_by');";
        const string redundantIndex = "SELECT count(*) FROM pg_indexes WHERE schemaname = 'billing' AND indexname = 'ix_bill_items_bill';";
        Migrate(OrderPath(), MigrationsPath());
        await using var source = NpgsqlDataSource.Create(ConnectionString());
        Assert.Equal(2L, await ScalarAsync<long>(source, rules));
        Assert.Equal(0L, await ScalarAsync<long>(source, redundantIndex));

        RollBackDownTo("209");
        Assert.Equal(0L, await ScalarAsync<long>(source, rules));
        Assert.Equal(1L, await ScalarAsync<long>(source, redundantIndex));

        Migrate(OrderPath(), MigrationsPath());
        Assert.Equal(2L, await ScalarAsync<long>(source, rules));
    }

    [Fact]
    public async Task TheHalfPortionMigrationFlagsOnlyProductsAlreadySoldInFractionsAndRollsBackCleanly()
    {
        const string flags = "SELECT string_agg(sku || '=' || allows_half_portion::text, ' ' ORDER BY sku) FROM catalog.products;";
        var (order, migrations) = CopyUpTo("215");
        Migrate(order, migrations);
        await using var source = NpgsqlDataSource.Create(ConnectionString());
        await source.CreateCommand(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode) VALUES
                ('aaaaaaaa-0000-0000-0000-000000000001', 'S1', 'Half sold', 1, 1),
                ('aaaaaaaa-0000-0000-0000-000000000002', 'S2', 'Whole sold', 1, 1);
            INSERT INTO orders.orders (order_id, source, status, confirmation_status, order_number, created_at, updated_at)
            VALUES ('bbbbbbbb-0000-0000-0000-000000000001', 'Cashier', 'Submitted', 'NotRequired', 'O-800', now(), now());
            INSERT INTO orders.order_items (order_item_id, order_id, product_id, product_name_snapshot, quantity, unit_price, tax_rate,
                tax_amount, net_amount, gross_amount, status, kitchen_state, portion_reservation_status, created_at, updated_at)
            VALUES
                (gen_random_uuid(), 'bbbbbbbb-0000-0000-0000-000000000001', 'aaaaaaaa-0000-0000-0000-000000000001', 'Half sold', 1.5, 10, 10,
                 1.36, 13.64, 15, 'Active', 'Sent', 'NotApplicable', now(), now()),
                (gen_random_uuid(), 'bbbbbbbb-0000-0000-0000-000000000001', 'aaaaaaaa-0000-0000-0000-000000000002', 'Whole sold', 2, 10, 10,
                 1.82, 18.18, 20, 'Active', 'Sent', 'NotApplicable', now(), now());
            """).ExecuteNonQueryAsync();

        Migrate(OrderPath(), MigrationsPath());
        Assert.Equal("S1=true S2=false", await ScalarAsync<string>(source, flags));

        RollBackDownTo("216");
        Assert.Equal(0L, await ScalarAsync<long>(source,
            "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'catalog' AND table_name = 'products' AND column_name = 'allows_half_portion';"));

        Migrate(OrderPath(), MigrationsPath());
        Assert.Equal("S1=true S2=false", await ScalarAsync<string>(source, flags));
    }

    [Fact]
    public async Task TheAdjustmentTypeCheckListsExactlyTheTypesTheDomainCanCreate()
    {
        Migrate(OrderPath(), MigrationsPath());
        await using var source = NpgsqlDataSource.Create(ConnectionString());

        var definition = await ScalarAsync<string>(source,
            "SELECT string_agg(pg_get_constraintdef(oid), ' ') FROM pg_constraint " +
            "WHERE conrelid = 'billing.bill_adjustments'::regclass AND contype = 'c' AND pg_get_constraintdef(oid) LIKE '%adjustment_type%';");
        var listed = Regex.Matches(definition, "'([A-Za-z]+)'").Select(match => match.Groups[1].Value).Order().ToArray();

        Assert.Equal(Enum.GetNames<AdjustmentType>().Order().ToArray(), listed);
    }

    [Fact]
    public async Task AStoredCustomFeeRowStopsTheAdjustmentTypeMigrationInsteadOfBeingRewritten()
    {
        var (order, migrations) = CopyUpTo("216");
        Migrate(order, migrations);
        await using var source = NpgsqlDataSource.Create(ConnectionString());
        await InsertAdjustmentAsync(source, "CustomFee");

        using var output = new StringWriter();
        var exit = HostComposition.Run(new HostCompositionOptions(OrderPath(), MigrationsPath(), _database.PsqlOptions, null), output);

        Assert.NotEqual(HostExitCode.Success, exit);
        Assert.Contains("CustomFee", output.ToString());
        Assert.Equal(1L, await ScalarAsync<long>(source, "SELECT count(*) FROM billing.bill_adjustments WHERE adjustment_type = 'CustomFee';"));
    }

    [Fact]
    public async Task RollingBackTheAdjustmentTypeMigrationAcceptsCustomFeeAgainAndItAppliesOnceMore()
    {
        Migrate(OrderPath(), MigrationsPath());
        await using var source = NpgsqlDataSource.Create(ConnectionString());
        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertAdjustmentAsync(source, "CustomFee"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);

        RollBackDownTo("217");
        await InsertAdjustmentAsync(source, "CustomFee");
        await source.CreateCommand("DELETE FROM billing.bill_adjustments;").ExecuteNonQueryAsync();

        Migrate(OrderPath(), MigrationsPath());
        failure = await Assert.ThrowsAsync<PostgresException>(() => InsertAdjustmentAsync(source, "CustomFee"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
    }

    [Fact]
    public async Task ABillItemThatHoldsAnAdjustmentOrAllocationCannotBeDeletedOnItsOwnButTheWholeBillCanBe()
    {
        Migrate(OrderPath(), MigrationsPath());
        await using var source = NpgsqlDataSource.Create(ConnectionString());
        var (billId, adjustedItemId, allocatedItemId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await ExecuteWithoutForeignKeysAsync(source, $"""
            INSERT INTO billing.bills (bill_id, bill_number, status, opened_at, created_at, updated_at)
            VALUES ('{billId}', 'B-{billId:N}', 'Open', now(), now(), now());
            INSERT INTO billing.bill_items (bill_item_id, bill_id, order_item_id, product_id, product_name_snapshot, quantity,
                unit_price, tax_rate, net_amount, gross_amount, line_type, created_at, updated_at)
            VALUES ('{adjustedItemId}', '{billId}', gen_random_uuid(), gen_random_uuid(), 'a', 1, 5, 0, 5, 5, 'Sale', now(), now()),
                   ('{allocatedItemId}', '{billId}', gen_random_uuid(), gen_random_uuid(), 'b', 1, 5, 0, 5, 5, 'Sale', now(), now());
            INSERT INTO billing.bill_adjustments (bill_adjustment_id, bill_id, bill_item_id, adjustment_type, calculation_type,
                amount, net_amount, gross_amount, is_deduction, reason, authorized_by, created_at)
            VALUES (gen_random_uuid(), '{billId}', '{adjustedItemId}', 'DiscountAmount', 'FixedAmount', 1, 1, 1, true, 'test', gen_random_uuid(), now());
            INSERT INTO billing.bill_allocations (bill_allocation_id, bill_id, bill_item_id, owner_type, owner_reference, allocated_amount, created_at)
            VALUES (gen_random_uuid(), '{billId}', '{allocatedItemId}', 'Item', 'x', 5, now());
            """);

        foreach (var itemId in new[] { adjustedItemId, allocatedItemId })
        {
            var failure = await Assert.ThrowsAsync<PostgresException>(() =>
                source.CreateCommand($"DELETE FROM billing.bill_items WHERE bill_item_id = '{itemId}';").ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, failure.SqlState);
        }

        await source.CreateCommand($"DELETE FROM billing.bills WHERE bill_id = '{billId}';").ExecuteNonQueryAsync();
        Assert.Equal(0L, await ScalarAsync<long>(source,
            "SELECT (SELECT count(*) FROM billing.bill_items) + (SELECT count(*) FROM billing.bill_adjustments) + (SELECT count(*) FROM billing.bill_allocations);"));
    }

    [Fact]
    public async Task TheBillRulesAddedUnvalidatedAreValidatedAndAViolatingOldRowStopsTheMigration()
    {
        const string unvalidated =
            "SELECT count(*) FROM pg_constraint WHERE conname IN ('ck_bills_nonnegative_amounts', 'fk_bills_cancelled_by') AND NOT convalidated;";
        var (order, migrations) = CopyUpTo("208");
        Migrate(order, migrations);
        await using var source = NpgsqlDataSource.Create(ConnectionString());
        await source.CreateCommand(
            """
            INSERT INTO billing.bills (bill_id, bill_number, status, subtotal, opened_at, created_at, updated_at)
            VALUES (gen_random_uuid(), 'B-OLD', 'Open', -1, now(), now(), now());
            """).ExecuteNonQueryAsync();

        using var output = new StringWriter();
        var exit = HostComposition.Run(new HostCompositionOptions(OrderPath(), MigrationsPath(), _database.PsqlOptions, null), output);
        Assert.NotEqual(HostExitCode.Success, exit);
        Assert.Contains("ck_bills_nonnegative_amounts", output.ToString());

        await source.CreateCommand("DELETE FROM billing.bills;").ExecuteNonQueryAsync();
        Migrate(OrderPath(), MigrationsPath());
        Assert.Equal(0L, await ScalarAsync<long>(source, unvalidated));
    }

    [Fact]
    public async Task TheCashLedgerRefusesChangesAndDeletesButADeletedUserStillLosesOnlyTheirRecordedByLink()
    {
        Migrate(OrderPath(), MigrationsPath());
        await using var source = NpgsqlDataSource.Create(ConnectionString());
        var (userId, transactionId) = (Guid.NewGuid(), Guid.NewGuid());
        await source.CreateCommand(
            $"""
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES ('{userId}', 'ledger-{userId:N}', 'not-used', 'Ledger Test', true);
            """).ExecuteNonQueryAsync();
        await ExecuteWithoutForeignKeysAsync(source, $"""
            INSERT INTO cash.cash_transactions (cash_transaction_id, cash_session_id, type, direction, amount, recorded_by, occurred_at)
            VALUES ('{transactionId}', gen_random_uuid(), 'Opening', 'In', 5, '{userId}', now());
            """);

        foreach (var statement in new[]
        {
            $"UPDATE cash.cash_transactions SET amount = 9 WHERE cash_transaction_id = '{transactionId}';",
            $"UPDATE cash.cash_transactions SET recorded_by = NULL WHERE cash_transaction_id = '{transactionId}';",
            $"DELETE FROM cash.cash_transactions WHERE cash_transaction_id = '{transactionId}';",
            "TRUNCATE cash.cash_transactions CASCADE;",
        })
        {
            var failure = await Assert.ThrowsAsync<PostgresException>(() => source.CreateCommand(statement).ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.IntegrityConstraintViolation, failure.SqlState);
        }

        await source.CreateCommand($"DELETE FROM identity.users WHERE user_id = '{userId}';").ExecuteNonQueryAsync();
        Assert.Equal(1L, await ScalarAsync<long>(source,
            $"SELECT count(*) FROM cash.cash_transactions WHERE cash_transaction_id = '{transactionId}' AND recorded_by IS NULL AND amount = 5;"));

        var unattributed = await Assert.ThrowsAsync<PostgresException>(() =>
            source.CreateCommand($"UPDATE cash.cash_transactions SET amount = 9 WHERE cash_transaction_id = '{transactionId}';").ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.IntegrityConstraintViolation, unattributed.SqlState);
    }

    [Fact]
    public async Task RollingBackTheIntegrityMigrationRestoresTheCascadesAndLiftsTheLedgerGuardAndItAppliesOnceMore()
    {
        static string Cascades(char action) => "SELECT count(*) FROM pg_constraint WHERE conname IN ('fk_bill_adjustments_item', 'fk_bill_allocations_item') AND confdeltype = '" + action + "';";
        const string guard = "SELECT count(*) FROM pg_trigger WHERE tgname IN ('trg_cash_transactions_guard', 'trg_cash_transactions_clear_guard');";
        Migrate(OrderPath(), MigrationsPath());
        await using var source = NpgsqlDataSource.Create(ConnectionString());
        Assert.Equal(2L, await ScalarAsync<long>(source, Cascades('a')));
        Assert.Equal(2L, await ScalarAsync<long>(source, guard));

        RollBackDownTo("218");
        Assert.Equal(2L, await ScalarAsync<long>(source, Cascades('c')));
        Assert.Equal(0L, await ScalarAsync<long>(source, guard));

        Migrate(OrderPath(), MigrationsPath());
        Assert.Equal(2L, await ScalarAsync<long>(source, Cascades('a')));
        Assert.Equal(2L, await ScalarAsync<long>(source, guard));
    }

    [Fact]
    public async Task TheCaseHistoryRefusesChangesDeletesAndBulkClearsAndRollingBackLiftsTheGuardAndItAppliesOnceMore()
    {
        const string guard = "SELECT count(*) FROM pg_trigger WHERE tgname IN ('trg_case_actions_guard', 'trg_case_actions_clear_guard');";
        Migrate(OrderPath(), MigrationsPath());
        await using var source = NpgsqlDataSource.Create(ConnectionString());
        var (caseId, actionId) = (Guid.NewGuid(), Guid.NewGuid());
        await source.CreateCommand(
            $"""
            INSERT INTO reconciliation.cases (case_id, deduplication_key, case_type, source_a_ref, source_b_ref, severity, status)
            VALUES ('{caseId}', 'guard-{caseId:N}', 'PaymentMismatch', 'a', 'b', 'Low', 'Open');
            INSERT INTO reconciliation.case_actions (action_id, case_id, action_type, performed_by)
            VALUES ('{actionId}', '{caseId}', 'Created', gen_random_uuid());
            """).ExecuteNonQueryAsync();

        foreach (var statement in new[]
        {
            $"UPDATE reconciliation.case_actions SET action_type = 'Resolved' WHERE action_id = '{actionId}';",
            $"DELETE FROM reconciliation.case_actions WHERE action_id = '{actionId}';",
            "TRUNCATE reconciliation.case_actions;",
        })
        {
            var failure = await Assert.ThrowsAsync<PostgresException>(() => source.CreateCommand(statement).ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.IntegrityConstraintViolation, failure.SqlState);
            Assert.Contains("case_actions is append-only", failure.MessageText);
        }

        Assert.Equal(1L, await ScalarAsync<long>(source,
            $"SELECT count(*) FROM reconciliation.case_actions WHERE action_id = '{actionId}' AND action_type = 'Created';"));
        Assert.Equal(2L, await ScalarAsync<long>(source, guard));

        RollBackDownTo("220");
        Assert.Equal(0L, await ScalarAsync<long>(source, guard));
        Assert.Equal(0L, await ScalarAsync<long>(source, "SELECT count(*) FROM pg_proc WHERE proname = 'case_actions_guard';"));

        Migrate(OrderPath(), MigrationsPath());
        Assert.Equal(2L, await ScalarAsync<long>(source, guard));
    }

    [Fact]
    public async Task TheApprovedManualCardLookupHasItsOwnIndexAndRollingBackRemovesItAndItAppliesOnceMore()
    {
        // Columns in order, whether it is unique, whether it covers every row.
        const string shape =
            """
            SELECT (SELECT string_agg(a.attname, ',' ORDER BY k.ord)
                    FROM unnest(i.indkey::int2[]) WITH ORDINALITY k(attnum, ord)
                    JOIN pg_attribute a ON a.attrelid = i.indrelid AND a.attnum = k.attnum)
                   || ':' || i.indisunique::text || ':' || (i.indpred IS NULL)::text
            FROM pg_index i
            JOIN pg_class c ON c.oid = i.indexrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'payments' AND c.relname = 'ix_manual_card_confirmations_payment_status';
            """;
        Migrate(OrderPath(), MigrationsPath());
        await using var source = NpgsqlDataSource.Create(ConnectionString());
        Assert.Equal("payment_id,status:false:true", await ScalarAsync<string>(source, shape));

        RollBackDownTo("221");
        Assert.Equal(0L, await ScalarAsync<long>(source, "SELECT count(*) FROM pg_indexes WHERE indexname = 'ix_manual_card_confirmations_payment_status';"));

        Migrate(OrderPath(), MigrationsPath());
        Assert.Equal("payment_id,status:false:true", await ScalarAsync<string>(source, shape));
    }

    [Fact]
    public async Task TheCashierCompPolicyIsSeededAndRollingBackRemovesOnlyTheSeededRow()
    {
        const string policy = "SELECT mode || ':' || limit_amount || ':' || max_count || ':' || window_seconds FROM identity.authorization_policies WHERE permission_code = 'bills.comp' AND role_code = 'cashier';";
        Migrate(OrderPath(), MigrationsPath());
        await using var source = NpgsqlDataSource.Create(ConnectionString());
        Assert.Equal("auto_within:50.00:5:28800", await ScalarAsync<string>(source, policy));

        RollBackDownTo("222");
        Assert.Equal(0L, await ScalarAsync<long>(source, "SELECT count(*) FROM identity.authorization_policies WHERE permission_code = 'bills.comp' AND role_code = 'cashier';"));

        Migrate(OrderPath(), MigrationsPath());
        Assert.Equal("auto_within:50.00:5:28800", await ScalarAsync<string>(source, policy));
    }

    [Fact]
    public async Task TheBillTerminalOwnerColumnIsNullableAndRollingBackRemovesItAndItAppliesOnceMore()
    {
        const string column = "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'billing' AND table_name = 'bills' AND column_name = 'terminal_id' AND data_type = 'uuid' AND is_nullable = 'YES';";
        Migrate(OrderPath(), MigrationsPath());
        await using var source = NpgsqlDataSource.Create(ConnectionString());
        Assert.Equal(1L, await ScalarAsync<long>(source, column));

        RollBackDownTo("219");
        Assert.Equal(0L, await ScalarAsync<long>(source, column));

        Migrate(OrderPath(), MigrationsPath());
        Assert.Equal(1L, await ScalarAsync<long>(source, column));
    }

    [Theory]
    [InlineData("payments.payment_status_history", "changed_by", "fk_payment_status_history_changed_by",
        "INSERT INTO payments.payment_status_history (payment_status_history_id, payment_id, old_status, new_status, changed_at) " +
        "VALUES (gen_random_uuid(), gen_random_uuid(), 'Approved', 'Refunded', now());")]
    [InlineData("cash.cash_sessions", "cashier_user_id", "fk_cash_sessions_cashier_user", CashSessionRow)]
    [InlineData("cash.cash_sessions", "closed_by", "fk_cash_sessions_closed_by", CashSessionRow)]
    [InlineData("cash.cash_sessions", "reconciled_by", "fk_cash_sessions_reconciled_by", CashSessionRow)]
    [InlineData("cash.cash_counts", "counted_by", "fk_cash_counts_counted_by",
        "INSERT INTO cash.cash_counts (cash_count_id, cash_session_id, counted_amount, counted_by, counted_at) " +
        "VALUES (gen_random_uuid(), gen_random_uuid(), 1, gen_random_uuid(), now());")]
    [InlineData("cash.cash_transactions", "recorded_by", "fk_cash_transactions_recorded_by",
        "INSERT INTO cash.cash_transactions (cash_transaction_id, cash_session_id, type, direction, amount, recorded_by, occurred_at) " +
        "VALUES (gen_random_uuid(), gen_random_uuid(), 'Opening', 'In', 1, gen_random_uuid(), now());")]
    [InlineData("payments.refund_intents", "requested_by", "fk_refund_intents_requested_by",
        "INSERT INTO payments.refund_intents (refund_intent_id, payment_id, payment_allocation_id, requested_amount, status, idempotency_key, requested_by, requested_at) " +
        "VALUES (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(), 1, 'Pending', gen_random_uuid()::text, gen_random_uuid(), now());")]
    public async Task APaymentOrCashColumnThatNamesAUserRejectsAUserWhoDoesNotExist(string table, string column, string constraint, string insertRow)
    {
        Migrate(OrderPath(), MigrationsPath());
        await using var source = NpgsqlDataSource.Create(ConnectionString());
        await ExecuteWithoutForeignKeysAsync(source, insertRow);
        await source.CreateCommand($"ALTER TABLE {table} DISABLE TRIGGER USER;").ExecuteNonQueryAsync();

        var failure = await Assert.ThrowsAsync<PostgresException>(() =>
            source.CreateCommand($"UPDATE {table} SET {column} = gen_random_uuid();").ExecuteNonQueryAsync());

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, failure.SqlState);
        Assert.Equal(constraint, failure.ConstraintName);
    }

    [Fact]
    public async Task TheUserForeignKeysClearTheirNullableColumnsOnUserDeleteAndRefuseTheDeleteForTheRequiredOnes()
    {
        Migrate(OrderPath(), MigrationsPath());
        await using var source = NpgsqlDataSource.Create(ConnectionString());

        Assert.Equal(
            "fk_cash_counts_counted_by:a,fk_cash_sessions_cashier_user:a,fk_cash_sessions_closed_by:n,fk_cash_sessions_reconciled_by:n," +
            "fk_cash_transactions_recorded_by:n,fk_payment_status_history_changed_by:n,fk_refund_intents_requested_by:n",
            await ScalarAsync<string>(source,
                "SELECT string_agg(conname || ':' || confdeltype::text, ',' ORDER BY conname) FROM pg_constraint " +
                "WHERE contype = 'f' AND confrelid = 'identity.users'::regclass AND conname IN " +
                "('fk_cash_counts_counted_by', 'fk_cash_sessions_cashier_user', 'fk_cash_sessions_closed_by', 'fk_cash_sessions_reconciled_by', " +
                "'fk_cash_transactions_recorded_by', 'fk_payment_status_history_changed_by', 'fk_refund_intents_requested_by');"));
        Assert.Equal(1L, await ScalarAsync<long>(source,
            "SELECT count(*) FROM pg_indexes WHERE schemaname = 'cash' AND indexname = 'ix_cash_transactions_recorded_by';"));
        Assert.Equal(5L, await ScalarAsync<long>(source,
            "SELECT count(*) FROM information_schema.columns WHERE is_nullable = 'YES' AND (table_schema, table_name, column_name) IN " +
            "(('payments', 'payment_status_history', 'changed_by'), ('cash', 'cash_sessions', 'closed_by'), ('cash', 'cash_sessions', 'reconciled_by'), " +
            "('cash', 'cash_transactions', 'recorded_by'), ('payments', 'refund_intents', 'requested_by'));"));
    }

    private const string CashSessionRow =
        "INSERT INTO cash.cash_sessions (cash_session_id, cashier_user_id, terminal_id, status, opening_balance, opened_at, closed_by, reconciled_by, created_at, updated_at) " +
        "VALUES (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(), 'Closed', 0, now(), gen_random_uuid(), gen_random_uuid(), now(), now());";

    [Theory]
    [InlineData("032")]
    [InlineData("075")]
    [InlineData("096")]
    [InlineData("100")]
    [InlineData("101")]
    [InlineData("108")]
    [InlineData("119")]
    [InlineData("123")]
    [InlineData("131")]
    [InlineData("180")]
    [InlineData("209")]
    [InlineData("210")]
    public void AMigrationWithoutItsOwnEvidenceRollsBackAndAppliesAgain(string id)
    {
        var (order, migrations) = CopyUpTo(id);

        Migrate(order, migrations);
        Rollback(order, migrations, id);
        Migrate(order, migrations);
    }

    private (string Order, string Migrations) CopyUpTo(string lastId)
    {
        var migrations = Path.Combine(_scratch, "migrations");
        foreach (var file in Directory.EnumerateFiles(MigrationsPath(), "*.sql", SearchOption.AllDirectories))
        {
            if (string.CompareOrdinal(Path.GetFileName(file)[..3], lastId) > 0)
                continue;
            var target = Path.Combine(migrations, Path.GetRelativePath(MigrationsPath(), file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }

        var manifest = JsonNode.Parse(File.ReadAllText(OrderPath()))!;
        var kept = manifest["migrations"]!.AsArray()
            .Where(entry => string.CompareOrdinal(entry!["id"]!.GetValue<string>(), lastId) <= 0)
            .Select(entry => entry!.DeepClone())
            .ToArray();
        manifest["migrations"] = new JsonArray(kept);
        var order = Path.Combine(_scratch, "order.json");
        File.WriteAllText(order, manifest.ToJsonString());
        return (order, migrations);
    }

    // A migration can only be rolled back once every later one is, so unwind from the newest down to the given id.
    private void RollBackDownTo(string firstId)
    {
        var ids = JsonNode.Parse(File.ReadAllText(OrderPath()))!["migrations"]!.AsArray()
            .Select(entry => entry!["id"]!.GetValue<string>())
            .Where(id => string.CompareOrdinal(id, firstId) >= 0)
            .OrderByDescending(id => id, StringComparer.Ordinal);
        foreach (var id in ids)
            Rollback(OrderPath(), MigrationsPath(), id);
    }

    private void Migrate(string order, string migrations) => Run(order, migrations, null);

    private void Rollback(string order, string migrations, string id) => Run(order, migrations, id);

    private void Run(string order, string migrations, string? rollbackId)
    {
        using var output = new StringWriter();
        var exit = HostComposition.Run(
            new HostCompositionOptions(order, migrations, _database.PsqlOptions, rollbackId), output);
        Assert.True(exit == HostExitCode.Success, output.ToString());
    }

    private static string OrderPath() => Path.Combine(Root, "database", "MigrationComposition", "order.json");

    private static string MigrationsPath() => Path.Combine(Root, "database", "migrations");

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

    private static async Task InsertScreensaverAsync(NpgsqlDataSource source, string contentType, byte[] content)
    {
        await using var command = source.CreateCommand(
            "INSERT INTO customer_display.screensaver_images (id, content, content_type, updated_at, row_version) " +
            "VALUES (1, @content, @type, now(), 1) ON CONFLICT (id) DO NOTHING;");
        command.Parameters.AddWithValue("content", content);
        command.Parameters.AddWithValue("type", contentType);
        await command.ExecuteNonQueryAsync();
    }

    // Foreign keys are skipped for the insert only: the test needs one adjustment row, not a whole bill graph.
    private static Task InsertAdjustmentAsync(NpgsqlDataSource source, string adjustmentType) =>
        ExecuteWithoutForeignKeysAsync(source, $"""
            INSERT INTO billing.bill_adjustments
                (bill_adjustment_id, bill_id, adjustment_type, calculation_type, amount, net_amount, gross_amount,
                 is_deduction, reason, authorized_by, created_at)
            VALUES (gen_random_uuid(), gen_random_uuid(), '{adjustmentType}', 'FixedAmount', 5, 5, 5, false, 'test', gen_random_uuid(), now());
            """);

    private static async Task ExecuteWithoutForeignKeysAsync(NpgsqlDataSource source, string sql)
    {
        await using var connection = await source.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand("SET LOCAL session_replication_role = replica;\n" + sql, connection, transaction);
        await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }

    private static async Task AssertCheckViolationAsync(NpgsqlDataSource source, string contentType, byte[] content)
    {
        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertScreensaverAsync(source, contentType, content));
        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlDataSource source, string sql)
    {
        await using var command = source.CreateCommand(sql);
        return (T)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Scalar returned null."));
    }

    private static Task<string> IndexDefinitionAsync(NpgsqlDataSource source, string name) =>
        ScalarAsync<string>(source, $"SELECT indexdef FROM pg_indexes WHERE indexname = '{name}';");

    // The objects migration 201 adds: two indexes and four constraints; the column type change is checked on its own.
    private static async Task<long> SchemaObjectCountAsync(NpgsqlDataSource source) =>
        await ScalarAsync<long>(
            source,
            """
            SELECT (SELECT count(*) FROM pg_indexes
                    WHERE indexname IN ('ix_terminals_active_order', 'ix_account_transactions_source_reference'))
                 + (SELECT count(*) FROM pg_constraint
                    WHERE conname IN ('pairing_requests_expiry_check', 'display_sessions_expiry_check',
                                      'screensaver_images_content_type_check', 'screensaver_images_size_check'));
            """);
}

internal static class MigrationTestPaths
{
    public static string FindRepositoryRoot()
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
