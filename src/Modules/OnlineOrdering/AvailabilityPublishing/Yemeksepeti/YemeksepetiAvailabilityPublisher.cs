using ALKAROS.OnlineOrdering.Yemeksepeti.ProductMapping;
using ALKAROS.OnlineOrdering.Yemeksepeti.StatusSync;
using ALKAROS.Secrets;

namespace ALKAROS.OnlineOrdering.AvailabilityPublishing.Yemeksepeti;

/// <summary>
/// V12-ONL-005: the Yemeksepeti availability channel. It knows the products that have an open
/// V12-MAP-001 SKU mapping and tells the provider their sellable <c>quantity</c> through the vendor
/// catalog update (UNVERIFIED DRAFT — the public Partner API v2.0.2 says quantity, together with the
/// active flag and the vendor's sales buffer, decides whether a product is shown). The channel is
/// enabled only when its client credentials are configured. The batch size is our own bound; the
/// public document states no per-request product limit, only a 50-60 requests per minute rate limit.
/// </summary>
public sealed class YemeksepetiAvailabilityPublisher : IAvailabilityChannelPublisher
{
    private readonly IYemeksepetiProductMappingService _mappings;
    private readonly IYemeksepetiPartnerClient _client;
    private readonly ISecretProvider _secrets;

    public YemeksepetiAvailabilityPublisher(
        IYemeksepetiProductMappingService mappings, IYemeksepetiPartnerClient client, ISecretProvider secrets)
    {
        _mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
    }

    public string Channel => "Yemeksepeti";

    public bool IsEnabled => _secrets.GetValue(YemeksepetiPartnerHttpClient.ClientId) is not null;

    public int MaxBatchSize => 100;

    public async Task<IReadOnlyList<PublishedChannelProduct>> PublishedProductsAsync(int limit, CancellationToken cancellationToken = default) =>
        (await _mappings.ListOpenMappingsAsync(limit, cancellationToken).ConfigureAwait(false))
        .Select(m => new PublishedChannelProduct(m.ProductId, m.ExternalSku))
        .ToList();

    public Task PublishAsync(IReadOnlyList<ChannelAvailability> availability, CancellationToken cancellationToken = default) =>
        _client.UpdateVendorCatalogAsync(
            availability.Select(a => new YemeksepetiCatalogProductUpdate(a.ExternalId, null, null, a.Quantity)).ToList(),
            cancellationToken);
}
