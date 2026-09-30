using ALKAROS.Host.Composition;
using ALKAROS.Host.Composition.Migrations;
using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.Authentication;
using ALKAROS.Privacy.RetentionExecution;
using Npgsql;

namespace ALKAROS.Host;

/// <summary>
/// Entry point of the ALKAROS host application. The host executable entry
/// point is exposed as a callable <c>Main</c> returning the process exit code;
/// the executable packaging is owned by the release tasks.
/// Exit codes: 0 = success, 1 = migration execution failed, 2 = startup or
/// validation failed.
/// </summary>
public static class Program
{
    private const string PasswordEnvironmentVariable = "ALKAROS_DB_PASSWORD";
    private const string BootstrapUsernameEnvironmentVariable = "ALKAROS_BOOTSTRAP_USERNAME";
    private const string BootstrapDisplayNameEnvironmentVariable = "ALKAROS_BOOTSTRAP_DISPLAY_NAME";
    private const string BootstrapPasswordEnvironmentVariable = "ALKAROS_BOOTSTRAP_PASSWORD";
    private const string ManagerRoleCode = "manager";

    // The permission codes migration 042 grants the "manager" role that are NOT
    // part of the granular §2-3 catalog. Provisioning re-applies them idempotently
    // so a fresh bootstrap and a migrated database converge. The granular codes
    // (orders.*, tables.*, bills.*, floorplan.manage, cash.drawer, reports.view)
    // are granted to manager by migration 043 and are not repeated here.
    // kitchen.routing.manage / kitchen.reprint / operations.backup used to be
    // referenced by RequirePermissionAsync but never seeded, which made those
    // endpoints unreachable for every user. (deep-analysis finding B-1)
    // pos.cashier.mutate was dropped by migration 049 (V1-IAM-024).
    private static readonly (string Code, string Name)[] ManagerPermissions =
    [
        ("catalog.manage", "Manage catalog resources"),
        ("kitchen.routing.manage", "Manage kitchen printer routing"),
        ("kitchen.reprint", "Authorize kitchen ticket reprints"),
        ("operations.backup", "Trigger and inspect operational backups"),
    ];

