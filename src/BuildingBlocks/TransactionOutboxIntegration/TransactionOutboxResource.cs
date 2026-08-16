using System.Globalization;
using System.Data.Common;
using System.Text;
using ALKAROS.Messaging;
using ALKAROS.Transactions;

namespace ALKAROS.TransactionOutboxIntegration;

/// <summary>
/// An <see cref="ITransactionResource"/> that persists <see cref="Enqueue"/>
/// calls into the outbox as part of the ambient transaction commit. Envelopes
/// are buffered during the workflow and written through the connection and
/// transaction owned by the transaction scope. The resource must be enlisted last (the
/// <see cref="TransactionOutbox.RunAsync"/> wrapper does this automatically)
/// so that all domain and Outbox database writes commit together. Rolling
/// back clears the buffer and the shared transaction (V0-ARC-003 §3).
/// </summary>
public sealed class TransactionOutboxResource : ITransactionResource
{
    private readonly DbDataSource _dataSource;
    private readonly List<OutboxEnvelope> _envelopes = new();
    private readonly Action? _onCommitted;

    public TransactionOutboxResource(DbDataSource dataSource, Action? onCommitted = null)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _onCommitted = onCommitted;
    }

    internal DbDataSource DataSource => _dataSource;

    /// <summary>
    /// Invoked immediately after the ambient transaction commits to notify
    /// outbox listeners for crash-safe immediate dispatch wake-up.
    /// </summary>
    public void NotifyCommitted()
    {
        if (_envelopes.Count > 0)
        {
            _onCommitted?.Invoke();
        }
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
    /// This method must be called through a transaction scope with a shared
    /// database connection and transaction.
    /// </summary>
    public Task CommitAsync(CancellationToken cancellationToken)
        => throw new InvalidOperationException(
            "TransactionOutboxResource requires the transaction scope database session.");

    /// <summary>
    /// Writes every buffered envelope using the connection and transaction
    /// owned by the transaction scope. This resource never opens or commits
    /// an independent database transaction.
    /// </summary>
    public async Task CommitAsync(
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        if (_envelopes.Count == 0)
            return;

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
            AddParameter(command, envelope.EventType);
            AddParameter(command, envelope.AggregateType);
            AddParameter(command, envelope.AggregateId);
            AddParameter(command, envelope.PayloadEnvelope);
        }

        command.CommandText = sb.ToString();
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void AddParameter(DbCommand command, object value)
    {
        var parameter = command.CreateParameter();
        parameter.Value = value;
        command.Parameters.Add(parameter);
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
