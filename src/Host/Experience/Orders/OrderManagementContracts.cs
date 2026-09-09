namespace ALKAROS.Host.Experience.Orders;

/// <summary>
/// <paramref name="Id"/> (V1-RMD-123) is the client-generated correlation id
/// for this whole "send to kitchen" submission — both production clients
/// (waiter-app.js, cashier-app.js) already generate it once and resend the
/// identical value on every retry of the same draft+submit attempt, but the
/// server used to ignore it entirely. Persisted as the resulting order's
/// <see cref="ALKAROS.Orders.OrderAggregate.Order.SourceReferenceId"/> and
/// enforced by a partial unique index on (table_id, source_reference_id):
/// a retry that arrives after the order was already fully submitted (the
/// response was merely lost) now replays the existing order instead of
/// starting a second, duplicate one that gets dispatched to the kitchen
/// twice. Optional for backward compatibility with a caller that omits it —
/// such a call gets no retry protection beyond the existing per-item id
/// dedup, exactly like before this fix.
/// </summary>
public sealed record CreateTableDraftRequest(
    Guid TableId,
    string TableNumber,
    string WaiterName,
    IReadOnlyList<OrderItemDraftDto> Items,
    string? OrderNote = null,
    Guid? Id = null);

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
/// whether a billed line was converted to BillLineType.Waste, and
/// (V1-RMD-143 follow-up, 2026-09-09) whether the item's own consumed stock
/// was given back — only when the kitchen had not started on it yet.
/// </summary>
public sealed record VoidSentItemResultV1(
    string Status,
    Guid OrderId,
    Guid OrderItemId,
    long? NewOrderRowVersion,
    decimal? NewOrderTotal,
    bool? KitchenTicketItemCancelled,
    bool? BillLineConvertedToWaste,
    bool? StockRestored,
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

/// <summary>
/// <paramref name="AvailableStockQuantity"/> (V1-RMD-143, Semih's own
/// "kalan stok bilgisi ver garsona") is populated only by
/// <c>OrderManagementStore.GetOrderByIdAsync</c> — how many more units of
/// this product the mapped stock item(s) could still cover right now, or
/// null when the product has no stock mapping configured at all (not
/// tracked, not "zero left"). Purely informational: OrderStockConsumption
/// Service's own check at Accept time is the real, authoritative gate.
/// </summary>
public sealed record OrderItemDto(
    Guid ItemId,
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    string? SpecialInstructions,
    decimal? AvailableStockQuantity = null);
