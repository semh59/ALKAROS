using Npgsql;

namespace ALKAROS.OnlineOrdering.Providers.Contracts;

/// <summary>The statuses a restaurant reports to a platform.</summary>
public enum OnlineOutboundStatus
{
    /// <summary>The platform's courier collects the order.</summary>
    ReadyForPickup,

    /// <summary>The restaurant's own courier left with the order.</summary>
    Dispatched,

    Cancelled
}

/// <summary>Why the restaurant cancels an online order.</summary>
public enum OnlineCancellationReason
{
    Closed,
    ItemUnavailable,
    TooBusy
}

/// <summary>An item as the platform identifies it on a status update.</summary>
public sealed record OnlineOrderLineReference(string Sku, decimal Quantity);

/// <summary>A local status change the platform must hear about; queued in the caller's transaction.</summary>
public sealed record OnlineOrderStatusRequest(
    string ExternalOrderId,
    OnlineOutboundStatus Status,
    OnlineCancellationReason? Reason,
    IReadOnlyList<OnlineOrderLineReference> Items,
    DateTimeOffset RequestedAt);

/// <summary>
/// V12-ONL-007: one online ordering platform. The shared intake, handover and cancellation flow (stock holds,
/// kitchen, order lifecycle, reconciliation) is written once against this contract; each platform implements
/// its own payload reading, status vocabulary and outbound calls.
/// </summary>
public interface IOnlineOrderProvider
{
    /// <summary>The platform identity stored with every order (<c>online_ordering.online_orders.provider</c>).</summary>
    string Provider { get; }

    /// <summary>The platform's name as staff read it, in order history and reasons.</summary>
    string DisplayName { get; }

    /// <summary>The prefix of the local order number of an order from this platform.</summary>
    string OrderNumberPrefix { get; }

    /// <summary>What one stored platform event asks of the local order.</summary>
    StatusMappingResult MapStatus(string externalOrderId, string providerStatus, string rawPayload);

    /// <summary>The platform order in internal terms, or the first reason it cannot become one.</summary>
    Task<NormalizationResult> NormalizeAsync(string rawPayload, DateTimeOffset receivedAt, CancellationToken cancellationToken = default);

    /// <summary>The items a cancellation of an order that never became local must name, read leniently.</summary>
    IReadOnlyList<OnlineOrderLineReference> ReadItemReferences(string rawPayload);

    /// <summary>The status a handover of <paramref name="orderId"/> is reported as; null when the platform documents none for it.</summary>
    Task<OnlineOutboundStatus?> HandoverStatusAsync(
        Guid orderId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>Queues <paramref name="request"/> for delivery to the platform in the caller's transaction.</summary>
    Task RequestStatusAsync(
        OnlineOrderStatusRequest request, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default);
}

/// <summary>The registered platforms by identity.</summary>
public sealed class OnlineOrderProviderRegistry
{
    private readonly Dictionary<string, IOnlineOrderProvider> _providers = new(StringComparer.Ordinal);

    public OnlineOrderProviderRegistry(IEnumerable<IOnlineOrderProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        foreach (var provider in providers)
        {
            ArgumentNullException.ThrowIfNull(provider, nameof(providers));
            if (!_providers.TryAdd(provider.Provider, provider))
                throw new ArgumentException($"Online ordering platform '{provider.Provider}' is registered twice.", nameof(providers));
        }
    }

    /// <summary>The platform with this identity; an unknown identity is a fault, never a silent default.</summary>
    public IOnlineOrderProvider Get(string provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        return _providers.TryGetValue(provider, out var found)
            ? found
            : throw new UnknownOnlineOrderProviderException(provider);
    }
}

public sealed class UnknownOnlineOrderProviderException : Exception
{
    public UnknownOnlineOrderProviderException(string provider)
        : base($"No online ordering platform '{provider}' is registered.") { }
}
