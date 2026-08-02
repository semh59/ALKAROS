using System.Data.Common;

namespace ALKAROS.Transactions;

/// <summary>
/// The transaction execution boundary: runs a workflow inside an ambient
/// transaction so that every enlisted <see cref="ITransactionResource"/>
/// commits together or rolls back completely. Independent nested
/// transactions are rejected; nested calls join the ambient transaction.
/// Failures are retried only when a retry policy classifies them as
/// transient; unknown failures always surface immediately.
/// </summary>
public static class TransactionContext
{
    /// <summary>
    /// Executes <paramref name="workflow"/> inside a transaction boundary.
    /// When an ambient transaction is already active, the default
    /// <see cref="TransactionJoinBehavior.Join"/> behavior propagates into
    /// it; <see cref="TransactionJoinBehavior.CreateNew"/> is rejected with
    /// <see cref="NestedTransactionException"/> in that case.
    /// </summary>
    public static Task RunAsync(
        Func<ITransactionContext, Task> workflow,
        TransactionOptions? options = null,
        TransactionRetryPolicy? retryPolicy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        if (!Enum.IsDefined(options?.JoinBehavior ?? TransactionJoinBehavior.Join))
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Invalid TransactionJoinBehavior value.");

        var current = TransactionScope.Current;
        if (current is not null)
        {
            if ((options?.JoinBehavior ?? TransactionJoinBehavior.Join) == TransactionJoinBehavior.CreateNew)
                throw new NestedTransactionException();

            return workflow(current);
        }

        return RunRootAsync(workflow, dataSource: null, retryPolicy, cancellationToken);
    }

    /// <summary>
    /// Executes <paramref name="workflow"/> with one connection and database
    /// transaction owned by the transaction scope. The connection and
    /// transaction are exposed through <see cref="ITransactionContext"/> for
    /// persistent writes and enlisted database resources.
    /// </summary>
    public static Task RunAsync(
        DbDataSource dataSource,
        Func<ITransactionContext, Task> workflow,
        TransactionOptions? options = null,
        TransactionRetryPolicy? retryPolicy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(workflow);

        if (!Enum.IsDefined(options?.JoinBehavior ?? TransactionJoinBehavior.Join))
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Invalid TransactionJoinBehavior value.");

        var current = TransactionScope.Current;
        if (current is not null)
        {
            if ((options?.JoinBehavior ?? TransactionJoinBehavior.Join) == TransactionJoinBehavior.CreateNew)
                throw new NestedTransactionException();

            return workflow(current);
        }

        return RunRootAsync(workflow, dataSource, retryPolicy, cancellationToken);
    }

    private static async Task RunRootAsync(
        Func<ITransactionContext, Task> workflow,
        DbDataSource? dataSource,
        TransactionRetryPolicy? retryPolicy,
        CancellationToken cancellationToken)
    {
        var attempts = retryPolicy?.MaxAttempts ?? 1;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                await RunSingleAttemptAsync(workflow, dataSource, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception exception) when (retryPolicy is not null
                && retryPolicy.MayRetry(attempt + 1, exception))
            {
                var delay = retryPolicy.DelayForAttempt(attempt + 1);
                if (delay < TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException(
                        nameof(retryPolicy),
                        "DelayForAttempt must return a non-negative TimeSpan.");

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task RunSingleAttemptAsync(
        Func<ITransactionContext, Task> workflow,
        DbDataSource? dataSource,
        CancellationToken cancellationToken)
    {
        // Suspend before touching the AsyncLocal ambient: writes executed
        // before the first await would otherwise land in the caller's
        // ExecutionContext and leak into unrelated concurrent flows.
        await Task.Yield();

        var scope = new TransactionScope(dataSource);
        var previous = TransactionScope.Current;
        TransactionScope.Current = scope;

        try
        {
            await scope.InitializeAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await workflow(scope).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                await RollbackOrThrowAsync(scope, exception).ConfigureAwait(false);
                throw;
            }

            try
            {
                await scope.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                await RollbackOrThrowAsync(scope, exception).ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            TransactionScope.Current = previous;
            await scope.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Rolls back every enlisted resource with an un-cancellable token so
    /// rollback always runs. When the rollback itself fails, the original
    /// failure and the rollback failure are combined into a
    /// <see cref="TransactionExecutionException"/>; otherwise the original
    /// exception is rethrown by the caller so retry classification is
    /// preserved.
    /// </summary>
    private static async Task RollbackOrThrowAsync(
        TransactionScope scope,
        Exception originalException)
    {
        try
        {
            await scope.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception rollbackException)
        {
            throw new TransactionExecutionException(
                "Transaction rollback failed after an execution failure; "
                + "the transaction state is unknown.",
                new AggregateException(originalException, rollbackException));
        }
    }
}
