using System;
using System.Collections.Generic;
using System.Linq;

namespace ALKAROS.Recipes.CostSnapshots;

public sealed class RecipeCostSnapshotItem
{
    public Guid Id { get; }
    public Guid SnapshotId { get; }
    public Guid StockItemId { get; }
    public decimal RawQuantity { get; }
    public decimal WasteFactor { get; }
    public decimal EffectiveNativeQuantity { get; }
    public string NativeUnitCode { get; }
    public decimal StockQuantity { get; }
    public string StockUnitCode { get; }
    public decimal UnitCost { get; }
    public decimal LineCost { get; }
    public DateTimeOffset CreatedAt { get; }

    public RecipeCostSnapshotItem(
        Guid id,
        Guid snapshotId,
        Guid stockItemId,
        decimal rawQuantity,
        decimal wasteFactor,
        decimal effectiveNativeQuantity,
        string nativeUnitCode,
        decimal stockQuantity,
        string stockUnitCode,
        decimal unitCost,
        decimal lineCost,
        DateTimeOffset createdAt)
    {
        if (rawQuantity <= 0)
        {
            throw new InvalidCostSnapshotException("Raw quantity must be greater than zero.");
        }
        if (wasteFactor < 0)
        {
            throw new InvalidCostSnapshotException("Waste factor cannot be negative.");
        }
        if (unitCost < 0)
        {
            throw new InvalidCostSnapshotException("Unit cost cannot be negative.");
        }
        if (string.IsNullOrWhiteSpace(nativeUnitCode))
        {
            throw new InvalidCostSnapshotException("Native unit code is required.");
        }
        if (string.IsNullOrWhiteSpace(stockUnitCode))
        {
            throw new InvalidCostSnapshotException("Stock unit code is required.");
        }

        Id = id;
        SnapshotId = snapshotId;
        StockItemId = stockItemId;
        RawQuantity = rawQuantity;
        WasteFactor = wasteFactor;
        EffectiveNativeQuantity = effectiveNativeQuantity;
        NativeUnitCode = nativeUnitCode.Trim().ToLowerInvariant();
        StockQuantity = stockQuantity;
        StockUnitCode = stockUnitCode.Trim().ToLowerInvariant();
        UnitCost = unitCost;
        LineCost = lineCost;
        CreatedAt = createdAt;
    }

    public static RecipeCostSnapshotItem Create(
        Guid snapshotId,
        Guid stockItemId,
        decimal rawQuantity,
        decimal wasteFactor,
        string nativeUnitCode,
        decimal stockQuantity,
        string stockUnitCode,
        decimal unitCost,
        Guid? id = null)
    {
        var effectiveNativeQuantity = Math.Round(rawQuantity * (1m + wasteFactor), 4, MidpointRounding.AwayFromZero);
        var lineCost = Math.Round(stockQuantity * unitCost, 2, MidpointRounding.AwayFromZero);

        return new RecipeCostSnapshotItem(
            id ?? Guid.NewGuid(),
            snapshotId,
            stockItemId,
            rawQuantity,
            wasteFactor,
            effectiveNativeQuantity,
            nativeUnitCode,
            stockQuantity,
            stockUnitCode,
            unitCost,
            lineCost,
            DateTimeOffset.UtcNow);
    }
}

public sealed class RecipeCostSnapshot
{
    private readonly List<RecipeCostSnapshotItem> _items = new();

    public Guid Id { get; }
    public Guid RecipeVersionId { get; }
    public DateOnly CostBasisDate { get; }
    public decimal CalculatedCost { get; private set; }
    public string Currency { get; }
    public DateTimeOffset CreatedAt { get; }
    public IReadOnlyList<RecipeCostSnapshotItem> Items => _items.AsReadOnly();

    public RecipeCostSnapshot(
        Guid id,
        Guid recipeVersionId,
        DateOnly costBasisDate,
        decimal calculatedCost,
        string currency,
        DateTimeOffset createdAt,
        IEnumerable<RecipeCostSnapshotItem>? items = null)
    {
        if (recipeVersionId == Guid.Empty)
        {
            throw new InvalidCostSnapshotException("RecipeVersionId is required.");
        }

        Id = id;
        RecipeVersionId = recipeVersionId;
        CostBasisDate = costBasisDate;
        CalculatedCost = calculatedCost;
        Currency = string.IsNullOrWhiteSpace(currency) ? "TRY" : currency.Trim().ToUpperInvariant();
        CreatedAt = createdAt;

        if (items != null)
        {
            _items.AddRange(items);
        }
    }

    public static RecipeCostSnapshot Create(
        Guid recipeVersionId,
        DateOnly costBasisDate,
        string currency = "TRY",
        Guid? id = null)
    {
        return new RecipeCostSnapshot(
            id ?? Guid.NewGuid(),
            recipeVersionId,
            costBasisDate,
            0m,
            currency,
            DateTimeOffset.UtcNow);
    }

    public void AddItem(RecipeCostSnapshotItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _items.Add(item);
        CalculatedCost = _items.Sum(i => i.LineCost);
    }
}
