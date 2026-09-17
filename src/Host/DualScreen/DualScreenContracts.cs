using System.Text.Json.Serialization;

namespace ALKAROS.Host.DualScreen;

public sealed record LoginRequest(string Username, string Password, Guid TerminalId);

/// <summary>
/// V1-RMD-151: unlocking a device that sat idle. Carries no username — the
/// session already says who this is; the PIN only proves the same person is
/// still holding it.
/// </summary>
public sealed record UnlockRequest(string Pin);

/// <summary>
/// V1-RMD-151: setting or clearing one's own unlock PIN. A null
/// <paramref name="Pin"/> removes it, which is how a user turns the PIN
/// prompt back off. <paramref name="CurrentPassword"/> is required even
/// though a session exists — an unlocked device left on a table must not be
/// enough to plant a PIN on that account.
/// </summary>
public sealed record SetPinRequest(string CurrentPassword, string? Pin);

public sealed record LoginResponse(Guid UserId, string DisplayName, Guid TerminalId);

/// <remarks>
/// V1-RMD-148: <paramref name="ModifierGroups"/> is what lets a client ask
/// "half portion or extra rice?" at all. The set listed here is exactly the
/// set V1-RMD-147's order path accepts — a modifier reaches a product either
/// directly or through a group assigned to it, and inactive ones appear in
/// neither. Null when the product has no options.
/// </remarks>
public sealed record CatalogProductDto(
    Guid ProductId,
    string Sku,
    string Name,
    string CategoryCode,
    string CategoryName,
    decimal UnitPrice,
    decimal TaxRate,
    IReadOnlyList<CatalogModifierGroupDto>? ModifierGroups = null,
    // V1-WTR-017: manager-entered estimated prep time in minutes, null when
    // never set on the product. Lets a waiter client warn before sending a
    // round whose items' prep times are far apart.
    int? PrepTimeMinutes = null,
    // V1-WTR-054: how many more units the mapped stock can still cover, null
    // when the product carries no stock mapping (unlimited). A product whose
    // computed count would be zero or less never reaches this DTO at all —
    // GetCatalogAsync's query drops it, the same way an unavailable product
    // already is.
    int? RemainingCount = null);

/// <summary>
/// V1-RMD-148: one option group of a product. <paramref name="SelectionType"/>
/// is "Single" or "Multiple" (catalog.modifier_groups.selection_type 1 or 2);
/// the selection bounds are reported so a client can require a mandatory
/// group, but the server does not enforce them yet.
/// </summary>
public sealed record CatalogModifierGroupDto(
    Guid ModifierGroupId,
    string Code,
    string Name,
    string SelectionType,
    int MinSelections,
    int MaxSelections,
    IReadOnlyList<CatalogModifierDto> Modifiers);

/// <summary>V1-RMD-148: one selectable option and what it adds to the line.</summary>
public sealed record CatalogModifierDto(
    Guid ModifierId,
    string Code,
    string Name,
    decimal PriceDelta);

public sealed record CatalogPage(IReadOnlyList<CatalogProductDto> Items, string? NextCursor);

/// <summary>
/// V1-WTR-018: one line of a guest's own read-only live bill — the QR
/// live-bill idea. Name-only (no product/order-item ids): this DTO
/// carries nothing a guest could use to target a mutation, mirroring
/// CatalogProductDto's own "read model, not a write surface" shape.
/// </summary>
public sealed record QrLiveBillLineDto(string Name, decimal Quantity, decimal UnitPrice, decimal LineTotal);

/// <summary>
/// V1-WTR-018: a table's live, running tab, exactly as the guest's own QR
/// session sees it — no ids beyond what a poll needs to detect a change.
/// <paramref name="HasActiveOrder"/> is false whenever there is nothing to
/// show yet (no order placed, or the table's check already closed/paid);
/// every other field is then empty/zero rather than null, so the guest
/// page never has to special-case a partially-populated shape.
/// </summary>
public sealed record QrLiveBillDto(
    bool HasActiveOrder,
    IReadOnlyList<QrLiveBillLineDto> Lines,
    decimal Subtotal,
    decimal TaxTotal,
    decimal Total,
    long Revision)
{
    public static readonly QrLiveBillDto Empty = new(false, [], 0m, 0m, 0m, 0);
}

public sealed record StartOrderRequest(Guid? TableId = null, long? ExpectedTableRowVersion = null);

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
