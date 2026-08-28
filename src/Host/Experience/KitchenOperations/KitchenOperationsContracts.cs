using System.Text.Json.Serialization;

namespace ALKAROS.Host.Experience.KitchenOperations;

public sealed record KitchenTicketItemV1(
    Guid Id,
    Guid OrderItemId,
    Guid ProductId,
    string ProductName,
    decimal Quantity,
    string? Modifiers,
    string Status,
    long RowVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? ReadyAt,
    DateTimeOffset? ServedAt,
    DateTimeOffset? CancelledAt);

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

