using System.Data.Common;

namespace ALKAROS.Transactions;

/// <summary>
/// The ambient transaction scope. Instances are created by
/// <see cref="TransactionContext"/> and flow through asynchronous calls via
/// <see cref="AsyncLocal{T}"/>. A scope is not thread-safe: it must only be
/// used from the single async flow that runs the workflow.
/// </summary>
internal sealed class TransactionScope : ITransactionContext, IAsyncDisposable
{
    private static readonly AsyncLocal<TransactionScope?> AmbientStorage = new();

    private readonly List<ITransactionResource> _resources = new();
    private readonly DbDataSource? _dataSource;
    private DbConnection? _connection;
    private DbTransaction? _transaction;

    public TransactionScope(DbDataSource? dataSource)
    {
        _dataSource = dataSource;
    }

    public Guid Id { get; } = Guid.NewGuid();

    public DbConnection Connection
        => _connection ?? throw new InvalidOperationException(
            "This transaction does not have a database connection.");

    public DbTransaction Transaction
        => _transaction ?? throw new InvalidOperationException(
            "This transaction does not have a database transaction.");

    /// <summary>
    /// The ambient scope for the current async flow.
    /// </summary>
    public static TransactionScope? Current
    {
        get => AmbientStorage.Value;
        set => AmbientStorage.Value = value;
    }

    public void Enlist(ITransactionResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        _resources.Add(resource);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_dataSource is null)
            return;

        _connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _transaction = await _connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
            throw;
        }
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        foreach (var resource in _resources)
        {
            if (_transaction is null)
                await resource.CommitAsync(cancellationToken).ConfigureAwait(false);
            else
                await resource.CommitAsync(Connection, _transaction, cancellationToken).ConfigureAwait(false);
        }

        if (_transaction is not null)
            await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (_transaction is not null)
            await _transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);

        for (var i = _resources.Count - 1; i >= 0; i--)
            await _resources[i].RollbackAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync().ConfigureAwait(false);
            _transaction = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }
    }
}
