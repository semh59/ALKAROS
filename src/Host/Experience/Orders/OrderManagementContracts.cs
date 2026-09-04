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
