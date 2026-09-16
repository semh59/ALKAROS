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
    bool IsAgeRestricted = false,
    // V1-RMD-220: found by an independent audit (2026-09-16) — the domain
    // model (KitchenTicketItem.IsHeld, V1-WTR-025) has carried this since
    // the course system shipped, but it was never copied into this DTO, so
    // the KDS screen could never tell a deliberately-held course item
    // apart from a normal Queued one and staff could start it early.
    bool IsHeld = false);

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
    IReadOnlyList<KitchenTicketItemV1> Items,
    // V1-KIT-012: resolved fresh from orders.orders + table_mgmt.tables at
    // read time (same precedent as KitchenOrderSubmissionDispatcher's own
    // age-restriction/category lookups, V1-RMD-137/V1-KIT-006) — never
    // stored on kitchen.kitchen_tickets itself. Both null for a table-less
    // order (takeaway/bar tab) — orders.orders.table_id is nullable.
    Guid? TableId = null,
    string? TableNumber = null);

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

/// <summary>
/// V1-RMD-219: a minimal read of catalog.categories for the routing form's
/// own dropdown - just enough to name a category, nothing catalog.manage
/// itself would gate (price, availability, product membership).
/// </summary>
public sealed record KitchenCategoryV1(Guid Id, string Name);

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

/// <summary>
/// V1-KIT-010: whether kitchen.live_sync_enabled is on for this deployment
/// — read-only, the setting itself is changed elsewhere (Settings
/// module). V1-KIT-013: DenseModeThreshold has its own name on purpose —
/// it is a separate typed setting (kitchen.dense_mode_threshold,
/// V1-SET-005), not derived from Enabled. Both are exposed on the same
/// response so the Kitchen screen's one `/operations/live-sync` call
/// (already fetched on every load) covers everything it needs to know
/// about its own silent behavior toggles, rather than adding a second GET.
/// </summary>
public sealed record LiveSyncStatusV1(bool Enabled, int DenseModeThreshold);

/// <summary>
/// V1-KIT-014: research-grounded answer to Toast's "Tickets by Hour"/
/// "Tickets by Fulfillment Time" and Lightspeed's "KDS Statistics"
/// (docs/engineering/kitchen-allday-view-and-performance-report-research.md).
/// Built entirely from kitchen.kitchen_tickets' own existing timestamps —
/// no new event-tracking schema. <see cref="StationPerformanceV1.TargetMinutes"/>
/// is honestly a fixed global default (KitchenTicket.DefaultTargetPrepMinutes),
/// not a per-product estimate a Toast/Lightspeed-class system would carry.
/// </summary>
public sealed record KitchenPerformanceReportV1(
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<StationPerformanceV1> Stations,
    IReadOnlyList<HourlyVolumeV1> HourlyVolume);

/// <summary>
/// One station's timing over the report window, for tickets that reached
/// Ready (a ticket still open at query time is not a data point, it is
/// simply not counted yet). Mean AND median are both reported — the
/// research this task is grounded in found a real vendor's own docs
/// (Fresh KDS) warning that an average alone can hide extreme values.
/// </summary>
public sealed record StationPerformanceV1(
    string StationId,
    int CompletedTicketCount,
    double AverageMinutes,
    double MedianMinutes,
    int TargetMinutes,
    double TargetOverrunPercentage);

/// <summary>Completed-ticket volume for one UTC hour inside the report window.</summary>
public sealed record HourlyVolumeV1(DateTimeOffset HourStart, int CompletedTicketCount);

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

