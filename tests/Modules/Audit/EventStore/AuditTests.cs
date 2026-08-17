namespace ALKAROS.Audit.EventStore.Tests;

using ALKAROS.Audit.EventStore;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Npgsql;
using Xunit;

public sealed class AuditTestDatabase : PgTestDatabase
{
    public AuditTestDatabase()
        : base("alkaros_ops001_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
        {
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
        }
    }
}

public sealed class AuditSanitizerUnitTests
{
    private readonly AuditSanitizer _sanitizer = new();

    [Fact]
    public void RedactsTopLevelSensitiveFields()
    {
        var json = """
            {
                "user_id": "123",
                "password": "SuperSecretPassword123!",
                "pin": "1234",
                "secret_key": "xyz-abc-secret",
                "token": "bearer-jwt-token-val",
                "pan": "4111111111111111",
                "cvv": "123",
                "amount": 250.00
            }
            """;

        var sanitized = _sanitizer.SanitizeJson(json);
        sanitized.Should().NotBeNull();

        sanitized.Should().NotContain("SuperSecretPassword123!");
        sanitized.Should().NotContain("1234");
        sanitized.Should().NotContain("xyz-abc-secret");
        sanitized.Should().NotContain("bearer-jwt-token-val");
        sanitized.Should().NotContain("4111111111111111");

        sanitized.Should().Contain("\"password\":\"[REDACTED]\"");
        sanitized.Should().Contain("\"pin\":\"[REDACTED]\"");
        sanitized.Should().Contain("\"secret_key\":\"[REDACTED]\"");
        sanitized.Should().Contain("\"token\":\"[REDACTED]\"");
        sanitized.Should().Contain("\"pan\":\"[REDACTED]\"");
        sanitized.Should().Contain("\"cvv\":\"[REDACTED]\"");
        sanitized.Should().Contain("\"amount\":250");
    }

    [Fact]
    public void RedactsNestedSensitiveFieldsInObjectsAndArrays()
    {
        var json = """
            {
                "order_id": "ord-1",
                "payment_details": {
                    "provider": "QNB",
                    "card": {
                        "card_number": "5400000000000000",
                        "cvc": "999"
                    }
                },
                "items": [
                    { "name": "Burger", "auth_token": "item-secret-token" }
                ]
            }
            """;

        var sanitized = _sanitizer.SanitizeJson(json);

        sanitized.Should().NotContain("5400000000000000");
        sanitized.Should().NotContain("999");
        sanitized.Should().NotContain("item-secret-token");

        sanitized.Should().Contain("\"card_number\":\"[REDACTED]\"");
        sanitized.Should().Contain("\"cvc\":\"[REDACTED]\"");
        sanitized.Should().Contain("\"auth_token\":\"[REDACTED]\"");
        sanitized.Should().Contain("\"name\":\"Burger\"");
    }

    [Fact]
    public void SerializeAndSanitizeHandlesPocoSafely()
    {
        var poco = new
        {
            UserName = "waiter_1",
            Pin = "9876",
            Password = "MyPassword",
            Role = "Waiter"
        };

        var sanitized = _sanitizer.SerializeAndSanitize(poco);

        sanitized.Should().NotContain("9876");
        sanitized.Should().NotContain("MyPassword");
        sanitized.Should().Contain("\"Pin\":\"[REDACTED]\"");
        sanitized.Should().Contain("\"Password\":\"[REDACTED]\"");
        sanitized.Should().Contain("\"Role\":\"Waiter\"");
    }
}

public sealed class AuditEventUnitTests
{
    [Fact]
    public void ValidateThrowsOnMissingRequiredFields()
    {
        var act1 = () => new AuditEvent(Guid.Empty, "E", "A", Guid.NewGuid(), "U", "C").Validate();
        act1.Should().Throw<ArgumentException>().WithParameterName("Id");

        var act2 = () => new AuditEvent(Guid.NewGuid(), "", "A", Guid.NewGuid(), "U", "C").Validate();
        act2.Should().Throw<ArgumentException>().WithParameterName("EventName");

        var act3 = () => new AuditEvent(Guid.NewGuid(), "E", "", Guid.NewGuid(), "U", "C").Validate();
        act3.Should().Throw<ArgumentException>().WithParameterName("AggregateType");

        var act4 = () => new AuditEvent(Guid.NewGuid(), "E", "A", Guid.Empty, "U", "C").Validate();
        act4.Should().Throw<ArgumentException>().WithParameterName("AggregateId");

        var act5 = () => new AuditEvent(Guid.NewGuid(), "E", "A", Guid.NewGuid(), "", "C").Validate();
        act5.Should().Throw<ArgumentException>().WithParameterName("ActorType");

        var act6 = () => new AuditEvent(Guid.NewGuid(), "E", "A", Guid.NewGuid(), "U", "").Validate();
        act6.Should().Throw<ArgumentException>().WithParameterName("CorrelationId");
    }
}

