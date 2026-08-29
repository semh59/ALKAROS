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
