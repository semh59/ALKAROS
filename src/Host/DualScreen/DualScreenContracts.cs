using System.Text.Json.Serialization;

namespace ALKAROS.Host.DualScreen;

public sealed record LoginRequest(string Username, string Password, Guid TerminalId);

public sealed record LoginResponse(Guid UserId, string DisplayName, Guid TerminalId);

public sealed record CatalogProductDto(
    Guid ProductId,
    string Sku,
    string Name,
    string CategoryCode,
    string CategoryName,
    decimal UnitPrice,
    decimal TaxRate);

public sealed record StartOrderResponse(Guid OrderId, string OrderNumber, long Revision);

public sealed record AddOrderItemRequest(Guid ProductId, decimal Quantity, long ExpectedRevision);

public sealed record ChangeOrderItemQuantityRequest(decimal Quantity, long ExpectedRevision);

public sealed record SubmitOrderRequest(string OperationId, long ExpectedRevision);

public sealed record PairingRequestCreate(Guid DisplayId);

public sealed record PairingRequestCreated(Guid RequestId, Guid DisplayId, string Secret, string Code, DateTimeOffset ExpiresAt);

public sealed record PairingApprovalRequest(string Code);

public sealed record PairingCompletionRequest(string Secret);

public sealed record PairingCompleted(Guid DisplayId, Guid TerminalId, DateTimeOffset ExpiresAt);

public sealed record CustomerDisplayLineDto(
    Guid ItemId,
    string Name,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record CustomerDisplaySnapshotDto(
    Guid DisplayId,
    Guid TerminalId,
    Guid? OrderId,
    long Revision,
    string State,
    bool Editable,
    string? OrderNumber,
    IReadOnlyList<CustomerDisplayLineDto> Lines,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TaxTotal,
    decimal Total,
    string Currency,
    DateTimeOffset ServerTimestamp,
    string Message);

public sealed record ApiErrorEnvelope([property: JsonPropertyName("error")] ApiError Error);

public sealed record ApiError(string Code, string Message, int Status, string TraceId);

public sealed record CashierPrincipal(Guid UserId, Guid TerminalId, Guid SessionId, string DisplayName);

public sealed record DisplayPrincipal(Guid DisplayId, Guid TerminalId, Guid SessionId, DateTimeOffset ExpiresAt);

public sealed record OrderMutationResult(Guid OrderId, long Revision);
