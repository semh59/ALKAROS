using System.Text.Json.Serialization;

namespace ALKAROS.Host.Experience.KitchenOperations;

public sealed record KitchenTicketItemV1(
    Guid Id,
    Guid OrderItemId,
    Guid ProductId,
    string ProductName,
    decimal Quantity,
    string? Modifiers,
    string? Notes,
    string Status,
    long RowVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? ReadyAt,
    DateTimeOffset? ServedAt,
    DateTimeOffset? CancelledAt,
    // V1-RMD-137: found by an independent audit (2026-09-09) — surfaces the
    // ticket item's own point-in-time age-restriction snapshot so a kitchen
    // screen can prompt an ID check before the item is served, same as the
    // printed ticket's own marker (EscPosTicketFormatter).
    bool IsAgeRestricted = false);

public sealed record KitchenTicketV1(
    Guid Id,
    Guid OrderId,
    string TicketNumber,
    string StationId,
    string Status,
    long RowVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? ReadyAt,
    DateTimeOffset? CancelledAt,
    int TargetPrepMinutes,
    IReadOnlyList<KitchenTicketItemV1> Items);

public sealed record TransitionKitchenTicketV1(
    string TargetState,
    long ExpectedRowVersion,
    string? Reason = null);

public sealed record TransitionKitchenItemV1(
    string TargetState,
    long ExpectedTicketRowVersion,
    long ExpectedItemRowVersion,
    string? Reason = null);

/// <summary>V1-KIT-009: reverses an item's most recent transition within its short undo window (KitchenTicketItem.CanUndo) — no TargetState, there is only one valid direction.</summary>
public sealed record UndoKitchenItemV1(
    long ExpectedTicketRowVersion,
    long ExpectedItemRowVersion);

public sealed record PrinterV1(
    Guid Id,
    string Name,
    string StationId,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record UpdatePrinterV1(string Name, string StationId, bool IsActive);

public sealed record PrinterRouteV1(
    Guid Id,
    string RouteLevel,
    Guid PrinterId,
    Guid? ItemId,
    Guid? ProductId,
    Guid? CategoryId,
    DateOnly? SpecialDate,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record UpdatePrinterRouteV1(
    string RouteLevel,
    Guid PrinterId,
    Guid? ItemId = null,
    Guid? ProductId = null,
    Guid? CategoryId = null,
    DateOnly? SpecialDate = null,
    bool IsActive = true);

public sealed record PrintJobV1(
    Guid Id,
    Guid TicketId,
    Guid PrinterId,
    string Status,
    int AttemptCount,
    int MaxAttempts,
    DateTimeOffset? NextAttemptAt,
    DateTimeOffset? LeaseExpiresAt,
    DateTimeOffset? PrintedAt,
    DateTimeOffset? FailedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record PhysicalPrintDeliveryV1(
    Guid Id,
    Guid PrintJobId,
    Guid TicketId,
    Guid PrinterId,
    string Status,
    int AttemptNumber,
    bool IsReprint,
    string? OperatorReason,
    string? CrashReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? ResolvedAt,
    long RowVersion);

public sealed record ReprintDecisionV1(string Reason);

public sealed record BackupV1(
    Guid BackupId,
    string BackupType,
    long FileSizeBytes,
    string Status,
    string? ErrorMessage,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int RetentionDays);

/// <summary>V1-KIT-010: whether kitchen.live_sync_enabled is on for this deployment — read-only, the setting itself is changed elsewhere (Settings module).</summary>
public sealed record LiveSyncStatusV1(bool Enabled);

/// <summary>
/// V1-KIT-008: the result of 86-ing a product from the Kitchen screen. A
/// deliberately narrow projection of Catalog's own ProductV1 — the Kitchen
/// HTTP surface only ever needs to confirm the id and the new availability,
/// not re-expose SKU/pricing/modifier data that belongs to Catalog
/// Management. <see cref="PlanConflict"/> is the simple first-pass rule from
/// this task's scope: true when the product was still on active sale
/// (IsAvailable = true) the instant before this suspend, in which case an
/// informational authorization_grants row was also written (see
/// KitchenOperationsStore.SuspendProductAvailabilityAsync).
/// </summary>
public sealed record ProductAvailabilitySuspendedV1(
    Guid ProductId,
    bool IsAvailable,
    bool PlanConflict);

public sealed record HealthSnapshotV1(
    Guid SnapshotId,
    string DatabaseStatus,
    string DiskStatus,
    string LastBackupStatus,
    long FreeDiskBytes,
    long DatabaseSizeBytes,
    DateTimeOffset CapturedAt);

public sealed record StartBackupV1(
    string BackupType,
    int RetentionDays = 30);

public sealed record AuditEventV1(
    Guid Id,
    string EventName,
    string AggregateType,
    Guid AggregateId,
    string ActorType,
    string? Reason,
    string CorrelationId,
    DateTimeOffset OccurredAt);

public sealed record KitchenOperationsErrorEnvelopeV1(
    [property: JsonPropertyName("error")] KitchenOperationsErrorV1 Error);

public sealed record KitchenOperationsErrorV1(
    string Code,
    string Message,
    int Status,
    string TraceId);

public sealed record ProductionBackupOptions(string? BackupDirectory);

/// <summary>
/// A deployment-specific provider must produce a verified database dump. The
/// HTTP surface deliberately has no synthetic payload fallback.
/// </summary>
public interface IProductionBackupPayloadProvider
{
    Task<byte[]> CreatePayloadAsync(
        ALKAROS.Operations.BackupHealth.BackupType backupType,
        CancellationToken cancellationToken = default);
}

public sealed class KitchenOperationsUnauthorizedException : Exception
{
    public KitchenOperationsUnauthorizedException(string message) : base(message) { }
}

public sealed class KitchenOperationsForbiddenException : Exception
{
    public KitchenOperationsForbiddenException(string message) : base(message) { }
}

public sealed class KitchenOperationsNotFoundException : Exception
{
    public KitchenOperationsNotFoundException(string message) : base(message) { }
}

public sealed class KitchenOperationsConcurrencyException : Exception
{
    public KitchenOperationsConcurrencyException(string message) : base(message) { }
}

