using ALKAROS.Identity.DeviceSessions.Tests.Fixtures;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Identity.DeviceSessions.Tests;

/// <summary>
/// Verifies the 013-device-session-lifetime migration forward/down/forward
/// lifecycle and check constraint enforcement.
/// </summary>
public sealed class DeviceSessionLifetimeMigrationTests : IClassFixture<DeviceSessionsTestDatabase>
{
    private readonly DeviceSessionsTestDatabase _database;
    private const string CheckViolation = "23514";

    public DeviceSessionLifetimeMigrationTests(DeviceSessionsTestDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task LifetimeConstraintRejectsExpiredAtBeforeCreatedAt()
    {
        var upSql = "ALTER TABLE identity.device_sessions ADD CONSTRAINT chk_device_sessions_lifetime CHECK (expires_at > created_at);";
        var downSql = "ALTER TABLE identity.device_sessions DROP CONSTRAINT IF EXISTS chk_device_sessions_lifetime;";

        await _database.ExecuteAsync(upSql);
        try
        {
            var userId = await _database.InsertUserAsync();
            var now = DateTimeOffset.UtcNow;

            var act = async () =>
            {
                await using var connection = await _database.DataSource.OpenConnectionAsync();
                await using var cmd = connection.CreateCommand();
                cmd.CommandText =
                    """
                    INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, created_at, expires_at)
                    VALUES (gen_random_uuid(), @user_id, 'device-invalid', 'hash-123', @created_at, @expires_at);
                    """;
                cmd.Parameters.AddWithValue("user_id", userId);
                cmd.Parameters.AddWithValue("created_at", now);
                cmd.Parameters.AddWithValue("expires_at", now.AddMinutes(-5));
                await cmd.ExecuteNonQueryAsync();
            };

            var ex = await Assert.ThrowsAsync<PostgresException>(act);
            ex.SqlState.Should().Be(CheckViolation);
        }
        finally
        {
            await _database.ExecuteAsync(downSql);
        }
    }

    [Fact]
    public async Task LifetimeMigrationForwardDownForwardSymmetry()
    {
        var upSql = "ALTER TABLE identity.device_sessions ADD CONSTRAINT chk_device_sessions_lifetime CHECK (expires_at > created_at);";
        var downSql = "ALTER TABLE identity.device_sessions DROP CONSTRAINT IF EXISTS chk_device_sessions_lifetime;";

        // Up
        await _database.ExecuteAsync(upSql);

        // Down
        await _database.ExecuteAsync(downSql);

        // Up again
        await _database.ExecuteAsync(upSql);
        await _database.ExecuteAsync(downSql);
    }
}
