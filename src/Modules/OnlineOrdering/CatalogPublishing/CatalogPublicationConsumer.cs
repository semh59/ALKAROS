using ALKAROS.IntegrationContracts;

namespace ALKAROS.OnlineOrdering.CatalogPublishing;

/// <summary>Delivers committed catalog publications; the outbox retries a failed delivery and dead-letters it after its threshold.</summary>
public sealed class CatalogPublicationConsumer : IIntegrationEventConsumer
{
    private readonly CatalogPublicationService _publications;

    public CatalogPublicationConsumer(CatalogPublicationService publications)
    {
        _publications = publications ?? throw new ArgumentNullException(nameof(publications));
    }

    public bool CanHandle(string eventType) =>
        string.Equals(eventType, CatalogPublicationService.RequestedEventType, StringComparison.Ordinal);

    public Task HandleAsync(string eventType, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) =>
        _publications.DeliverAsync(
            IntegrationEventSerializer.Deserialize<CatalogPublicationRequested>(payload.Span).PublicationId, cancellationToken);
}