public sealed class PostgresAuditEventStoreIntegrationTests : IClassFixture<AuditTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly PostgresAuditEventStore _eventStore;

    public PostgresAuditEventStoreIntegrationTests(AuditTestDatabase database)
    {
        _dataSource = database.DataSource;
        _eventStore = new PostgresAuditEventStore(database.DataSource);
    }

    [Fact]
    public async Task AppendAsyncPersistsSanitizedEventAndRetrievesByAggregate()
    {
        var orderId = Guid.NewGuid();
        var correlationId = "corr-" + Guid.NewGuid().ToString("N")[..8];

        var rawBefore = "{\"status\": \"Draft\", \"user_pin\": \"1234\"}";
        var rawAfter = "{\"status\": \"Submitted\", \"auth_token\": \"secret-val\"}";

        var evt = new AuditEvent(
            Guid.NewGuid(),
            "Order.Submitted",
            "Order",
            orderId,
            "User",
            correlationId,
            actorId: Guid.NewGuid(),
            reason: "Customer checkout",
            beforeStateJson: rawBefore,
            afterStateJson: rawAfter);

        await _eventStore.AppendAsync(evt);

        var retrieved = await _eventStore.GetByAggregateAsync("Order", orderId);
        retrieved.Should().ContainSingle();

        var stored = retrieved[0];
        stored.Id.Should().Be(evt.Id);
        stored.EventName.Should().Be("Order.Submitted");
        stored.AggregateType.Should().Be("Order");
        stored.AggregateId.Should().Be(orderId);
        stored.Reason.Should().Be("Customer checkout");
        stored.CorrelationId.Should().Be(correlationId);

        // Sensitive fields redacted before DB persistence
        stored.BeforeStateJson.Should().NotContain("1234");
        stored.BeforeStateJson.Should().Contain("\"[REDACTED]\"");

        stored.AfterStateJson.Should().NotContain("secret-val");
        stored.AfterStateJson.Should().Contain("\"[REDACTED]\"");
    }

    [Fact]
    public async Task AppendBatchAsyncPersistsMultipleEventsAndQueriesByCorrelation()
    {
        var correlationId = "corr-batch-" + Guid.NewGuid().ToString("N")[..8];
        var orderId = Guid.NewGuid();

        var evt1 = new AuditEvent(Guid.NewGuid(), "Order.ItemVoided", "Order", orderId, "User", correlationId, reason: "Kitchen error");
        var evt2 = new AuditEvent(Guid.NewGuid(), "Order.ItemComplimentary", "Order", orderId, "User", correlationId, reason: "Manager promo");

        await _eventStore.AppendBatchAsync([evt1, evt2]);

        var retrieved = await _eventStore.GetByCorrelationIdAsync(correlationId);
        retrieved.Should().HaveCount(2);
        retrieved.Select(e => e.EventName).Should().ContainInOrder("Order.ItemVoided", "Order.ItemComplimentary");
    }

    [Fact]
    public async Task UpdateOperationOnAuditTableIsForbiddenAndFailsClosedAtDatabaseLevel()
    {
        var evtId = Guid.NewGuid();
        var evt = new AuditEvent(evtId, "Security.PinLogin", "User", Guid.NewGuid(), "User", "corr-immut-1");
        await _eventStore.AppendAsync(evt);

        // Attempting direct UPDATE on audit.audit_events
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var updateCmd = connection.CreateCommand();
        updateCmd.CommandText = "UPDATE audit.audit_events SET reason = 'Tampered Reason' WHERE id = @id;";
        updateCmd.Parameters.AddWithValue("id", evtId);

        var act = async () => await updateCmd.ExecuteNonQueryAsync();

        await act.Should().ThrowAsync<PostgresException>()
            .WithMessage("*audit_events table is append-only*");
    }

    [Fact]
    public async Task DeleteOperationOnAuditTableIsForbiddenAndFailsClosedAtDatabaseLevel()
    {
        var evtId = Guid.NewGuid();
        var evt = new AuditEvent(evtId, "Security.RoleAssigned", "User", Guid.NewGuid(), "User", "corr-immut-2");
        await _eventStore.AppendAsync(evt);

        // Attempting direct DELETE on audit.audit_events
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var deleteCmd = connection.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM audit.audit_events WHERE id = @id;";
        deleteCmd.Parameters.AddWithValue("id", evtId);

        var act = async () => await deleteCmd.ExecuteNonQueryAsync();

        await act.Should().ThrowAsync<PostgresException>()
            .WithMessage("*audit_events table is append-only*");
    }
}

public sealed class PostgresAuditDownSqlTests : IClassFixture<AuditTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresAuditDownSqlTests(AuditTestDatabase database)
    {
        _dataSource = database.DataSource;
    }

    [Fact]
    public async Task DownSqlDropsAuditSchemaAndTables()
    {
        var downSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "015-audit-log.down.sql"));

        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = downSql;
        await cmd.ExecuteNonQueryAsync();

        // Verify table no longer exists
        await using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText =
            """
            SELECT EXISTS (
                SELECT FROM information_schema.tables
                WHERE table_schema = 'audit' AND table_name = 'audit_events'
            );
            """;
        var exists = (bool)(await checkCmd.ExecuteScalarAsync())!;
        exists.Should().BeFalse();
    }
}
