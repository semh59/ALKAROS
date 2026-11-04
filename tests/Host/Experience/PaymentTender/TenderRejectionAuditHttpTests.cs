using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace ALKAROS.Host.Experience.PaymentTender.Tests;

public sealed partial class PaymentTenderHttpTests
{
    [Theory]
    [InlineData("Eft", 51)]
    [InlineData("BankCard", 51)]
    public async Task ATenderOverTheRemainingAmountIsRefusedAndLeavesOneAuditEvent(string method, int amount)
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, $"rmd794-{method}-over");
        var billId = await _database.SeedBillAsync(payable: 50m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var response = await PostAsync(client, TendersPath(terminalId, billId), cookie,
            new { Method = method, Amount = (decimal)amount, IdempotencyKey = $"rmd794-{method}-1" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("TENDER_OVER_ALLOCATION", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetProperty("code").GetString());
        var rejection = await ReadRejectionAsync(billId);
        Assert.Equal(1, rejection.Count);
        Assert.NotNull(rejection.ActorId);
        Assert.Equal(terminalId, rejection.State.GetProperty("terminalId").GetGuid());
        Assert.Equal(method, rejection.State.GetProperty("method").GetString());
        Assert.Equal(51m, rejection.State.GetProperty("amount").GetDecimal());
        Assert.Equal(50m, rejection.State.GetProperty("remainingPayable").GetDecimal());
        Assert.Equal("OverRemaining", rejection.State.GetProperty("reason").GetString());
        Assert.Equal($"rmd794-{method}-1", rejection.State.GetProperty("idempotencyKey").GetString());
        Assert.Equal(0L, await _database.PaymentCountAsync(billId));
    }

    [Fact]
    public async Task ATenderWithinTheRemainingAmountLeavesNoRejectionEvent()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "rmd794-within");
        var billId = await _database.SeedBillAsync(payable: 100m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var response = await PostAsync(client, TendersPath(terminalId, billId), cookie,
            new { Method = "Eft", Amount = 30m, IdempotencyKey = "rmd794-within-1" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, (await ReadRejectionAsync(billId)).Count);
    }

    [Fact]
    public async Task AFailingAuditWriteDoesNotChangeTheRefusalTheCashierSees()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "rmd794-audit-down");
        var billId = await _database.SeedBillAsync(payable: 50m);
        await using (var block = _database.DataSource.CreateCommand(
            "ALTER TABLE audit.audit_events ADD CONSTRAINT ck_test_no_tender_rejected CHECK (event_name <> 'payment.tender-rejected');"))
        {
            await block.ExecuteNonQueryAsync();
        }
        try
        {
            await using var app = await StartAsync();
            using var client = CreateClient(app);

            var response = await PostAsync(client, TendersPath(terminalId, billId), cookie,
                new { Method = "Eft", Amount = 51m, IdempotencyKey = "rmd794-down-1" });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error");
            Assert.Equal("TENDER_OVER_ALLOCATION", error.GetProperty("code").GetString());
            Assert.Contains("aşıyor", error.GetProperty("message").GetString());
            Assert.Equal(0, (await ReadRejectionAsync(billId)).Count);
        }
        finally
        {
            await using var unblock = _database.DataSource.CreateCommand(
                "ALTER TABLE audit.audit_events DROP CONSTRAINT ck_test_no_tender_rejected;");
            await unblock.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task AnAuditStoreThatFailsIsTriedOnceAndTheFailureIsLoggedAsAnError()
    {
        var store = new FailingAuditStore();
        var logs = new CapturingLoggerFactory();
        var billId = Guid.NewGuid();

        await ALKAROS.Host.DualScreen.DualScreenApplication.RecordRejectedTenderAsync(
            store, logs, new Microsoft.AspNetCore.Http.DefaultHttpContext(), Guid.NewGuid(), Guid.NewGuid(), billId,
            "Eft", 51m, 50m, new string('k', 300));

        Assert.Equal(1, store.Attempts);
        var entry = Assert.Single(logs.Entries);
        Assert.Equal(6943, entry.EventId);
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Error, entry.Level);
        Assert.Contains(billId.ToString(), entry.Message);
    }

    [Fact]
    public async Task AnIdempotencyKeyLongerThanTheLimitIsCutBeforeItIsWrittenToTheAuditTrail()
    {
        var store = new RecordingAuditStore();

        await ALKAROS.Host.DualScreen.DualScreenApplication.RecordRejectedTenderAsync(
            store, new CapturingLoggerFactory(), new Microsoft.AspNetCore.Http.DefaultHttpContext(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "Cash", 10m, 5m, new string('k', 300));

        var written = Assert.Single(store.Events);
        Assert.True(store.Token.CanBeCanceled && !store.Token.IsCancellationRequested);
        Assert.Equal("OverRemaining", written.Reason);
        Assert.Equal("payment.tender-rejected", written.EventName);
        Assert.Equal(100, JsonDocument.Parse(written.AfterStateJson!).RootElement.GetProperty("idempotencyKey").GetString()!.Length);
    }

    private sealed class FailingAuditStore : ALKAROS.Audit.EventStore.IAuditEventStore
    {
        public int Attempts { get; private set; }

        public Task AppendAsync(ALKAROS.Audit.EventStore.AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Attempts++;
            throw new InvalidOperationException("audit store down");
        }

        public Task AppendBatchAsync(IEnumerable<ALKAROS.Audit.EventStore.AuditEvent> auditEvents, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ALKAROS.Audit.EventStore.AuditEvent>> GetByAggregateAsync(string aggregateType, Guid aggregateId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ALKAROS.Audit.EventStore.AuditEvent>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingAuditStore : ALKAROS.Audit.EventStore.IAuditEventStore
    {
        public List<ALKAROS.Audit.EventStore.AuditEvent> Events { get; } = [];

        public CancellationToken Token { get; private set; }

        public Task AppendAsync(ALKAROS.Audit.EventStore.AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Token = cancellationToken;
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }

        public Task AppendBatchAsync(IEnumerable<ALKAROS.Audit.EventStore.AuditEvent> auditEvents, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ALKAROS.Audit.EventStore.AuditEvent>> GetByAggregateAsync(string aggregateType, Guid aggregateId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ALKAROS.Audit.EventStore.AuditEvent>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class CapturingLoggerFactory : Microsoft.Extensions.Logging.ILoggerFactory
    {
        public List<(int EventId, Microsoft.Extensions.Logging.LogLevel Level, string Message)> Entries { get; } = [];

        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => new Capture(Entries);

        public void AddProvider(Microsoft.Extensions.Logging.ILoggerProvider provider) { }

        public void Dispose() { }

        private sealed class Capture(List<(int EventId, Microsoft.Extensions.Logging.LogLevel Level, string Message)> entries)
            : Microsoft.Extensions.Logging.ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

            public void Log<TState>(
                Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter)
                => entries.Add((eventId.Id, logLevel, formatter(state, exception)));
        }
    }

    [Fact]
    public async Task ApprovingAClaimThatIsLargerThanWhatIsLeftOnTheBillIsRefusedAndLeavesAnAuditEventNamingTheClaim()
    {
        var terminalId = Guid.NewGuid();
        var cashier = await _database.SeedCashierSessionAsync(terminalId, "rmd794-claim-cashier");
        var billId = await _database.SeedBillAsync(payable: 100m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var paymentId = await CreateUnsettledPaymentAsync(client, terminalId, billId, cashier, "rmd794-claim-1");
        var manager = await _database.SeedManagerSessionAsync(terminalId, "rmd794-claim-manager");
        var claim = await PostAsync(client, ClaimPath(terminalId, billId, paymentId), manager, new { SlipNumber = "FIS-7941" });
        var confirmationId = (await claim.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("confirmationId").GetGuid();
        await using (var shrink = _database.DataSource.CreateCommand("INSERT INTO payments.payment_allocations (payment_allocation_id, payment_id, bill_id, amount, currency_code, idempotency_key, allocated_at) SELECT gen_random_uuid(), payment_id, bill_id, 70, currency_code, 'rmd794-other-tender', now() FROM payments.payments WHERE payment_id = @id;"))
        {
            shrink.Parameters.AddWithValue("id", paymentId);
            await shrink.ExecuteNonQueryAsync();
        }
        var approver = await _database.SeedManagerSessionAsync(terminalId, "rmd794-claim-approver");

        var response = await PostAsync(client, DecidePath(terminalId, billId, confirmationId, "approve"), approver, new { });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var rejection = await ReadRejectionAsync(billId);
        Assert.Equal(1, rejection.Count);
        Assert.NotNull(rejection.ActorId);
        Assert.Equal(JsonValueKind.Null, rejection.State.GetProperty("idempotencyKey").ValueKind);
        Assert.Equal(terminalId, rejection.State.GetProperty("terminalId").GetGuid());
        Assert.Equal("BankCard", rejection.State.GetProperty("method").GetString());
        Assert.Equal(confirmationId, rejection.State.GetProperty("confirmationId").GetGuid());
        Assert.Equal(40m, rejection.State.GetProperty("amount").GetDecimal());
        Assert.Equal(30m, rejection.State.GetProperty("remainingPayable").GetDecimal());
        Assert.Equal("Unknown", await _database.PaymentStatusAsync(paymentId));
    }

    private async Task<(int Count, Guid? ActorId, JsonElement State)> ReadRejectionAsync(Guid billId)
    {
        await using var command = _database.DataSource.CreateCommand(
            """
            SELECT actor_id, after_state_json FROM audit.audit_events
            WHERE event_name = 'payment.tender-rejected' AND aggregate_type = 'Bill' AND aggregate_id = @bill
              AND actor_id IN (SELECT user_id FROM identity.users);
            """);
        command.Parameters.AddWithValue("bill", billId);
        await using var reader = await command.ExecuteReaderAsync();
        var count = 0;
        Guid? actor = null;
        var state = default(JsonElement);
        while (await reader.ReadAsync())
        {
            count++;
            actor = reader.IsDBNull(0) ? null : reader.GetGuid(0);
            state = JsonDocument.Parse(reader.GetString(1)).RootElement.Clone();
        }

        return (count, actor, state);
    }
}
