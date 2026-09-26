using ALKAROS.OnlineOrdering.Yemeksepeti.ProductMapping;
using ALKAROS.OnlineOrdering.Yemeksepeti.StatusSync;

namespace ALKAROS.OnlineOrdering.CatalogPublishing.Yemeksepeti;

/// <summary>
/// V12-ONL-004: the Yemeksepeti catalog channel. The external identifier is the V12-MAP-001 SKU
/// mapping — an existing open mapping is reused, a new one is made from the catalog SKU, so the
/// same product is always published under the same SKU. The public Partner API v2.0.2 lets a
/// vendor update only price and availability of products its catalog already holds (adding
/// products is a pilot-only beta call), and it has no field for names, tax rates, modifiers or
/// our categories — those capabilities are reported as unsupported on every publication.
/// </summary>
public sealed class YemeksepetiCatalogPublisher : ICatalogChannelPublisher
{
    public const string ChannelName = "Yemeksepeti";

    private static readonly HashSet<CatalogCapability> Supported =
        [CatalogCapability.UpdatePrice, CatalogCapability.UpdateAvailability];

    private readonly IYemeksepetiProductMappingService _mappings;
    private readonly IYemeksepetiPartnerClient _client;
    private readonly TimeProvider _time;

    public YemeksepetiCatalogPublisher(IYemeksepetiProductMappingService mappings, IYemeksepetiPartnerClient client, TimeProvider time)
    {
        _mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    public string Channel => ChannelName;

    public IReadOnlySet<CatalogCapability> SupportedCapabilities => Supported;

    public async Task<string?> AssignExternalIdAsync(
        Guid productId, string productSku, bool createIfMissing, Guid actorId, CancellationToken cancellationToken = default)
    {
        var existing = await _mappings.FindOpenSkuForProductAsync(productId, cancellationToken).ConfigureAwait(false);
        if (existing is not null || !createIfMissing)
            return existing;

        // A mapping may legitimately move a SKU to another product over time, but publishing must never
        // do that implicitly: a catalog SKU that already stands for a different product stays with it. The
        // check and the write are one locked step (V12-RMD-005).
        try
        {
            var mapping = await _mappings.MapIfUnownedAsync(productSku, productId, _time.GetUtcNow(), actorId, cancellationToken)
                .ConfigureAwait(false);
            return mapping?.ExternalSku;
        }
        catch (ProductMappingRejectedException)
        {
            // The mapping's own typed refusal (e.g. the catalog SKU already stands for another product);
            // the publication reports the product as ExternalIdUnavailable instead of guessing.
            return null;
        }
    }

    public Task<string?> PublishAsync(IReadOnlyList<CatalogPublicationItem> items, CancellationToken cancellationToken = default) =>
        _client.UpdateVendorCatalogAsync(
            items.Select(item => new YemeksepetiCatalogProductUpdate(item.ExternalSku, item.Price, item.Active, null)).ToList(),
            cancellationToken);
}
