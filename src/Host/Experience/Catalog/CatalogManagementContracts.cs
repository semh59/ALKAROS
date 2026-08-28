using System.Text.Json.Serialization;
using ALKAROS.Catalog.Pricing;
using ALKAROS.Catalog.ProductCatalog;

namespace ALKAROS.Host.Experience.Catalog;

public sealed record CatalogPageV1<T>(IReadOnlyList<T> Items, string? NextCursor);

public sealed record CategoryV1(
    Guid Id,
    string Code,
    string Name,
    Guid? ParentId,
    int SortOrder,
    bool Active);

public sealed record CreateCategoryV1(
    Guid Id,
    string Code,
    string Name,
    Guid? ParentId = null,
    int SortOrder = 0,
    bool Active = true);

public sealed record TaxProfileV1(Guid Id, string Code, string Name, decimal VatRate, bool Active);

public sealed record CreateTaxProfileV1(
    Guid Id,
    string Code,
    string Name,
    decimal VatRate,
    bool Active = true);

public sealed record ProductV1(
    Guid Id,
    string Sku,
    string Name,
    ProductType ProductType,
    StockMode StockMode,
    Guid? CategoryId,
    Guid? TaxProfileId,
    string? Description,
    string? PrinterRoutePolicy,
    int DisplayOrder,
    decimal? CurrentPrice,
    bool Active);

public sealed record CreateProductV1(
    Guid Id,
    string Sku,
    string Name,
    ProductType ProductType,
    StockMode StockMode,
    Guid? CategoryId = null,
    Guid? TaxProfileId = null,
    string? Description = null,
    string? PrinterRoutePolicy = null,
    int DisplayOrder = 0,
    decimal? CurrentPrice = null,
    bool Active = true);

public sealed record ModifierGroupV1(
    Guid Id,
    string Code,
    string Name,
    SelectionType SelectionType,
    int MinSelections,
    int MaxSelections,
    bool Active);

public sealed record CreateModifierGroupV1(
    Guid Id,
    string Code,
    string Name,
    SelectionType SelectionType,
    int MinSelections = 0,
    int MaxSelections = 1,
    bool Active = true);

public sealed record ModifierV1(
    Guid Id,
    Guid ModifierGroupId,
    string Code,
    string Name,
    decimal PriceDelta,
    Guid? ProductId,
    bool Active);

public sealed record CreateModifierV1(
    Guid Id,
    Guid ModifierGroupId,
    string Code,
    string Name,
    decimal PriceDelta = 0,
    Guid? ProductId = null,
    bool Active = true);

public sealed record ProductModifierAssignmentV1(Guid Id, Guid ProductId, Guid ModifierGroupId);

public sealed record CreateProductModifierAssignmentV1(Guid Id, Guid ProductId, Guid ModifierGroupId);

public sealed record ProductPriceV1(
    Guid Id,
    Guid ProductId,
    PriceType PriceType,
    decimal Price,
    string CurrencyCode,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo);

public sealed record CreateProductPriceV1(
    Guid Id,
    Guid ProductId,
    PriceType PriceType,
    decimal Price,
    DateTimeOffset EffectiveFrom,
    string CurrencyCode = "TRY",
    DateTimeOffset? EffectiveTo = null);

public sealed record CatalogApiErrorEnvelopeV1(
    [property: JsonPropertyName("error")] CatalogApiErrorV1 Error);

public sealed record CatalogApiErrorV1(string Code, string Message, int Status, string TraceId);
