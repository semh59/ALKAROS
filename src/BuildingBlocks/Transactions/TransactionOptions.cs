namespace ALKAROS.Transactions;

/// <summary>
/// Controls how <see cref="TransactionContext.RunAsync"/> behaves when an
/// ambient transaction is already active.
/// </summary>
public enum TransactionJoinBehavior
{
    /// <summary>
    /// Join the ambient transaction when one is active; otherwise start a
    /// new root transaction. This is the default propagation behavior.
    /// </summary>
    Join,

    /// <summary>
    /// Always start a new root transaction. Rejected with
    /// <see cref="NestedTransactionException"/> when an ambient transaction
    /// is active, because an independent nested transaction would break the
    /// single-commit boundary.
    /// </summary>
    CreateNew,
}

/// <summary>
/// Options for a single <see cref="TransactionContext.RunAsync"/> invocation.
/// </summary>
public sealed record TransactionOptions
{
    /// <summary>
    /// Default options: ambient propagation with join behavior.
    /// </summary>
    public static readonly TransactionOptions Default = new();

    /// <summary>
    /// The join behavior applied when an ambient transaction is active.
    /// </summary>
    public TransactionJoinBehavior JoinBehavior { get; init; } = TransactionJoinBehavior.Join;
}
