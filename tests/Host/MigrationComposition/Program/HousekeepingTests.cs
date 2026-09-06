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

    private async Task<Guid> SeedUserAsync()
    {
        var userId = Guid.NewGuid();
        await using var command = _dataSource!.CreateCommand(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@id, @username, 'x', 'Housekeeping Test', true);
            """);
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
