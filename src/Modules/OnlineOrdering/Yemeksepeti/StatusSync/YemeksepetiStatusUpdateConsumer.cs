using ALKAROS.IntegrationContracts;

namespace ALKAROS.OnlineOrdering.Yemeksepeti.StatusSync;

/// <summary>
/// Delivers a committed local status change to the provider. The outbox retries a failed delivery
/// with backoff and dead-letters it after its threshold, so a provider outage never loses the
/// update and never blocks the local change that caused it.
/// </summary>
public sealed class YemeksepetiStatusUpdateConsumer : IIntegrationEventConsumer
{
    private readonly IYemeksepetiPartnerClient _client;

    public YemeksepetiStatusUpdateConsumer(IYemeksepetiPartnerClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public bool CanHandle(string eventType) =>
        string.Equals(eventType, YemeksepetiStatusSync.StatusUpdateRequestedEventType, StringComparison.Ordinal);

    public Task HandleAsync(string eventType, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) =>
        _client.UpdateOrderStatusAsync(
            IntegrationEventSerializer.Deserialize<YemeksepetiStatusUpdateRequested>(payload.Span), cancellationToken);
}
