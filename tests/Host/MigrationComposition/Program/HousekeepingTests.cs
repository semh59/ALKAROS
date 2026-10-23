using ALKAROS.Host.Composition;
using ALKAROS.Host.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Tests.Program;

[Collection("Host database password environment")]
public sealed class HousekeepingTests : IAsyncLifetime
{
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
    public async Task SweepDeletesExpiredRowsAndKeepsFreshAndInGraceRows()
    {
        var userId = await SeedUserAsync();

        // idempotency_keys: one expired, one still valid
        await InsertIdempotencyKeyAsync("client-a", "op-expired", expiresInHours: -2);
        await InsertIdempotencyKeyAsync("client-a", "op-valid", expiresInHours: 6);

        // device_sessions: expired beyond grace, expired within grace, still valid,
        // revoked beyond grace, revoked within grace
        var expiredBeyondGrace = await InsertSessionAsync(userId, "cashier:1", expiresDays: -30, revokedDays: null);
        var expiredInGrace = await InsertSessionAsync(userId, "cashier:2", expiresDays: -3, revokedDays: null);
        var stillValid = await InsertSessionAsync(userId, "cashier:3", expiresDays: 5, revokedDays: null);
        var revokedBeyondGrace = await InsertSessionAsync(userId, "cashier:4", expiresDays: 5, revokedDays: -30);
        var revokedInGrace = await InsertSessionAsync(userId, "cashier:5", expiresDays: 5, revokedDays: -2);

        using var output = new StringWriter();
        var originalOut = Console.Out;
        int exit;
        try
        {
            Console.SetOut(output);
            exit = ALKAROS.Host.Program.Main(["housekeeping", "--db-url", _database.Url]);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Equal((int)HostExitCode.Success, exit);
        Assert.Contains("idempotency_keys=1", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("device_sessions=2", output.ToString(), StringComparison.Ordinal);

        Assert.Equal(0L, await CountIdempotencyAsync("client-a", "op-expired"));
        Assert.Equal(1L, await CountIdempotencyAsync("client-a", "op-valid"));

        Assert.False(await SessionExistsAsync(expiredBeyondGrace));
        Assert.True(await SessionExistsAsync(expiredInGrace));
        Assert.True(await SessionExistsAsync(stillValid));
        Assert.False(await SessionExistsAsync(revokedBeyondGrace));
        Assert.True(await SessionExistsAsync(revokedInGrace));
    }

    [Fact]
    public async Task SweepAgesOutNotificationAndSecondScreenDataAndKeepsWhatIsStillInUse()
    {
        var active = await SeedUserAsync();
        var inactive = await SeedUserAsync(active: false);
        var tableId = await SeedTableAsync();

        var oldNoteRecipient = await SeedUserAsync();
        await InsertHandoffNoteAsync(active, oldNoteRecipient, createdDaysAgo: 100);
        await InsertHandoffNoteAsync(active, active, createdDaysAgo: 1);

        await InsertPushSubscriptionAsync("old-unused", active, createdDaysAgo: 200, lastSuccessDaysAgo: null);
        await InsertPushSubscriptionAsync("old-but-working", active, createdDaysAgo: 200, lastSuccessDaysAgo: 2);
        await InsertPushSubscriptionAsync("inactive-user", inactive, createdDaysAgo: 1, lastSuccessDaysAgo: 1);

        await InsertHelpRequestAsync(tableId, active, createdDaysAgo: 400);
        await InsertHelpRequestAsync(tableId, active, createdDaysAgo: 200);
        await InsertHelpRequestAsync(tableId, active, createdDaysAgo: 10);

        var terminalId = await InsertDisplayTerminalAsync();
        await InsertDisplaySessionAsync(terminalId, expiresDays: -30, revokedDays: null);
        await InsertDisplaySessionAsync(terminalId, expiresDays: 5, revokedDays: -30);
        await InsertDisplaySessionAsync(terminalId, expiresDays: 5, revokedDays: null);
        await InsertPairingRequestAsync(terminalId, expiresDays: -30, consumedDays: null);
        await InsertPairingRequestAsync(terminalId, expiresDays: 5, consumedDays: -30);
        await InsertPairingRequestAsync(terminalId, expiresDays: 5, consumedDays: null);

        var line = RunHousekeeping();

        Assert.Contains("handoff_notes=1", line, StringComparison.Ordinal);
        Assert.Contains("push_subscriptions=2", line, StringComparison.Ordinal);
        Assert.Contains("help_requests=1", line, StringComparison.Ordinal);
        Assert.Contains("display_sessions=2", line, StringComparison.Ordinal);
        Assert.Contains("display_pairings=2", line, StringComparison.Ordinal);
        Assert.Equal(1L, await CountAsync("notifications.serving_handoff_notes"));
        Assert.Equal(1L, await CountAsync("notifications.push_subscriptions"));
        Assert.Equal(1L, await ScalarCountAsync("SELECT count(*) FROM notifications.push_subscriptions WHERE endpoint = 'https://push.example.net/old-but-working'"));
        Assert.Equal(2L, await CountAsync("notifications.help_requests"));
        Assert.Equal(1L, await CountAsync("customer_display.display_sessions"));
        Assert.Equal(1L, await CountAsync("customer_display.pairing_requests"));
    }

    [Fact]
    public async Task NotificationWindowsCanBeShortenedPerRun()
    {
        var user = await SeedUserAsync();
        var tableId = await SeedTableAsync();
        await InsertHandoffNoteAsync(user, user, createdDaysAgo: 10);
        await InsertHelpRequestAsync(tableId, user, createdDaysAgo: 10);

        var line = RunHousekeeping("--notification-days", "5", "--help-request-days", "5");

        Assert.Contains("handoff_notes=1", line, StringComparison.Ordinal);
        Assert.Contains("help_requests=1", line, StringComparison.Ordinal);
        Assert.Contains("notification_days=5 help_request_days=5", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ALargeBacklogIsDeletedInBatches()
    {
        await ExecuteAsync(
            """
            INSERT INTO idempotency_keys (client_id, operation_id, request_hash, response_envelope, created_at, expires_at)
            SELECT 'bulk', 'op-' || n, repeat('0', 64), '\x00'::bytea, now() - interval '3 days', now() - interval '2 days'
            FROM generate_series(1, 5001) AS n;
            """);

        var line = RunHousekeeping();

        Assert.Contains("idempotency_keys=5001", line, StringComparison.Ordinal);
        Assert.Equal(0L, await CountAsync("idempotency_keys"));
    }

    [Theory]
    [InlineData("--notification-days", "0")]
    [InlineData("--help-request-days", "0")]
    [InlineData("--notification-days", "abc")]
    public void NonPositiveNotificationWindowsFailClosed(string option, string value)
    {
        var exit = ALKAROS.Host.Program.Main(["housekeeping", "--db-url", _database.Url, option, value]);
        Assert.Equal((int)HostExitCode.StartupFailed, exit);
    }

    [Fact]
    public async Task ZeroGraceDaysSweepsRecentlyExpiredSessions()
    {
        var userId = await SeedUserAsync();
        var expiredRecently = await InsertSessionAsync(userId, "cashier:9", expiresDays: -1, revokedDays: null);

        var exit = ALKAROS.Host.Program.Main(
            ["housekeeping", "--db-url", _database.Url, "--grace-days", "0"]);

        Assert.Equal((int)HostExitCode.Success, exit);
        Assert.False(await SessionExistsAsync(expiredRecently));
    }

    [Fact]
    public void MissingDbUrlFailsClosed()
    {
        var exit = ALKAROS.Host.Program.Main(["housekeeping"]);
        Assert.Equal((int)HostExitCode.StartupFailed, exit);
    }

    [Fact]
    public void NegativeGraceDaysFailsClosed()
    {
        var exit = ALKAROS.Host.Program.Main(
            ["housekeeping", "--db-url", _database.Url, "--grace-days", "-1"]);
        Assert.Equal((int)HostExitCode.StartupFailed, exit);
    }

    [Fact]
    public async Task ANewPendingNoteClosesTheEarlierOneForTheSameWaiter()
    {
        var sender = await SeedUserAsync();
        var receiver = await SeedUserAsync();
        var first = await InsertNoteAsync(sender, receiver, "birinci");
        var second = await InsertNoteAsync(sender, receiver, "ikinci");

        Assert.Equal(1L, await ScalarAsync<long>(
            "SELECT count(*) FROM notifications.serving_handoff_notes WHERE to_user_id = @id AND consumed_at IS NULL;", ("id", receiver)));
        Assert.True(await ScalarAsync<bool>(
            "SELECT consumed_at IS NOT NULL FROM notifications.serving_handoff_notes WHERE handoff_note_id = @id;", ("id", first)));
        Assert.True(await ScalarAsync<bool>(
            "SELECT consumed_at IS NULL FROM notifications.serving_handoff_notes WHERE handoff_note_id = @id;", ("id", second)));
    }

    [Fact]
    public async Task ANoteInsertedAlreadyConsumedLeavesThePendingOneAlone()
    {
        var sender = await SeedUserAsync();
        var receiver = await SeedUserAsync();
        var pending = await InsertNoteAsync(sender, receiver, "bekleyen");

        await ExecuteAsync(
            """
            INSERT INTO notifications.serving_handoff_notes (handoff_note_id, from_user_id, to_user_id, note, consumed_at)
            VALUES (@id, @from, @to, 'eski', now());
            """,
            ("id", Guid.NewGuid()), ("from", sender), ("to", receiver));

        Assert.True(await ScalarAsync<bool>(
            "SELECT consumed_at IS NULL FROM notifications.serving_handoff_notes WHERE handoff_note_id = @id;", ("id", pending)));
    }

    [Fact]
    public async Task TheUniqueIndexRefusesASecondPendingNoteEvenWithoutTheTrigger()
    {
        var sender = await SeedUserAsync();
        var receiver = await SeedUserAsync();
        await ExecuteAsync("ALTER TABLE notifications.serving_handoff_notes DISABLE TRIGGER trg_serving_handoff_notes_supersede;");
        await InsertNoteAsync(sender, receiver, "birinci");

        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertNoteAsync(sender, receiver, "ikinci"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, failure.SqlState);
    }

    [Fact]
    public async Task EachWaiterKeepsTheirOwnPendingNote()
    {
        var sender = await SeedUserAsync();
        var receiverA = await SeedUserAsync();
        var receiverB = await SeedUserAsync();
        await InsertNoteAsync(sender, receiverA, "A için");
        await InsertNoteAsync(sender, receiverB, "B için");

        Assert.Equal(2L, await ScalarAsync<long>(
            "SELECT count(*) FROM notifications.serving_handoff_notes WHERE consumed_at IS NULL;"));
    }

    [Fact]
    public async Task SimultaneousHandOffsToOneWaiterNeitherFailNorLeaveTwoPendingNotes()
    {
        var sender = await SeedUserAsync();
        var receiver = await SeedUserAsync();

        await Task.WhenAll(Enumerable.Range(0, 8).Select(index => InsertNoteAsync(sender, receiver, $"not {index}")));

        Assert.Equal(1L, await ScalarAsync<long>(
            "SELECT count(*) FROM notifications.serving_handoff_notes WHERE to_user_id = @id AND consumed_at IS NULL;", ("id", receiver)));
        Assert.Equal(8L, await ScalarAsync<long>(
            "SELECT count(*) FROM notifications.serving_handoff_notes WHERE to_user_id = @id;", ("id", receiver)));
    }

    [Fact]
    public async Task TheMigrationClosesDuplicatesLeftByTheOldSchemaAndKeepsTheNewestNote()
    {
        var sender = await SeedUserAsync();
        var receiver = await SeedUserAsync();
        await ExecuteAsync("DROP TRIGGER trg_serving_handoff_notes_supersede ON notifications.serving_handoff_notes;");
        await ExecuteAsync("DROP INDEX notifications.ux_serving_handoff_notes_one_pending;");
        var oldest = await InsertNoteAsync(sender, receiver, "en eski", createdMinutesAgo: 30);
        var middle = await InsertNoteAsync(sender, receiver, "ortadaki", createdMinutesAgo: 20);
        var newest = await InsertNoteAsync(sender, receiver, "en yeni", createdMinutesAgo: 10);

        var up = Path.Combine(
            FindRepositoryRoot(), "database", "migrations", "V1", "V1-RMD-692", "200-handoff-note-uniqueness-and-retention-indexes.up.sql");
        await ExecuteAsync(await File.ReadAllTextAsync(up));

        Assert.Equal(newest, await ScalarAsync<Guid>(
            "SELECT handoff_note_id FROM notifications.serving_handoff_notes WHERE to_user_id = @id AND consumed_at IS NULL;", ("id", receiver)));
        Assert.Equal(2L, await ScalarAsync<long>(
            "SELECT count(*) FROM notifications.serving_handoff_notes WHERE handoff_note_id = ANY(@ids) AND consumed_at IS NOT NULL;",
            ("ids", new[] { oldest, middle })));
        Assert.Equal(1L, await ScalarAsync<long>(
            "SELECT count(*) FROM pg_indexes WHERE indexname = 'ux_serving_handoff_notes_one_pending';"));
    }

    [Theory]
    [InlineData("DELETE FROM table_mgmt.tables WHERE table_id = @table;")]
    [InlineData("DELETE FROM identity.users WHERE user_id = @user;")]
    public async Task AHelpRequestIsNotErasedByRemovingItsTableOrItsWaiter(string deleteSql)
    {
        var user = await SeedUserAsync();
        var table = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO table_mgmt.tables (table_id, zone_id, table_number, capacity, active, current_status, row_version)
            VALUES (@id, NULL, @number, 2, true, 'Available', 1);
            """,
            ("id", table), ("number", $"S-{table:N}"[..12]));
        await ExecuteAsync(
            """
            INSERT INTO notifications.help_requests (help_request_id, table_id, table_number, requested_by_user_id, request_type)
            VALUES (@id, @table, '1', @user, 'Other');
            """,
            ("id", Guid.NewGuid()), ("table", table), ("user", user));

        var failure = await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteAsync(deleteSql, ("table", table), ("user", user)));

        Assert.Equal(PostgresErrorCodes.RestrictViolation, failure.SqlState);
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM notifications.help_requests;"));
    }

    private async Task<Guid> InsertNoteAsync(Guid from, Guid to, string note, int createdMinutesAgo = 0)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO notifications.serving_handoff_notes (handoff_note_id, from_user_id, to_user_id, note, created_at)
            VALUES (@id, @from, @to, @note, now() - make_interval(mins => @ago));
            """,
            ("id", id), ("from", from), ("to", to), ("note", note), ("ago", createdMinutesAgo));
        return id;
    }

    private async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = _dataSource!.CreateCommand(sql);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        return (T)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Scalar returned null."));
    }

    private string RunHousekeeping(params string[] extra)
    {
        using var output = new StringWriter();
        var originalOut = Console.Out;
        int exit;
        try
        {
            Console.SetOut(output);
            exit = ALKAROS.Host.Program.Main(["housekeeping", "--db-url", _database.Url, .. extra]);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Equal((int)HostExitCode.Success, exit);
        return output.ToString();
    }

    private async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = _dataSource!.CreateCommand(sql);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<long> ScalarCountAsync(string sql)
    {
        await using var command = _dataSource!.CreateCommand(sql);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private Task<long> CountAsync(string table) => ScalarCountAsync($"SELECT count(*) FROM {table};");

    private async Task<Guid> SeedTableAsync()
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO table_mgmt.tables (table_id, zone_id, table_number, capacity, active, current_status, row_version)
            VALUES (@id, NULL, @number, 2, true, 'Available', 1);
            """,
            ("id", id), ("number", $"H-{id:N}"[..12]));
        return id;
    }

    private Task InsertHandoffNoteAsync(Guid from, Guid to, int createdDaysAgo)
        => ExecuteAsync(
            """
            INSERT INTO notifications.serving_handoff_notes (handoff_note_id, from_user_id, to_user_id, note, created_at)
            VALUES (@id, @from, @to, 'not', now() - make_interval(days => @ago));
            """,
            ("id", Guid.NewGuid()), ("from", from), ("to", to), ("ago", createdDaysAgo));

    private Task InsertPushSubscriptionAsync(string name, Guid userId, int createdDaysAgo, int? lastSuccessDaysAgo)
        => ExecuteAsync(
            """
            INSERT INTO notifications.push_subscriptions
                (subscription_id, endpoint, p256dh, auth, user_id, terminal_id, created_at, last_success_at)
            VALUES (@id, @endpoint, 'k', 'a', @user, @terminal, now() - make_interval(days => @created),
                    CASE WHEN @has_success THEN now() - make_interval(days => @success) END);
            """,
            ("id", Guid.NewGuid()), ("endpoint", $"https://push.example.net/{name}"), ("user", userId),
            ("terminal", Guid.NewGuid()), ("created", createdDaysAgo),
            ("has_success", lastSuccessDaysAgo.HasValue), ("success", lastSuccessDaysAgo ?? 0));

    private Task InsertHelpRequestAsync(Guid tableId, Guid userId, int createdDaysAgo)
        => ExecuteAsync(
            """
            INSERT INTO notifications.help_requests (help_request_id, table_id, table_number, requested_by_user_id, request_type, created_at)
            VALUES (@id, @table, '1', @user, 'Other', now() - make_interval(days => @ago));
            """,
            ("id", Guid.NewGuid()), ("table", tableId), ("user", userId), ("ago", createdDaysAgo));

    private async Task<Guid> InsertDisplayTerminalAsync()
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            "INSERT INTO customer_display.terminals (terminal_id, created_at, updated_at) VALUES (@id, now(), now());",
            ("id", id));
        return id;
    }

    private Task InsertDisplaySessionAsync(Guid terminalId, int expiresDays, int? revokedDays)
        => ExecuteAsync(
            """
            INSERT INTO customer_display.display_sessions
                (session_id, display_id, terminal_id, token_hash, created_at, expires_at, revoked_at)
            VALUES (@id, @display, @terminal, md5(@id::text) || md5(@display::text), now() - interval '40 days',
                    now() + make_interval(days => @expires),
                    CASE WHEN @has_revoke THEN now() + make_interval(days => @revoked) END);
            """,
            ("id", Guid.NewGuid()), ("display", Guid.NewGuid()), ("terminal", terminalId), ("expires", expiresDays),
            ("has_revoke", revokedDays.HasValue), ("revoked", revokedDays ?? 0));

    private Task InsertPairingRequestAsync(Guid terminalId, int expiresDays, int? consumedDays)
        => ExecuteAsync(
            """
            INSERT INTO customer_display.pairing_requests
                (request_id, display_id, terminal_id, pairing_secret_hash, code_hash, expires_at, consumed_at, created_at)
            VALUES (@id, @display, @terminal, md5(@id::text) || md5(@display::text), md5(@display::text) || md5(@id::text),
                    now() + make_interval(days => @expires),
                    CASE WHEN @has_consumed THEN now() + make_interval(days => @consumed) END,
                    now() - interval '40 days');
            """,
            ("id", Guid.NewGuid()), ("display", Guid.NewGuid()), ("terminal", terminalId), ("expires", expiresDays),
            ("has_consumed", consumedDays.HasValue), ("consumed", consumedDays ?? 0));

    private async Task<Guid> SeedUserAsync(bool active = true)
    {
        var userId = Guid.NewGuid();
        await using var command = _dataSource!.CreateCommand(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@id, @username, 'x', 'Housekeeping Test', @active);
            """);
        command.Parameters.AddWithValue("active", active);
        command.Parameters.AddWithValue("id", userId);
        command.Parameters.AddWithValue("username", $"hk-{userId:N}"[..20]);
        await command.ExecuteNonQueryAsync();
        return userId;
    }

    private async Task InsertIdempotencyKeyAsync(string clientId, string operationId, int expiresInHours)
    {
        await using var command = _dataSource!.CreateCommand(
            """
            INSERT INTO idempotency_keys (client_id, operation_id, request_hash, response_envelope, created_at, expires_at)
            VALUES (@client, @op, repeat('0', 64), '\x00'::bytea, now() - interval '1 day',
                    now() + make_interval(hours => @hours));
            """);
        command.Parameters.AddWithValue("client", clientId);
        command.Parameters.AddWithValue("op", operationId);
        command.Parameters.AddWithValue("hours", expiresInHours);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Guid> InsertSessionAsync(Guid userId, string deviceId, int expiresDays, int? revokedDays)
    {
        var sessionId = Guid.NewGuid();
        await using var command = _dataSource!.CreateCommand(
            """
            INSERT INTO identity.device_sessions
                (session_id, user_id, device_id, token_hash, created_at, expires_at, revoked_at, last_seen_at)
            VALUES (@id, @user, @device, @token, now() - interval '40 days',
                    now() + make_interval(days => @expires),
                    CASE WHEN @has_revoke THEN now() + make_interval(days => @revoked) ELSE NULL END,
                    now());
            """);
        command.Parameters.AddWithValue("id", sessionId);
        command.Parameters.AddWithValue("user", userId);
        command.Parameters.AddWithValue("device", deviceId);
        command.Parameters.AddWithValue("token", sessionId.ToString("N"));
        command.Parameters.AddWithValue("expires", expiresDays);
        command.Parameters.AddWithValue("has_revoke", revokedDays.HasValue);
        command.Parameters.AddWithValue("revoked", revokedDays ?? 0);
        await command.ExecuteNonQueryAsync();
        return sessionId;
    }

    private async Task<long> CountIdempotencyAsync(string clientId, string operationId)
    {
        await using var command = _dataSource!.CreateCommand(
            "SELECT count(*) FROM idempotency_keys WHERE client_id = @client AND operation_id = @op;");
        command.Parameters.AddWithValue("client", clientId);
        command.Parameters.AddWithValue("op", operationId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task<bool> SessionExistsAsync(Guid sessionId)
    {
        await using var command = _dataSource!.CreateCommand(
            "SELECT count(*) FROM identity.device_sessions WHERE session_id = @id;");
        command.Parameters.AddWithValue("id", sessionId);
        return (long)(await command.ExecuteScalarAsync())! == 1L;
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
