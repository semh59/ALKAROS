namespace ALKAROS.Host.Experience.Orders;

public sealed record CreateTableDraftRequest(
    Guid TableId,
    string TableNumber,
    string WaiterName,
    IReadOnlyList<OrderItemDraftDto> Items,
    string? OrderNote = null);

/// <summary>
/// <paramref name="Id"/> is the client-generated cart-line id (both PWAs
/// already assign one per line for their own rendering). Reusing it as the
/// resulting OrderItem's id makes a retried draft submission idempotent —
/// SaveAsync's own known-vs-new-id diffing updates the existing row in
/// place instead of inserting a duplicate line (found while fixing the
/// table-draft merge defect, 2026-09-06: appending on every call, safe for
/// a genuinely new round of items, would otherwise double an item whose
/// request was retried after an ambiguous network failure).
/// </summary>
public sealed record OrderItemDraftDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    IReadOnlyList<string>? Modifiers = null,
    string? SpecialInstructions = null);

/// <summary>
/// V1-RMD-113: OperationId is now load-bearing — it is this request's
/// idempotency key (<see cref="ALKAROS.Orders.SubmitOrder.SubmitOrderCommand"/>.
/// OperationId), no longer accepted and discarded. ClientId is dropped: the
/// server derives it from the authenticated terminal, matching the
/// terminal-wide quick-sale submit endpoint's own convention.
/// </summary>
public sealed record SubmitTableOrderRequest(
    Guid OrderId,
    long ExpectedRowVersion,
    string OperationId);

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

/// <summary>
/// V1-RMD-111: request body for the garson-masa hand-off endpoint. The
/// caller needs orders.transfer-server when FromUserId is their own user id,
/// orders.transfer-server-any otherwise (docs/domain/authorization-model.md
/// §3).
/// </summary>
public sealed record TransferServingUserRequestV1(Guid FromUserId, Guid ToUserId);

/// <summary>V1-RMD-111: response for the garson-masa hand-off endpoint.</summary>
public sealed record TransferServingUserResultV1(int OrdersReassigned);

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
