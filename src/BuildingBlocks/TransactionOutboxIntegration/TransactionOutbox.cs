using ALKAROS.Messaging;
using ALKAROS.Transactions;

namespace ALKAROS.TransactionOutboxIntegration;

/// <summary>
/// Runs a workflow and its buffered outbox envelopes in one transaction
/// boundary (V1-FND-006). The envelope buffer is cleared at the start of
/// every attempt and the outbox resource is enlisted last, so domain state
/// commits before the outbox rows and no resource runs after them.
/// Dispatch must happen strictly after <see cref="RunAsync"/> returns: the
/// committed outbox rows are invisible before the commit and are delivered
/// by the caller through <see cref="OutboxStore.DispatchAsync"/> (commit
/// before dispatch). Rows committed by a process that crashes before
/// dispatch stay <see cref="OutboxStatus.Pending"/> and are picked up after
/// a restart; duplicate delivery is safe because the domain consumer is
/// idempotent (at-least-once, V0-ARC-003 §3).
/// </summary>
public static class TransactionOutbox
{
    /// <summary>
    /// Executes <paramref name="workflow"/> inside an ambient transaction,
    /// enlisting <paramref name="resource"/> after the workflow so its
    /// envelopes commit together with the domain state. Returns only after
    /// the commit succeeded, which guarantees every buffered envelope is
    /// durably pending in the outbox.
    /// </summary>
    public static async Task RunAsync(
        Func<ITransactionContext, Task> workflow,
        TransactionOutboxResource resource,
        TransactionOptions? options = null,
        TransactionRetryPolicy? retryPolicy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(resource);

        await TransactionContext.RunAsync(
            resource.DataSource,
            async context =>
            {
                resource.ResetForAttempt();
                await workflow(context).ConfigureAwait(false);
                context.Enlist(resource);
            },
            options,
            retryPolicy,
            cancellationToken).ConfigureAwait(false);

        resource.NotifyCommitted();
    }
}
