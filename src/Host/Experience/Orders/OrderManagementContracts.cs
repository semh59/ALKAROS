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
/// <remarks>
/// V1-RMD-146: <paramref name="Quantity"/> is decimal, not int — a half
/// portion is quantity 0,5, not a separate product or a negative-price
/// modifier. <c>orders.order_items.quantity</c> has always been
/// NUMERIC(18,3) and <see cref="ALKAROS.Orders.OrderAggregate.OrderItem"/>.
/// Quantity has always been decimal; only this contract and the read-side
/// projection were pinned to whole numbers.
/// </remarks>
public sealed record OrderItemDraftDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    IReadOnlyList<OrderItemModifierSelectionDto>? Modifiers = null,
    string? SpecialInstructions = null);

/// <summary>
/// V1-RMD-150: one chosen modifier and how many of it. <paramref name="Quantity"/>
/// is optional — left out, the server uses the ceiling of the line's own
/// quantity, so two portions carry two of the extra and half a portion still
/// carries one. The same number drives both the price and what the kitchen
/// ticket prints, so it is a real order input rather than a catalog setting:
/// whether a second plate wants the extra too is known when the order is
/// taken, not when the menu is configured.
/// </summary>
public sealed record OrderItemModifierSelectionDto(
    Guid ModifierId,
    decimal? Quantity = null);

/// <summary>
/// V1-RMD-147: one modifier as it was actually recorded on an order line.
/// Name and price delta are the catalog's values at order time, never the
/// client's — the same rule the product's own name and price already follow.
/// </summary>
public sealed record OrderItemModifierDto(
    Guid ModifierId,
    string Name,
    decimal PriceDelta,
    decimal Quantity = 1);

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

/// <summary>
/// V1-RMD-149: one order waiting for staff confirmation. Carries what a
/// waiter device needs to draw its banner directly, so the list and the live
/// SignalR announcement say the same thing without a follow-up call.
/// </summary>
public sealed record PendingOrderSummaryV1(
    Guid OrderId,
    Guid? TableId,
    string TableNumber,
    int ItemCount,
    decimal Total,
    DateTimeOffset CreatedAt);

/// <summary>
/// V1-ORD-006: which table the check is leaving. Sent explicitly rather than
/// read from the order so the caller and the server agree on what is being
/// released — a check that moved table since the screen was drawn releases
/// the table the waiter is actually looking at, or nothing.
/// </summary>
public sealed record SendCheckToCashierRequestV1(Guid TableId);

/// <summary>
/// V1-ORD-006: result of sending a check to the cashier.
/// <paramref name="AlreadySent"/> means the check had already left the table —
/// a double tap, or another device got there first. It is reported rather than
/// treated as an error: the outcome the caller wanted is already true.
/// </summary>
public sealed record SendCheckToCashierResultV1(Guid OrderId, Guid TableId, bool AlreadySent);

/// <summary>
/// V1-ORD-006: one check waiting to be settled at the till. Carries its own
/// number because by the time the guest reaches the cashier the table has
/// usually been re-seated, so the table number identifies nothing.
/// </summary>
public sealed record PendingCheckSummaryV1(
    Guid OrderId,
    string OrderNumber,
    string TableNumber,
    int ItemCount,
    decimal Total,
    DateTimeOffset CreatedAt);

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
/// <remarks>
/// V1-RMD-146: <paramref name="Status"/>, <paramref name="KitchenState"/> and
/// <paramref name="CreatedAt"/> come straight off the aggregate's own item and
/// used to be dropped by the projection. Without Status a cancelled line looks
/// identical to a live one; without KitchenState a waiter can only learn what
/// the kitchen is doing by querying the KDS ticket surface separately; without
/// CreatedAt there is no way to tell one round of items from the next.
/// </remarks>
public sealed record OrderItemDto(
    Guid ItemId,
    Guid ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    string? SpecialInstructions,
    decimal? AvailableStockQuantity = null,
    string Status = "",
    string KitchenState = "",
    DateTimeOffset CreatedAt = default,
    IReadOnlyList<OrderItemModifierDto>? Modifiers = null);
