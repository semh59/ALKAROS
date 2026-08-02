using System.Globalization;
using System.Text;
using ALKAROS.Messaging;
using ALKAROS.Transactions;
using Npgsql;

namespace ALKAROS.TransactionOutboxIntegration;

/// <summary>
/// An <see cref="ITransactionResource"/> that persists <see cref="Enqueue"/>
/// calls into the outbox as part of the ambient transaction commit. Envelopes
/// are buffered during the workflow and written in a single PostgreSQL
/// transaction at commit time, so a failed commit leaves no partial outbox
/// rows. The resource must be enlisted last (the
/// <see cref="TransactionOutbox.RunAsync"/> wrapper does this automatically)
/// so that domain state commits before the outbox writes and no resource
/// runs after them. Rolling back only clears the buffer: nothing was
/// persisted outside the commit transaction (V0-ARC-003 §3).
/// </summary>
public sealed class TransactionOutboxResource : ITransactionResource
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly List<OutboxEnvelope> _envelopes = new();

    public TransactionOutboxResource(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    /// <summary>
    /// The number of envelopes buffered for the current attempt.
    /// </summary>
    public int PendingCount => _envelopes.Count;

    /// <summary>
    /// Buffers <paramref name="envelope"/> for the commit of the ambient
    /// transaction. No database row is written until the transaction
    /// commits, so an aborted transaction never produces an outbox record.
    /// </summary>
    public void Enqueue(OutboxEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        _envelopes.Add(envelope);
    }

    /// <summary>
    /// Clears envelopes buffered by a previous attempt. Called at the start
    /// of every attempt so a retried workflow cannot duplicate outbox rows.
    /// </summary>
    public void ResetForAttempt()
    {
        _envelopes.Clear();
    }

    /// <summary>
    /// Writes every buffered envelope in one PostgreSQL transaction. The
    /// write is all-or-nothing: any failure rolls the transaction back and
    /// surfaces the database error, and the buffered envelopes remain
    /// pending for the next attempt.
    /// </summary>
    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_envelopes.Count == 0)
            return;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;

        var sb = new StringBuilder();
        for (var i = 0; i < _envelopes.Count; i++)
        {
            var offset = i * 4;
            sb.Append(
                """
                INSERT INTO outbox_messages (event_type, aggregate_type, aggregate_id, payload_envelope)
                VALUES (
                """);
            sb.Append(CultureInfo.InvariantCulture, $"${offset + 1}, ${offset + 2}, ${offset + 3}, ${offset + 4}");
            sb.AppendLine(");");

            var envelope = _envelopes[i];
            command.Parameters.AddWithValue(envelope.EventType);
            command.Parameters.AddWithValue(envelope.AggregateType);
            command.Parameters.AddWithValue(envelope.AggregateId);
            command.Parameters.AddWithValue(envelope.PayloadEnvelope);
        }

        command.CommandText = sb.ToString();
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Clears the buffer. Called only when the commit transaction failed or
    /// never ran, so no persisted row needs to be undone.
    /// </summary>
    public Task RollbackAsync(CancellationToken cancellationToken)
    {
        _envelopes.Clear();
        return Task.CompletedTask;
    }
}
