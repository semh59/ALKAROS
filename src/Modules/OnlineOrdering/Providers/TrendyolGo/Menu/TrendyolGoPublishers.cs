using System.Globalization;
using ALKAROS.OnlineOrdering.AvailabilityPublishing;
using ALKAROS.OnlineOrdering.CatalogPublishing;
using ALKAROS.OnlineOrdering.Yemeksepeti.ProductMapping;

namespace ALKAROS.OnlineOrdering.Providers.TrendyolGo.Menu;

/// <summary>
/// V12-TGO-004: the Trendyol Go availability channel. The platform takes no stock quantity, only whether a product is on
/// sale: a sellable quantity of zero takes it off (PASSIVE), anything else puts it on (ACTIVE). It knows the products
/// that have an open <c>trendyol-go</c> mapping. UNVERIFIED DRAFT (see <see cref="TrendyolGoMenuClient"/>).
/// </summary>
public sealed class TrendyolGoAvailabilityPublisher : IAvailabilityChannelPublisher
{
    public const string ChannelName = "Trendyol Go";

    private readonly IYemeksepetiProductMappingService _mappings;
    private readonly TrendyolGoMenuClient _client;

    /// <param name="mappings">The shared mapping service reading <c>trendyol-go</c> rows.</param>
    public TrendyolGoAvailabilityPublisher(IYemeksepetiProductMappingService mappings, TrendyolGoMenuClient client)
    {
        _mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public string Channel => ChannelName;

    public bool IsEnabled => _client.IsConfigured;

    /// <summary>One status call per product; far below the documented 50 calls per 10 seconds per pass.</summary>
    public int MaxBatchSize => 40;

    public async Task<IReadOnlyList<PublishedChannelProduct>> PublishedProductsAsync(int limit, CancellationToken cancellationToken = default) =>
        (await _mappings.ListOpenMappingsAsync(limit, cancellationToken).ConfigureAwait(false))
        .Select(m => new PublishedChannelProduct(m.ProductId, m.ExternalSku))
        .ToList();

    public async Task PublishAsync(IReadOnlyList<ChannelAvailability> availability, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(availability);
        foreach (var line in availability)
            await _client.SetProductActiveAsync(TrendyolGoCatalogPublisher.PlatformProductId(line.ExternalId), line.Quantity > 0, cancellationToken)
                .ConfigureAwait(false);
    }
}

/// <summary>
/// V12-TGO-004: the Trendyol Go catalog channel. Products are created and named in the seller panel, so a product's
/// external id is its existing <c>trendyol-go</c> mapping (the platform's numeric product id) — never made up from our
/// catalog SKU — and it is used only when the product is really on the store's menu (read once per publication). A
/// publication sends each price change through the queued price update and waits briefly for its batch result: a row the
/// platform refused fails the publication with the product ids and reasons; a batch still running is recorded by its id.
/// Availability is published as each product's on-sale status. UNVERIFIED DRAFT (see <see cref="TrendyolGoMenuClient"/>).
/// </summary>
public sealed class TrendyolGoCatalogPublisher : ICatalogChannelPublisher
{
    private static readonly HashSet<CatalogCapability> Supported = [CatalogCapability.UpdatePrice, CatalogCapability.UpdateAvailability];
    private static readonly TimeSpan[] BatchChecks = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)];

    private readonly IYemeksepetiProductMappingService _mappings;
    private readonly TrendyolGoMenuClient _client;
    private readonly TimeProvider _time;
    private IReadOnlySet<long>? _menu;

    public TrendyolGoCatalogPublisher(IYemeksepetiProductMappingService mappings, TrendyolGoMenuClient client, TimeProvider time)
    {
        _mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    public string Channel => TrendyolGoAvailabilityPublisher.ChannelName;

    public IReadOnlySet<CatalogCapability> SupportedCapabilities => Supported;

    /// <summary>The platform's product id of a mapping's external SKU (always numeric on this platform).</summary>
    public static long PlatformProductId(string externalSku) =>
        long.TryParse(externalSku, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
            ? id
            : throw new TrendyolGoMenuException($"'{externalSku}' is not a Trendyol Go product id.");

    public async Task<string?> AssignExternalIdAsync(
        Guid productId, string productSku, bool createIfMissing, Guid actorId, CancellationToken cancellationToken = default)
    {
        var mapped = await _mappings.FindOpenSkuForProductAsync(productId, cancellationToken).ConfigureAwait(false);
        if (mapped is null
            || !long.TryParse(mapped, NumberStyles.None, CultureInfo.InvariantCulture, out var platformId) || platformId <= 0)
            return null;
        _menu ??= await _client.MenuProductIdsAsync(cancellationToken).ConfigureAwait(false);
        return _menu.Contains(platformId) ? mapped : null;
    }

    public async Task<string?> PublishAsync(IReadOnlyList<CatalogPublicationItem> items, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var item in items)
            await _client.SetProductActiveAsync(PlatformProductId(item.ExternalSku), item.Active, cancellationToken).ConfigureAwait(false);

        string? lastBatch = null;
        foreach (var chunk in items.Chunk(TrendyolGoMenuClient.MaxPriceItems))
        {
            var batchId = await _client.UpdatePricesAsync(
                chunk.Select(item => new TrendyolGoPriceUpdate(PlatformProductId(item.ExternalSku), item.Price)).ToList(), cancellationToken)
                .ConfigureAwait(false);
            lastBatch = batchId;
            foreach (var wait in BatchChecks)
            {
                await Task.Delay(wait, _time, cancellationToken).ConfigureAwait(false);
                var result = await _client.BatchResultAsync(batchId, cancellationToken).ConfigureAwait(false);
                if (result.FailedItemCount > 0 || result.Failures.Count > 0)
                    throw new TrendyolGoMenuException(
                        $"Trendyol Go refused {Math.Max(result.FailedItemCount, result.Failures.Count)} price(s) in batch {batchId}: "
                        + string.Join("; ", result.Failures.Select(f => string.Create(CultureInfo.InvariantCulture, $"{f.ProductId} {f.Reason}"))));
                if (string.Equals(result.Status, "COMPLETED", StringComparison.Ordinal))
                    break;
            }
        }

        return lastBatch;
    }
}
