using ALKAROS.OnlineOrdering.Yemeksepeti.OrderNormalization;
using ALKAROS.Reconciliation.OnlineOrders;
using Npgsql;

namespace ALKAROS.Host.Experience.Reconciliation;

/// <summary>
/// V12-REC-001: the Host binds the reconciliation retry to the OnlineOrdering inbox contract, so the
/// Reconciliation module never writes another module's rows itself.
/// </summary>
public sealed class OnlineOrderingInboxReprocessing : IProviderEventReprocessing
{
    public Task<int> ReopenForReprocessingAsync(
        Guid inboxId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default) =>
        YemeksepetiInboxProcessingStore.ReopenForReprocessingAsync(inboxId, connection, transaction, cancellationToken);
}
