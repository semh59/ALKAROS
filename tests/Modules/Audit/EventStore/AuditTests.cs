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

    [Fact]
    public void MalformedJsonRedactsSensitiveKeywordsAndReturnsValidJson()
    {
        var raw = "user: admin, password: SuperSecretPassword123!, pin: 4321";
        var sanitized = _sanitizer.SanitizeJson(raw);

        sanitized.Should().NotBeNull();
        sanitized.Should().NotContain("SuperSecretPassword123!");
        sanitized.Should().NotContain("4321");
        sanitized.Should().Contain("[REDACTED]");
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
    public async Task AppendInATransactionIsSanitizedAndFollowsTheTransactionOutcome()
    {
        var rolledBack = new AuditEvent(Guid.NewGuid(), "Tx.RolledBack", "Order", Guid.NewGuid(), "User", "corr-tx-" + Guid.NewGuid().ToString("N")[..8]);
        var committed = new AuditEvent(
            Guid.NewGuid(), "Tx.Committed", "Order", Guid.NewGuid(), "User", "corr-tx-" + Guid.NewGuid().ToString("N")[..8],
            afterStateJson: "{\"auth_token\": \"secret-val\"}");

        await using (var connection = await _dataSource.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await _eventStore.AppendAsync(rolledBack, connection, transaction);
            await transaction.RollbackAsync();
        }

        await using (var connection = await _dataSource.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await _eventStore.AppendAsync(committed, connection, transaction);
            (await _eventStore.GetByCorrelationIdAsync(committed.CorrelationId)).Should().BeEmpty("the row is not committed yet");
            await transaction.CommitAsync();
        }

        (await _eventStore.GetByCorrelationIdAsync(rolledBack.CorrelationId)).Should().BeEmpty();
        var stored = await _eventStore.GetByCorrelationIdAsync(committed.CorrelationId);
        stored.Should().ContainSingle();
        stored[0].AfterStateJson.Should().NotContain("secret-val");
    }

    [Fact]
    public async Task AnInvalidEventIsRejectedOnBothWritePathsAndWritesNothing()
    {
        var correlationId = "corr-invalid-" + Guid.NewGuid().ToString("N")[..8];
        var invalid = new AuditEvent(Guid.NewGuid(), "", "Order", Guid.NewGuid(), "User", correlationId);

        await ((Func<Task>)(() => _eventStore.AppendAsync(invalid))).Should().ThrowAsync<ArgumentException>();
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await ((Func<Task>)(() => _eventStore.AppendAsync(invalid, connection, transaction))).Should().ThrowAsync<ArgumentException>();
        await transaction.RollbackAsync();

        (await _eventStore.GetByCorrelationIdAsync(correlationId)).Should().BeEmpty();
    }

    [Fact]
    public async Task TheSharedWriterSanitizesAndValidatesLikeTheStore()
    {
        var correlationId = "corr-writer-" + Guid.NewGuid().ToString("N")[..8];
        var withSecret = new AuditEvent(
            Guid.NewGuid(), "Writer.Event", "Order", Guid.NewGuid(), "User", correlationId,
            metadataJson: "{\"auth_token\": \"secret-val\", \"hadNote\": true}");
        var invalid = new AuditEvent(Guid.NewGuid(), "", "Order", Guid.NewGuid(), "User", correlationId);

        await using (var connection = await _dataSource.OpenConnectionAsync())
        {
            await AuditEventWriter.WriteAsync(connection, null, withSecret, null, default);
            await ((Func<Task>)(() => AuditEventWriter.WriteAsync(connection, null, invalid, null, default))).Should().ThrowAsync<ArgumentException>();
        }

        var stored = await _eventStore.GetByCorrelationIdAsync(correlationId);
        stored.Should().ContainSingle();
        stored[0].MetadataJson.Should().NotContain("secret-val").And.Contain("hadNote");
    }

    [Fact]
    public async Task AppendBatchAsyncRejectsANullEventAndWritesNothing()
    {
        var correlationId = "corr-null-" + Guid.NewGuid().ToString("N")[..8];
        var first = new AuditEvent(Guid.NewGuid(), "Batch.First", "Order", Guid.NewGuid(), "User", correlationId);

        var act = () => _eventStore.AppendBatchAsync([first, null!]);

        await act.Should().ThrowAsync<ArgumentNullException>();
        (await _eventStore.GetByCorrelationIdAsync(correlationId)).Should().BeEmpty();
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

public sealed class AuditStoreWithoutTransactionSupportTests
{
    private sealed class PlainStore : IAuditEventStore
    {
        public Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task AppendBatchAsync(IEnumerable<AuditEvent> auditEvents, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<AuditEvent>> GetByAggregateAsync(string aggregateType, Guid aggregateId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AuditEvent>>([]);

        public Task<IReadOnlyList<AuditEvent>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AuditEvent>>([]);
    }

    [Fact]
    public async Task AStoreThatCannotJoinATransactionRefusesInsteadOfWritingOutsideIt()
    {
        IAuditEventStore store = new PlainStore();
        var evt = new AuditEvent(Guid.NewGuid(), "Plain.Event", "Order", Guid.NewGuid(), "User", "corr-plain");

        var act = () => store.AppendAsync(evt, null!, null!);

        await act.Should().ThrowAsync<NotSupportedException>();
    }
}

public sealed class AuditEventHardeningTests
{
    [Fact]
    public void OccurredAtIsNormalizedToUtc()
    {
        var local = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(3));

        var auditEvent = new AuditEvent(Guid.NewGuid(), "E", "A", Guid.NewGuid(), "U", "C", occurredAt: local);

        auditEvent.OccurredAt.Offset.Should().Be(TimeSpan.Zero);
        auditEvent.OccurredAt.Should().Be(local);
    }

    [Theory]
    [InlineData("EventName", 129)]
    [InlineData("AggregateType", 65)]
    [InlineData("ActorType", 33)]
    [InlineData("CorrelationId", 129)]
    [InlineData("CausationId", 129)]
    public void ValidateRejectsValuesLongerThanTheColumn(string field, int length)
    {
        var tooLong = new string('x', length);
        var auditEvent = new AuditEvent(
            Guid.NewGuid(),
            field == "EventName" ? tooLong : "E",
            field == "AggregateType" ? tooLong : "A",
            Guid.NewGuid(),
            field == "ActorType" ? tooLong : "U",
            field == "CorrelationId" ? tooLong : "C",
            causationId: field == "CausationId" ? tooLong : null);

        var act = () => auditEvent.Validate();

        act.Should().Throw<ArgumentException>().WithParameterName(field);
    }

    [Theory]
    [InlineData(128, 64, 32, 128, 128)]
    public void ValidateAcceptsValuesExactlyAtTheColumnLimit(int eventName, int aggregateType, int actorType, int correlation, int causation)
    {
        var auditEvent = new AuditEvent(
            Guid.NewGuid(), new string('e', eventName), new string('a', aggregateType), Guid.NewGuid(),
            new string('u', actorType), new string('c', correlation), causationId: new string('k', causation));

        var act = () => auditEvent.Validate();

        act.Should().NotThrow();
    }
}
