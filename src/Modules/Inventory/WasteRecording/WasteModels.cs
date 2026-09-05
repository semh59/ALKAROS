namespace ALKAROS.Inventory.WasteRecording;

public static class WasteSources
{
    public const string Production = "Production";
    public const string PortionReservation = "PortionReservation";
    public const string Manual = "Manual";
    public const string Spoilage = "Spoilage";
    public const string Expiration = "Expiration";
    public const string PreparationDamage = "PreparationDamage";

    public static readonly IReadOnlySet<string> ValidSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Production,
        PortionReservation,
        Manual,
        Spoilage,
        Expiration,
        PreparationDamage
    };

    public static bool IsValid(string source)
    {
        return !string.IsNullOrWhiteSpace(source) && ValidSources.Contains(source.Trim());
    }
}

public sealed record RecordWasteRequest(
    Guid StockItemId,
    Guid StockLocationId,
    string WasteSource,
    decimal Quantity,
    string UnitCode,
    string Reason,
    Guid RecordedBy,
    Guid? SourceReferenceId = null,
    string? IdempotencyKey = null,
    string? MetadataJson = null,
    Guid? Id = null);

public sealed class WasteRecord
{
    public WasteRecord(
        Guid id,
        Guid stockMovementId,
        Guid stockItemId,
        Guid stockLocationId,
        string wasteSource,
        decimal quantity,
        string unitCode,
        decimal normalizedQuantity,
        string trackingUnitCode,
        string wasteReason,
        Guid recordedBy,
        DateTimeOffset recordedAt,
        Guid? sourceReferenceId = null,
        string? idempotencyKey = null,
        string? metadataJson = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (stockMovementId == Guid.Empty)
            throw new ArgumentException("StockMovementId cannot be empty.", nameof(stockMovementId));
        if (stockItemId == Guid.Empty)
            throw new ArgumentException("StockItemId cannot be empty.", nameof(stockItemId));
        if (stockLocationId == Guid.Empty)
            throw new ArgumentException("StockLocationId cannot be empty.", nameof(stockLocationId));
        if (string.IsNullOrWhiteSpace(wasteSource))
            throw new ArgumentException("WasteSource cannot be empty.", nameof(wasteSource));
        if (quantity <= 0m)
            throw new InvalidWasteQuantityException($"Quantity must be positive, got {quantity}.");
        if (normalizedQuantity <= 0m)
            throw new InvalidWasteQuantityException($"Normalized quantity must be positive, got {normalizedQuantity}.");
        if (string.IsNullOrWhiteSpace(unitCode))
            throw new ArgumentException("UnitCode cannot be empty.", nameof(unitCode));
        if (string.IsNullOrWhiteSpace(trackingUnitCode))
            throw new ArgumentException("TrackingUnitCode cannot be empty.", nameof(trackingUnitCode));
        if (string.IsNullOrWhiteSpace(wasteReason))
            throw new InvalidWasteReasonException("WasteReason cannot be empty.");
        if (recordedBy == Guid.Empty)
            throw new UnauthorizedWasteRecorderException("RecordedBy must be a valid non-empty Guid.");

        Id = id;
        StockMovementId = stockMovementId;
        StockItemId = stockItemId;
        StockLocationId = stockLocationId;
        WasteSource = wasteSource.Trim();
        SourceReferenceId = sourceReferenceId;
        IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        Quantity = quantity;
        UnitCode = unitCode.Trim().ToLowerInvariant();
        NormalizedQuantity = normalizedQuantity;
        TrackingUnitCode = trackingUnitCode.Trim().ToLowerInvariant();
        WasteReason = wasteReason.Trim();
        RecordedBy = recordedBy;
        RecordedAt = recordedAt;
        MetadataJson = metadataJson;
    }

    public Guid Id { get; }
    public Guid StockMovementId { get; }
    public Guid StockItemId { get; }
    public Guid StockLocationId { get; }
    public string WasteSource { get; }
    public Guid? SourceReferenceId { get; }
    public string? IdempotencyKey { get; }
    public decimal Quantity { get; }
    public string UnitCode { get; }
    public decimal NormalizedQuantity { get; }
    public string TrackingUnitCode { get; }
    public string WasteReason { get; }
    public Guid RecordedBy { get; }
    public DateTimeOffset RecordedAt { get; }
    public string? MetadataJson { get; }
}

public sealed record WasteRecordingResult(
    WasteRecord Record,
    ALKAROS.Inventory.MovementLedger.StockMovement Movement,
    bool IsIdempotentReplay);
