namespace ALKAROS.Host.Experience.Orders;

public sealed record CreateTableDraftRequest(
    Guid TableId,
    string TableNumber,
    string WaiterName,
    IReadOnlyList<OrderItemDraftDto> Items,
    string? OrderNote = null);

public sealed record OrderItemDraftDto(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    IReadOnlyList<string>? Modifiers = null,
    string? SpecialInstructions = null);

public sealed record SubmitTableOrderRequest(
    Guid OrderId,
    long ExpectedRowVersion,
    string? ClientId = null,
    string? OperationId = null);

/// <summary>V1-ORD-005: request body for voiding a not-yet-sent item.</summary>
public sealed record VoidOrderItemRequestV1(
    long ExpectedRowVersion,
    string ReasonCode,
    string? Notes = null);

/// <summary>V1-ORD-005: response for a completed void.</summary>
public sealed record VoidOrderItemResultV1(
    Guid OrderId,
    Guid OrderItemId,
    string NewItemStatus,
    long NewOrderRowVersion,
    decimal NewOrderTotal,
    DateTimeOffset AppliedAt);

/// <summary>
/// V1-BIL-005: request body for the comp endpoint. IdempotencyKey identifies
/// one command instance across a pending-grant retry — the same key must be
/// reused after a manager approves, so the second call resolves to the same
/// grant row instead of raising a duplicate request.
/// </summary>
public sealed record ApplyComplimentaryRequestV1(
    string IdempotencyKey,
    long ExpectedRowVersion,
    string ReasonCode,
    string? Notes = null);

/// <summary>
/// V1-BIL-005: response for the comp endpoint. "Applied" carries the usual
/// result fields; "Pending" carries only GrantId — the caller polls the
/// authorization-decisions surface (V1-IAM-020) or retries this same request
/// (same IdempotencyKey) once a manager resolves it.
/// </summary>
public sealed record ApplyComplimentaryResultV1(
    string Status,
    Guid OrderId,
    Guid OrderItemId,
    string? NewItemStatus,
    long? NewOrderRowVersion,
    decimal? NewOrderTotal,
    DateTimeOffset? AppliedAt,
    Guid? GrantId);

/// <summary>
/// V1-IAM-027: request body for voiding a sent-but-unserved item.
/// IdempotencyKey plays the same role as in <see cref="ApplyComplimentaryRequestV1"/>
/// — a retry after a manager approves a pending bills.void grant reuses it.
/// </summary>
public sealed record VoidSentItemRequestV1(
    string IdempotencyKey,
    long ExpectedRowVersion,
    string ReasonCode,
    string? Notes = null);

/// <summary>
/// V1-IAM-027: response for the sent-item void endpoint. "Applied" also
/// reports whether a matching kitchen ticket item was found and cancelled,
/// and whether a billed line was converted to BillLineType.Waste.
/// </summary>
public sealed record VoidSentItemResultV1(
    string Status,
    Guid OrderId,
    Guid OrderItemId,
    long? NewOrderRowVersion,
    decimal? NewOrderTotal,
    bool? KitchenTicketItemCancelled,
    bool? BillLineConvertedToWaste,
    DateTimeOffset? AppliedAt,
    Guid? GrantId);

public sealed record OrderDto(
    Guid OrderId,
    Guid TableId,
    string TableNumber,
    string Status,
    long RowVersion,
    decimal TotalAmount,
    IReadOnlyList<OrderItemDto> Items,
    DateTimeOffset CreatedAt);

public sealed record OrderItemDto(
    Guid ItemId,
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    string? SpecialInstructions);
