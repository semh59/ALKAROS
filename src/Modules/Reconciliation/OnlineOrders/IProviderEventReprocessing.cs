using Npgsql;

namespace ALKAROS.Reconciliation.OnlineOrders;

/// <summary>
/// The one state change a reconciliation retry makes to a stored provider event: putting it back to
/// waiting so it is processed again. The event belongs to OnlineOrdering, so this module only names the
/// operation; the Host binds it to the OnlineOrdering inbox contract. It runs inside the caller's
/// transaction so the retry trail is written together with its effect, and returns the number of events
/// reopened (0 when the event is no longer refused or failed, or its cancellation was already requested).
/// </summary>
public interface IProviderEventReprocessing
{
    Task<int> ReopenForReprocessingAsync(
        Guid inboxId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default);
}
