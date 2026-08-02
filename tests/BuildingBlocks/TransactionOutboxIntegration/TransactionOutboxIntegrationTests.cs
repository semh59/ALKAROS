using ALKAROS.Messaging;
using ALKAROS.TestHelpers;
using ALKAROS.TransactionOutboxIntegration.Tests.Fixtures;
using ALKAROS.Transactions;
using Npgsql;
using Xunit;

namespace ALKAROS.TransactionOutboxIntegration.Tests;

/// <summary>
/// Verifies the transaction/outbox integration contract (V1-FND-006):
/// transactional enqueue, commit-before-dispatch, rollback suppression,
/// post-commit wake-up, restart recovery, duplicate-dispatch idempotency
/// and typed failure propagation across every commit/crash window.
/// </summary>
public sealed class TransactionOutboxIntegrationTests : IClassFixture<TransactionOutboxTestDatabase>
{
    private readonly TransactionOutboxTestDatabase _database;

    public TransactionOutboxIntegrationTests(TransactionOutboxTestDatabase database)
    {
        _database = database;
    }

    private async Task ResetAsync()
        => await _database.ResetTablesAsync();

    private static OutboxEnvelope Envelope(string eventType, byte[] payload)
        => new(eventType, "order", Guid.NewGuid(), payload);