    public static int Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "provision-manager", StringComparison.Ordinal))
        {
            try
            {
                return ProvisionManagerAsync(args[1..]).GetAwaiter().GetResult();
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NpgsqlException)
            {
                Console.Error.WriteLine($"PROVISIONING: {ex.Message}");
                return (int)HostExitCode.StartupFailed;
            }
        }

        if (args.Length > 0 && string.Equals(args[0], "serve", StringComparison.Ordinal))
        {
            try
            {
                return DualScreenApplication.Run(args[1..]);
            }
            catch (DualScreenStartupException ex)
            {
                Console.Error.WriteLine($"STARTUP: {ex.Message}");
                return (int)HostExitCode.StartupFailed;
            }
        }

        if (args.Length > 0 && string.Equals(args[0], "housekeeping", StringComparison.Ordinal))
        {
            try
            {
                return HousekeepingAsync(args[1..]).GetAwaiter().GetResult();
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NpgsqlException or FormatException)
            {
                Console.Error.WriteLine($"HOUSEKEEPING: {ex.Message}");
                return (int)HostExitCode.StartupFailed;
            }
        }

        if (args.Length > 0 && string.Equals(args[0], "kvkk-retention", StringComparison.Ordinal))
        {
            try
            {
                return KvkkRetentionAsync(args[1..]).GetAwaiter().GetResult();
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NpgsqlException or FormatException or IOException)
            {
                Console.Error.WriteLine($"KVKK-RETENTION: {ex.Message}");
                return (int)HostExitCode.StartupFailed;
            }
        }

        var options = ParseArguments(args);
        if (options is null)
        {
            PrintUsage(Console.Error);
            return (int)HostExitCode.StartupFailed;
        }

        try
        {
            var exitCode = HostComposition.Run(options, Console.Out);
            Console.Out.WriteLine($"exit: {(int)exitCode}");
            return (int)exitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FATAL: {ex}");
            return (int)HostExitCode.StartupFailed;
        }
    }

    private static async Task<int> ProvisionManagerAsync(string[] args)
    {
        if (args.Length != 2 || !string.Equals(args[0], "--db-url", StringComparison.Ordinal))
            throw new ArgumentException("provision-manager requires exactly one --db-url argument.");

        var databasePassword = RequiredEnvironmentValue(PasswordEnvironmentVariable, 1, 256, allowWhitespace: false);
        var username = RequiredEnvironmentValue(BootstrapUsernameEnvironmentVariable, 3, 100, allowWhitespace: false);
        var displayName = RequiredEnvironmentValue(BootstrapDisplayNameEnvironmentVariable, 1, 200, allowWhitespace: true);
        var bootstrapPassword = RequiredEnvironmentValue(BootstrapPasswordEnvironmentVariable, 12, 256, allowWhitespace: false);
        var connectionString = BuildConnectionString(args[1], databasePassword, "ALKAROS.ManagerProvisioning");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

        var users = new List<(Guid Id, string Username, bool Active)>();
        await using (var usersCommand = new NpgsqlCommand(
            "SELECT user_id, username, active FROM identity.users ORDER BY username FOR UPDATE;",
            connection,
            transaction))
        await using (var reader = await usersCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                users.Add((reader.GetGuid(0), reader.GetString(1), reader.GetBoolean(2)));
        }

        var existingUser = users.SingleOrDefault(user => string.Equals(user.Username, username, StringComparison.Ordinal));
        Guid userId;
        if (existingUser == default)
        {
            if (users.Count != 0)
                throw new InvalidOperationException("Manager provisioning is refused because users already exist.");

            userId = Guid.NewGuid();
            var passwordHash = new PasswordHasher().Hash(bootstrapPassword);
            await using var insertUser = new NpgsqlCommand(
                """
                INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
                VALUES (@user_id, @username, @password_hash, @display_name, true);
                """,
                connection,
                transaction);
            insertUser.Parameters.AddWithValue("user_id", userId);
            insertUser.Parameters.AddWithValue("username", username);
            insertUser.Parameters.AddWithValue("password_hash", passwordHash);
            insertUser.Parameters.AddWithValue("display_name", displayName);
            await insertUser.ExecuteNonQueryAsync();
        }
        else
        {
            if (!existingUser.Active)
                throw new InvalidOperationException("The provisioned manager account is inactive.");
            userId = existingUser.Id;
        }

        var roleId = await UpsertReturningIdAsync(
            connection,
            transaction,
            "identity.roles",
            "role_id",
            ManagerRoleCode,
            "Manager");

        foreach (var permission in ManagerPermissions)
        {
            var permissionId = await UpsertReturningIdAsync(
                connection,
                transaction,
                "identity.permissions",
                "permission_id",
                permission.Code,
                permission.Name);
            await ExecuteAssignmentAsync(
                connection,
                transaction,
                "identity.role_permissions",
                "role_permission_id",
                "role_id",
                roleId,
                "permission_id",
                permissionId);
        }

        await ExecuteAssignmentAsync(
            connection,
            transaction,
            "identity.user_roles",
            "user_role_id",
            "user_id",
            userId,
            "role_id",
            roleId);

        await transaction.CommitAsync();
        Console.Out.WriteLine($"Manager provisioning verified for username '{username}'.");
        return (int)HostExitCode.Success;
    }

    /// <summary>
    /// One-shot operational data housekeeping: deletes expired ephemeral rows
    /// that carry no value after expiry and would otherwise grow without bound
    /// (V1-RMD-091). Not a KVKK personal-data retention job (that is V15-KVK-001,
    /// with 5-10 year retention per the V0-CMP-003 inventory) and it never
    /// touches orders, bills, fiscal or invoice records.
    /// Usage: housekeeping --db-url &lt;url&gt; [--grace-days &lt;N&gt;]
    /// </summary>
    private static async Task<int> HousekeepingAsync(string[] args)
    {
        string? databaseUrl = null;
        var graceDays = 7;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--db-url" when i + 1 < args.Length:
                    databaseUrl = args[++i];
                    break;
                case "--grace-days" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out graceDays) || graceDays < 0)
                        throw new ArgumentException("--grace-days must be a non-negative integer.");
                    break;
                default:
                    throw new ArgumentException($"Unrecognized housekeeping argument '{args[i]}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(databaseUrl))
            throw new ArgumentException("housekeeping requires a --db-url argument.");

        var databasePassword = RequiredEnvironmentValue(PasswordEnvironmentVariable, 1, 256, allowWhitespace: false);
        var connectionString = BuildConnectionString(databaseUrl, databasePassword, "ALKAROS.Housekeeping");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);

        var started = DateTimeOffset.UtcNow;

        int idempotencyDeleted;
        await using (var command = new NpgsqlCommand(
            "DELETE FROM idempotency_keys WHERE expires_at < now();",
            connection,
            transaction))
        {
            idempotencyDeleted = await command.ExecuteNonQueryAsync();
        }

        // identity.session_operations rows cascade with their parent session.
        int sessionsDeleted;
        await using (var command = new NpgsqlCommand(
            """
            DELETE FROM identity.device_sessions
            WHERE expires_at < now() - make_interval(days => @grace)
               OR (revoked_at IS NOT NULL AND revoked_at < now() - make_interval(days => @grace));
            """,
            connection,
            transaction))
        {
            command.Parameters.AddWithValue("grace", graceDays);
            sessionsDeleted = await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();

        var seconds = (DateTimeOffset.UtcNow - started).TotalSeconds;
        Console.Out.WriteLine(
            $"housekeeping: idempotency_keys={idempotencyDeleted} device_sessions={sessionsDeleted} "
            + $"grace_days={graceDays} seconds={seconds:F2}");
        return (int)HostExitCode.Success;
    }

    /// <summary>
    /// KVKK retention: anonymize personal data past its retention window
    /// (V1-RMD-094, driven by the V0-CMP-003 inventory). Classes and windows:
    ///   identity.users (inactive + 1 year)      -> mask username/display_name, null email/phone
    ///   orders.orders.notes / order_items.notes (5 years, terminal order)  -> '[anonymized]'
    ///   table_mgmt.table_reservations.reason    (5 years, closed reservation) -> '[anonymized]'
    /// audit.audit_events is out of scope: it is enforced append-only by a
    /// database trigger (AUD-01). Its 10-year anonymization needs a partition-
    /// drop mechanism and is deferred to V15-KVK-002; IAuditSanitizer already
    /// redacts secrets on write.
    /// Fiscal receipts, Z reports, invoices and the financial columns are
    /// legal-retention and are never touched. Default is a dry run; pass
    /// --apply to write. Idempotent (an already-'[anonymized]' row is skipped).
    /// The windows and the choice of records come from the versioned policy of
    /// Privacy.RetentionExecution, so the dry run and the apply always agree;
    /// customers and suppliers past their window are only queued as work items.
    /// Usage: kvkk-retention --db-url &lt;url&gt; [--apply] [--as-of &lt;ISO date&gt;]
    ///        [--exclude-order-ids-file &lt;path&gt;]
    /// </summary>
    private static async Task<int> KvkkRetentionAsync(string[] args)
    {
        const string Marker = "[anonymized]";
        string? databaseUrl = null;
        var apply = false;
        var asOf = DateTimeOffset.UtcNow;
        string? excludeFile = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--db-url" when i + 1 < args.Length:
                    databaseUrl = args[++i];
                    break;
                case "--apply":
                    apply = true;
                    break;
                case "--as-of" when i + 1 < args.Length:
                    if (!DateTimeOffset.TryParse(args[++i], System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                            out asOf))
                        throw new ArgumentException("--as-of must be an ISO date.");
                    break;
                case "--exclude-order-ids-file" when i + 1 < args.Length:
                    excludeFile = args[++i];
                    break;
                default:
                    throw new ArgumentException($"Unrecognized kvkk-retention argument '{args[i]}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(databaseUrl))
            throw new ArgumentException("kvkk-retention requires a --db-url argument.");

        var excludedOrderIds = new List<Guid>();
        if (excludeFile is not null)
        {
            foreach (var line in await File.ReadAllLinesAsync(excludeFile))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                    continue;
                if (!Guid.TryParse(trimmed, out var held))
                    throw new ArgumentException($"exclude-order-ids-file contains a non-GUID line: '{trimmed}'.");
                excludedOrderIds.Add(held);
            }
        }

        var databasePassword = RequiredEnvironmentValue(PasswordEnvironmentVariable, 1, 256, allowWhitespace: false);
        var connectionString = BuildConnectionString(databaseUrl, databasePassword, "ALKAROS.KvkkRetention");

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var retention = new PostgresRetentionExecutionService(dataSource);
        var heldSet = excludedOrderIds.ToHashSet();

        // The dry run and the apply pick their records through the same versioned policy; only the apply writes.
        var staff = 0;
        var orderNotes = 0;
        var itemNotes = 0;
        var reservations = 0;
        RetentionPlan plan;
        if (apply)
        {
            plan = (await retention.ExecuteAsync(asOf, "kvkk-retention", heldSet)).Plan;
            (staff, orderNotes, itemNotes, reservations) = await ScrubPendingAsync(dataSource, retention, heldSet, Marker);
        }
        else
        {
            plan = await retention.PlanAsync(asOf, heldSet);
            var orderIds = plan.Candidates.Where(c => c.DataClass == RetentionClass.OrderNotes).Select(c => c.SubjectId).ToArray();
            staff = plan.Candidates.Count(c => c.DataClass == RetentionClass.StaffAccount);
            reservations = plan.Candidates.Count(c => c.DataClass == RetentionClass.ReservationReason);
            await using var counts = dataSource.CreateCommand(
                """
                SELECT (SELECT count(*) FROM orders.orders WHERE order_id = ANY(@ids) AND notes IS NOT NULL AND notes <> @m),
                       (SELECT count(*) FROM orders.order_items WHERE order_id = ANY(@ids) AND notes IS NOT NULL AND notes <> @m);
                """);
            counts.Parameters.AddWithValue("ids", orderIds);
            counts.Parameters.AddWithValue("m", Marker);
            await using var reader = await counts.ExecuteReaderAsync();
            await reader.ReadAsync();
            orderNotes = Convert.ToInt32(reader.GetInt64(0), System.Globalization.CultureInfo.InvariantCulture);
            itemNotes = Convert.ToInt32(reader.GetInt64(1), System.Globalization.CultureInfo.InvariantCulture);
        }

        Console.Out.WriteLine(
            $"kvkk-retention: staff={staff} order_notes={orderNotes} item_notes={itemNotes} "
            + $"reservation_reasons={reservations} "
            + $"customers={plan.Candidates.Count(c => c.DataClass == RetentionClass.CustomerProfile)} "
            + $"suppliers={plan.Candidates.Count(c => c.DataClass == RetentionClass.Supplier)} "
            + $"policy={plan.PolicyVersion} "
            + $"as_of={asOf:yyyy-MM-dd} excluded_orders={excludedOrderIds.Count} apply={apply.ToString().ToLowerInvariant()}");
        return (int)HostExitCode.Success;
    }

    /// <summary>
    /// Scrubs the free-text fields of every pending staff, order-note and reservation work item (customers and suppliers
    /// stay pending for the field-level anonymization workflow) and completes the items in the same transaction.
    /// </summary>
    private static async Task<(int Staff, int OrderNotes, int ItemNotes, int Reservations)> ScrubPendingAsync(
        NpgsqlDataSource dataSource, PostgresRetentionExecutionService retention, HashSet<Guid> held, string marker)
    {
        async Task<Guid[]> PendingIdsAsync(RetentionClass dataClass)
            => (await retention.PendingAsync(dataClass, 1_000_000)).Select(item => item.SubjectId).Where(id => !held.Contains(id)).ToArray();

        var staffIds = await PendingIdsAsync(RetentionClass.StaffAccount);
        var orderIds = await PendingIdsAsync(RetentionClass.OrderNotes);
        var reservationIds = await PendingIdsAsync(RetentionClass.ReservationReason);

        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);

        async Task<int> UpdateAsync(string sql, Guid[] ids)
        {
            await using var update = new NpgsqlCommand(sql, connection, transaction);
            update.Parameters.AddWithValue("ids", ids);
            update.Parameters.AddWithValue("m", marker);
            return await update.ExecuteNonQueryAsync();
        }

        var staff = await UpdateAsync(
            """
            UPDATE identity.users
            SET username = 'anon-' || left(user_id::text, 8),
                display_name = @m, email = NULL, phone = NULL,
                password_hash = '!kvkk-retention-disabled', updated_at = now()
            WHERE user_id = ANY(@ids) AND active = false AND display_name <> @m;
            """,
            staffIds);
        var orderNotes = await UpdateAsync(
            "UPDATE orders.orders SET notes = @m, updated_at = now() WHERE order_id = ANY(@ids) AND notes IS NOT NULL AND notes <> @m;",
            orderIds);
        var itemNotes = await UpdateAsync(
            "UPDATE orders.order_items SET notes = @m, updated_at = now() WHERE order_id = ANY(@ids) AND notes IS NOT NULL AND notes <> @m;",
            orderIds);
        var reservations = await UpdateAsync(
            """
            UPDATE table_mgmt.table_reservations
            SET reason = @m, release_reason = CASE WHEN release_reason IS NOT NULL THEN @m END, row_version = row_version + 1
            WHERE table_reservation_id = ANY(@ids) AND reason <> @m;
            """,
            reservationIds);

        await retention.CompleteAsync(RetentionClass.StaffAccount, staffIds, "kvkk-retention", transaction);
        await retention.CompleteAsync(RetentionClass.OrderNotes, orderIds, "kvkk-retention", transaction);
        await retention.CompleteAsync(RetentionClass.ReservationReason, reservationIds, "kvkk-retention", transaction);
        await transaction.CommitAsync();
        return (staff, orderNotes, itemNotes, reservations);
    }

    private static string RequiredEnvironmentValue(
        string variable,
        int minimumLength,
        int maximumLength,
        bool allowWhitespace)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException($"{variable} is required.");
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new ArgumentException($"{variable} must not start or end with whitespace.");
        if (value.Length < minimumLength || value.Length > maximumLength)
            throw new ArgumentException($"{variable} length must be between {minimumLength} and {maximumLength} characters.");
        if (!allowWhitespace && value.Any(char.IsWhiteSpace))
            throw new ArgumentException($"{variable} must not contain whitespace.");
        return value;
    }

    private static string BuildConnectionString(string databaseUrl, string password, string applicationName)
    {
        if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != "postgresql" && uri.Scheme != "postgres")
            || string.IsNullOrWhiteSpace(uri.Host)
            || string.IsNullOrWhiteSpace(uri.AbsolutePath.Trim('/')))
        {
            throw new ArgumentException("--db-url must be a PostgreSQL URL with host and database.");
        }

        var userInfo = uri.UserInfo.Split(':', 2);
        if (userInfo.Length != 1 || string.IsNullOrWhiteSpace(userInfo[0]))
            throw new ArgumentException("--db-url must contain a username and must not contain a password.");

        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = password,
            ApplicationName = applicationName,
            // Pooling is disabled on purpose: this builder is only for the
            // one-shot CLI verbs (provision-manager, housekeeping,
            // kvkk-retention) that open a single connection and exit. Do NOT
            // reuse this helper on a long-lived service path (serve) - that
            // path builds its own pooled connection string in
            // DualScreenOptions. (deep-analysis finding B-5)
            Pooling = false,
        }.ConnectionString;
    }

    private static async Task<Guid> UpsertReturningIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string table,
        string idColumn,
        string code,
        string name)
    {
        var id = Guid.NewGuid();
        await using var command = new NpgsqlCommand(
            $"""
            INSERT INTO {table} ({idColumn}, code, name)
            VALUES (@id, @code, @name)
            ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name
            RETURNING {idColumn};
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("code", code);
        command.Parameters.AddWithValue("name", name);
        return (Guid)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException($"Could not resolve '{code}' in {table}."));
    }

    private static async Task ExecuteAssignmentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string table,
        string idColumn,
        string leftColumn,
        Guid leftId,
        string rightColumn,
        Guid rightId)
    {
        await using var command = new NpgsqlCommand(
            $"""
            INSERT INTO {table} ({idColumn}, {leftColumn}, {rightColumn})
            VALUES (@id, @left_id, @right_id)
            ON CONFLICT ({leftColumn}, {rightColumn}) DO NOTHING;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("left_id", leftId);
        command.Parameters.AddWithValue("right_id", rightId);
        await command.ExecuteNonQueryAsync();
    }

    private static HostCompositionOptions? ParseArguments(string[] args)
    {
        string? manifestPath = null;
        string? migrationsDirectory = null;
        string? databaseUrl = null;
        string? psqlExecutable = null;
        string? rollbackId = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--order-manifest" when i + 1 < args.Length:
                    if (manifestPath is not null) return null;
                    manifestPath = args[++i];
                    break;
                case "--migrations-dir" when i + 1 < args.Length:
                    if (migrationsDirectory is not null) return null;
                    migrationsDirectory = args[++i];
                    break;
                case "--db-url" when i + 1 < args.Length:
                    if (databaseUrl is not null) return null;
                    databaseUrl = args[++i];
                    break;
                case "--psql" when i + 1 < args.Length:
                    if (psqlExecutable is not null) return null;
                    psqlExecutable = args[++i];
                    break;
                case "--rollback" when i + 1 < args.Length:
                    if (rollbackId is not null) return null;
                    rollbackId = args[++i];
                    break;
                default:
                    return null;
            }
        }

        if (string.IsNullOrWhiteSpace(manifestPath)
            || string.IsNullOrWhiteSpace(migrationsDirectory)
            || string.IsNullOrWhiteSpace(databaseUrl))
            return null;

        if (rollbackId is not null && !IsPosition(rollbackId))
            return null;

        var password = Environment.GetEnvironmentVariable(PasswordEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(password))
            return null;

        return new HostCompositionOptions(
            manifestPath,
            migrationsDirectory,
            new PsqlOptions(
                databaseUrl,
                psqlExecutable ?? "psql",
                password),
            rollbackId);
    }

    private static bool IsPosition(string value)
        => value.Length == 3 && value.All(char.IsAsciiDigit);

    private static void PrintUsage(TextWriter writer)
    {
        writer.WriteLine("Usage: ALKAROS.Host --order-manifest <path> --migrations-dir <path> --db-url <url> [--psql <path>] [--rollback <position>]");
        writer.WriteLine("  --order-manifest  Path to database/MigrationComposition/order.json");
        writer.WriteLine("  --migrations-dir  Directory scanned for <NNN>-<name>.up.sql / .down.sql files");
        writer.WriteLine("  --db-url          PostgreSQL connection URL (e.g. postgresql://user@host:5432/db)");
        writer.WriteLine("  --psql            psql executable path (default: psql from PATH)");
        writer.WriteLine("  --rollback        Run the rollback script of the given position instead of forward");
        writer.WriteLine("  Database password is read from the ALKAROS_DB_PASSWORD environment variable.");
        writer.WriteLine();
        writer.WriteLine("Verbs: serve | provision-manager --db-url <url> | housekeeping --db-url <url> [--grace-days <N>]");
        writer.WriteLine("       | kvkk-retention --db-url <url> [--apply] [--as-of <ISO date>] [--exclude-order-ids-file <path>]");
        writer.WriteLine("  housekeeping      Delete expired idempotency_keys and expired/long-revoked device_sessions");
        writer.WriteLine("                    (operational hygiene only; not KVKK personal-data retention).");
        writer.WriteLine("  kvkk-retention    Anonymize personal data past its V0-CMP-003 retention window");
        writer.WriteLine("                    (staff / order notes / reservation reasons). Dry run unless --apply.");
        writer.WriteLine("                    Fiscal / invoice / financial data and append-only audit are never touched.");
    }
}
