namespace ALKAROS.OnlineOrdering.AvailabilityPublishing;

/// <summary>A product a channel already knows, by the channel's external identifier.</summary>
public sealed record PublishedChannelProduct(Guid ProductId, string ExternalId);

/// <summary>One availability line sent to a channel: how many units can be sold now (0 = unavailable).</summary>
public sealed record ChannelAvailability(string ExternalId, int Quantity);

/// <summary>An online channel that can be told a product's availability.</summary>
public interface IAvailabilityChannelPublisher
{
    string Channel { get; }

    /// <summary>False while the channel has no credentials configured; nothing is computed or sent for it then.</summary>
    bool IsEnabled { get; }

    /// <summary>The largest number of products one provider call may carry.</summary>
    int MaxBatchSize { get; }

    /// <summary>The products the channel knows (up to <paramref name="limit"/>), by their external identifiers.</summary>
    Task<IReadOnlyList<PublishedChannelProduct>> PublishedProductsAsync(int limit, CancellationToken cancellationToken = default);

    Task PublishAsync(IReadOnlyList<ChannelAvailability> availability, CancellationToken cancellationToken = default);
}

/// <summary>A product whose channel has not been told its current availability for longer than the tolerance, or whose deliveries keep failing.</summary>
public sealed record AvailabilityDivergence(
    string Channel,
    Guid ProductId,
    string ExternalId,
    int DesiredQuantity,
    int? DeliveredQuantity,
    DateTimeOffset DesiredAt,
    int DeliveryAttempts,
    string? LastError);

public sealed record AvailabilityPassResult(int StatesChanged, int ProductsDelivered);