    private static async Task WriteDomainAsync(ITransactionContext context, Guid id)
    {
        await using var command = context.Connection.CreateCommand();
        command.Transaction = context.Transaction;
        command.CommandText = "INSERT INTO fnd011_domain_writes (id, value) VALUES (@id, 'written');";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "id";
        parameter.Value = id;
        command.Parameters.Add(parameter);

        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task DomainWriteAndOutboxEnqueueCommitInTheSameTransaction()
    {
        await ResetAsync();
        var resource = new TransactionOutboxResource(_database.DataSource);
        var domainId = Guid.NewGuid();

        await TransactionOutbox.RunAsync(
            async context =>
            {
                await WriteDomainAsync(context, domainId);
                resource.Enqueue(Envelope("order.created", new byte[] { 1, 2, 3 }));
            },
            resource);

        Assert.Equal(1, await _database.CountAsync("fnd011_domain_writes"));
        Assert.Equal(1, await _database.CountAsync("outbox_messages"));
    }

    [Fact]
    public async Task CommitPersistsEnqueuedEnvelopeWithoutDispatch()
    {
        await ResetAsync();
        var resource = new TransactionOutboxResource(_database.DataSource);
        var envelope = Envelope("order.created", new byte[] { 1, 2, 3 });

        await TransactionOutbox.RunAsync(
            _ =>
            {
                resource.Enqueue(envelope);
                return Task.CompletedTask;
            },
            resource);

        Assert.Equal(1, await _database.CountAsync("outbox_messages"));
        Assert.Equal("order.created", await _database.ScalarAsync<string>(
            "SELECT event_type FROM outbox_messages;"));
        Assert.Equal("pending", await _database.ScalarAsync<string>(
            "SELECT status FROM outbox_messages;"));
    }

    [Fact]
    public async Task EnqueuedEnvelopeIsInvisibleBeforeCommit()
    {
        await ResetAsync();
        var resource = new TransactionOutboxResource(_database.DataSource);
        var outboxStore = new OutboxStore(_database.DataSource);
        var probeSink = new RecordingSink();
        var envelope = Envelope("order.created", new byte[] { 1, 2, 3 });

        await TransactionOutbox.RunAsync(
            async _ =>
            {
                resource.Enqueue(envelope);
                Assert.Equal(0, await _database.CountAsync("outbox_messages"));
                Assert.Equal(0, await outboxStore.DispatchAsync(probeSink, 10));
            },
            resource);

        Assert.Equal(1, await _database.CountAsync("outbox_messages"));
        Assert.Empty(probeSink.Delivered);
    }

    [Fact]
    public async Task PostCommitDispatchDeliversCommittedRecords()
    {
        await ResetAsync();
        var resource = new TransactionOutboxResource(_database.DataSource);
        var outboxStore = new OutboxStore(_database.DataSource);
        var sink = new RecordingSink();
        var envelope = Envelope("order.created", new byte[] { 1, 2, 3 });

        await TransactionOutbox.RunAsync(
            _ =>
            {
                resource.Enqueue(envelope);
                return Task.CompletedTask;
            },
            resource);

        var attempted = await outboxStore.DispatchAsync(sink, 10);

        Assert.Equal(1, attempted);
        Assert.Single(sink.Delivered);
        Assert.Equal(envelope.EventType, sink.Delivered[0].EventType);
        Assert.Equal("dispatched", await _database.ScalarAsync<string>(
            "SELECT status FROM outbox_messages;"));
    }

    [Fact]
    public async Task WorkflowFailureRollsBackWithoutOutboxRecordOrDispatch()
    {
        await ResetAsync();
        var resource = new TransactionOutboxResource(_database.DataSource);
        var envelope = Envelope("order.created", new byte[] { 1, 2, 3 });

        await Assert.ThrowsAsync<SimulatedFailureException>(() =>
            TransactionOutbox.RunAsync(
                async _ =>
                {
                    resource.Enqueue(envelope);
                    throw new SimulatedFailureException("workflow failed");
                },
                resource));

        Assert.Equal(0, await _database.CountAsync("outbox_messages"));
    }

    [Fact]
    public async Task CommitFailureLeavesNoOutboxRecordAndNoDispatch()
    {
        await ResetAsync();
        var resource = new TransactionOutboxResource(_database.DataSource);
        var sink = new RecordingSink();
        var failingResource = new RecordingResource("domain", commitFailureAt: 1);
        var envelope = Envelope("order.created", new byte[] { 1, 2, 3 });

        await Assert.ThrowsAsync<SimulatedFailureException>(() =>
            TransactionOutbox.RunAsync(
                context =>
                {
                    resource.Enqueue(envelope);
                    context.Enlist(failingResource);
                    return Task.CompletedTask;
                },
                resource));

        Assert.True(failingResource.RolledBack);
        Assert.Equal(0, await _database.CountAsync("outbox_messages"));
        Assert.Empty(sink.Delivered);
    }

    [Fact]
    public async Task RetryAttemptResetPreventsDuplicateOutboxRows()
    {
        await ResetAsync();
        var resource = new TransactionOutboxResource(_database.DataSource);
        var attempt = 0;
        var envelope = Envelope("order.created", new byte[] { 1, 2, 3 });
        var retryPolicy = new TransactionRetryPolicy(
            maxAttempts: 2,
            delayForAttempt: _ => TimeSpan.Zero,
            classifier: new FixedClassifier(RetryClassification.Transient));

        await TransactionOutbox.RunAsync(
            _ =>
            {
                resource.Enqueue(envelope);
                if (attempt++ == 0)
                    throw new SimulatedTransientException("transient workflow failure");
                return Task.CompletedTask;
            },
            resource,
            retryPolicy: retryPolicy);

        Assert.Equal(1, await _database.CountAsync("outbox_messages"));
    }

    [Fact]
    public async Task OutboxCommitFailureRollsBackAllRowsAndPropagatesTypedError()
    {
        await ResetAsync();
        await _database.ExecuteAsync(
            "ALTER TABLE outbox_messages ADD CONSTRAINT fnd006_payload_limit "
            + "CHECK (length(payload_envelope) <= 4);");
        try
        {
            var resource = new TransactionOutboxResource(_database.DataSource);
            var domainId = Guid.NewGuid();

            await Assert.ThrowsAsync<PostgresException>(() =>
                TransactionOutbox.RunAsync(
                    async context =>
                    {
                        await WriteDomainAsync(context, domainId);
                        resource.Enqueue(Envelope("order.created", new byte[] { 1 }));
                        resource.Enqueue(Envelope("order.approved", new byte[] { 1, 2, 3, 4, 5 }));
                    },
                    resource));

            Assert.Equal(0, await _database.CountAsync("fnd011_domain_writes"));
            Assert.Equal(0, await _database.CountAsync("outbox_messages"));
        }
        finally
        {
            await _database.ExecuteAsync(
                "ALTER TABLE outbox_messages DROP CONSTRAINT fnd006_payload_limit;");
        }
    }

    [Fact]
    public async Task RetryUsesANewTransactionAndCommitsOnlyTheSuccessfulAttempt()
    {
        await ResetAsync();
        var resource = new TransactionOutboxResource(_database.DataSource);
        var domainId = Guid.NewGuid();
        var attempts = 0;
        object? firstTransaction = null;
        var retryPolicy = new TransactionRetryPolicy(
            maxAttempts: 2,
            delayForAttempt: _ => TimeSpan.Zero,
            classifier: new FixedClassifier(RetryClassification.Transient));

        await TransactionOutbox.RunAsync(
            async context =>
            {
                if (attempts++ == 0)
                {
                    firstTransaction = context.Transaction;
                    await WriteDomainAsync(context, domainId);
                    resource.Enqueue(Envelope("order.created", new byte[] { 1 }));
                    throw new SimulatedTransientException("retry transaction");
                }

                Assert.NotNull(firstTransaction);
                Assert.NotSame(firstTransaction, context.Transaction);
                await WriteDomainAsync(context, domainId);
                resource.Enqueue(Envelope("order.created", new byte[] { 1 }));
            },
            resource,
            retryPolicy: retryPolicy);

        Assert.Equal(1, await _database.CountAsync("fnd011_domain_writes"));
        Assert.Equal(1, await _database.CountAsync("outbox_messages"));
    }

    [Fact]
    public async Task RestartRecoveryDispatchesCommittedRecords()
    {
        await ResetAsync();
        var resource = new TransactionOutboxResource(_database.DataSource);
        var envelope = Envelope("order.created", new byte[] { 1, 2, 3 });

        await TransactionOutbox.RunAsync(
            _ =>
            {
                resource.Enqueue(envelope);
                return Task.CompletedTask;
            },
            resource);

        Assert.Equal(1, await _database.CountAsync("outbox_messages"));

        var restartedStore = new OutboxStore(_database.DataSource);
        var sink = new RecordingSink();
        var attempted = await restartedStore.DispatchAsync(sink, 10);

        Assert.Equal(1, attempted);
        Assert.Single(sink.Delivered);
        Assert.Equal("dispatched", await _database.ScalarAsync<string>(
            "SELECT status FROM outbox_messages;"));
    }

    [Fact]
    public async Task DuplicateDeliveryDoesNotProduceSecondBusinessEffect()
    {
        await ResetAsync();
        var resource = new TransactionOutboxResource(_database.DataSource);
        var outboxStore = new OutboxStore(_database.DataSource);
        var sink = new IdempotentSink();
        var envelope = Envelope("order.created", new byte[] { 1, 2, 3 });

        await TransactionOutbox.RunAsync(
            _ =>
            {
                resource.Enqueue(envelope);
                return Task.CompletedTask;
            },
            resource);

        await outboxStore.DispatchAsync(sink, 10);
        Assert.Equal(1, sink.BusinessEffectCount);

        await _database.ExecuteAsync(
            "UPDATE outbox_messages SET status = 'pending', next_retry_at = NULL;");

        var reattempted = await outboxStore.DispatchAsync(sink, 10);

        Assert.Equal(1, reattempted);
        Assert.Equal(1, sink.BusinessEffectCount);
    }
}
