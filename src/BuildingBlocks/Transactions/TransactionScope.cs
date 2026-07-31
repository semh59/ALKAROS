namespace ALKAROS.Transactions;

/// <summary>
/// The ambient transaction scope. Instances are created by
/// <see cref="TransactionContext"/> and flow through asynchronous calls via
/// <see cref="AsyncLocal{T}"/>. A scope is not thread-safe: it must only be
/// used from the single async flow that runs the workflow.
/// </summary>
internal sealed class TransactionScope : ITransactionContext
{
    private static readonly AsyncLocal<TransactionScope?> AmbientStorage = new();

    private readonly List<ITransactionResource> _resources = new();

    public Guid Id { get; } = Guid.NewGuid();

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

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        foreach (var resource in _resources)
            await resource.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        for (var i = _resources.Count - 1; i >= 0; i--)
            await _resources[i].RollbackAsync(cancellationToken).ConfigureAwait(false);
    }
}
