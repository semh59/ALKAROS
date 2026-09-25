namespace ALKAROS.OnlineOrdering.Yemeksepeti.ProductMapping;

/// <summary>
/// One period during which a Yemeksepeti SKU stood for one catalog product. Yemeksepeti
/// Partner API v2.0.2 identifies a product by its <c>sku</c> both in the catalog it
/// receives and in every order item it sends (EXT:YSP-PARTNER-2.0.2, public document; no
/// sandbox verification exists — V0-YSP-001 is Blocked).
/// </summary>
public sealed record YemeksepetiProductMapping(
    Guid MappingId,
    string ExternalSku,
    Guid ProductId,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo);

public enum ProductMappingResolutionOutcome
{
    Resolved,

    /// <summary>No mapping was in effect for the SKU at that moment.</summary>
    Unmapped,

    /// <summary>More than one mapping claims the SKU at that moment; nothing is chosen.</summary>
    Ambiguous,

    /// <summary>The mapped product is no longer active in the catalog.</summary>
    ProductInactive,

    /// <summary>The mapped product now demands a modifier choice the provider cannot send.</summary>
    ProductRequiresModifierChoice
}

/// <summary>What a provider SKU means at one moment. Only <see cref="ProductMappingResolutionOutcome.Resolved"/> may create an order line.</summary>
public sealed record ProductMappingResolution(
    ProductMappingResolutionOutcome Outcome,
    string ExternalSku,
    Guid? ProductId,
    Guid? MappingId)
{
    public bool IsResolved => Outcome == ProductMappingResolutionOutcome.Resolved;
}

public enum ProductMappingRejection
{
    ProductNotFound,
    ProductInactive,

    /// <summary>
    /// The product has an active modifier group with a minimum selection. The public
    /// Partner API v2.0.2 order item carries no modifier or topping field, so such a
    /// product could never be ordered complete through this channel.
    /// </summary>
    ProductRequiresModifierChoice,

    /// <summary>The SKU already has a mapping that starts at or after the requested moment; history is never rewritten.</summary>
    LaterMappingExists,

    /// <summary>The product is already published under another SKU.</summary>
    ProductMappedToAnotherSku
}

public sealed class ProductMappingRejectedException : Exception
{
    public ProductMappingRejectedException(ProductMappingRejection rejection, string externalSku, Guid productId)
        : base($"Yemeksepeti SKU '{externalSku}' cannot be mapped to product '{productId}': {rejection}.")
    {
        Rejection = rejection;
        ExternalSku = externalSku;
        ProductId = productId;
    }

    public ProductMappingRejection Rejection { get; }
    public string ExternalSku { get; }
    public Guid ProductId { get; }
}

public sealed class InvalidProductMappingRequestException : Exception
{
    public InvalidProductMappingRequestException(string message) : base(message) { }
}
