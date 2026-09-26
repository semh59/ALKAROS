using System.Security.Cryptography;
using System.Text;

namespace ALKAROS.OnlineOrdering.Providers.TrendyolGo.OrderIntake;

/// <summary>
/// V12-TGO-002. Uber Eats Trendyol Go's order events as stored in the shared inbox. UNVERIFIED DRAFT: taken from the
/// public developer document (EXT:TGO-MEAL-API, "Event Types and Event Payloads", "Create Integrator", "Get Order
/// Packages", read 2026-09-27); no real delivery has been seen (V12-TGO-001 Blocked; C106 waiver).
/// <list type="bullet">
/// <item>Each webhook event type is POSTed to its own path (the integrator's <c>baseUrl</c> + <c>destinationUrl</c>);
/// the payload itself does not name its type, so the path does.</item>
/// <item>Every payload carries the package <c>id</c> (64 alphanumeric characters), the same across a package's
/// events; it is the order's external identity here.</item>
/// <item>A polled package names its state in <c>packageStatus</c> (Created, Picking, Invoiced, Shipped, Delivered,
/// Cancelled, UnSupplied); it is stored under the same event name as its webhook counterpart.</item>
/// <item>Each state happens once per package, so the event key is the platform, the event and the package: a
/// webhook delivery, its retries (the document warns of short-term duplicates) and a polled copy are one row.</item>
/// </list>
/// </summary>
public static class TrendyolGoEvents
{
    public const string Provider = "trendyol-go";

    public const string Created = "created";
    public const string Cancelled = "cancelled";
    public const string Unsupplied = "unsupplied";
    public const string Shipped = "shipped";
    public const string Delivered = "delivered";
    public const string PickupEtaCalculated = "pickupEtaCalculated";
    public const string CourierNearby = "courierNearby";
    public const string StoreChanged = "storeChanged";
    public const string SellerChanged = "sellerChanged";

    /// <summary>Restaurant-side package states only seen by polling: echoes of this restaurant's own acceptance and readiness.</summary>
    public const string Picking = "picking";
    public const string Invoiced = "invoiced";

    /// <summary>The event types a webhook path may name.</summary>
    public static IReadOnlySet<string> WebhookEventTypes { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        Created, Cancelled, Unsupplied, Shipped, Delivered, PickupEtaCalculated, CourierNearby, StoreChanged, SellerChanged
    };

    /// <summary>The stored event name of a polled package's <c>packageStatus</c>; null for a status the document does not list.</summary>
    public static string? FromPackageStatus(string? packageStatus) => packageStatus switch
    {
        "Created" => Created,
        "Picking" => Picking,
        "Invoiced" => Invoiced,
        "Shipped" => Shipped,
        "Delivered" => Delivered,
        "Cancelled" => Cancelled,
        "UnSupplied" => Unsupplied,
        _ => null
    };

    /// <summary>The inbox key of one package event (lowercase SHA-256 hex).</summary>
    public static string EventKey(string eventType, string packageId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\u001f', Provider, eventType, packageId))))
            .ToLowerInvariant();
}
