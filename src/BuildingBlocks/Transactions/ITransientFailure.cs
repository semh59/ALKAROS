namespace ALKAROS.Transactions;

/// <summary>
/// Marker interface for failures that are safe to retry. Exceptions that
/// implement this interface declare that the operation failed before
/// reaching a point that cannot be re-executed, so the workflow may be
/// attempted again.
/// </summary>
public interface ITransientFailure
{
}
