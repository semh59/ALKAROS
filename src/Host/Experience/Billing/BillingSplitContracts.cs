using System.Text.Json.Serialization;

namespace ALKAROS.Host.Experience.Billing;

public sealed record AllocationVersionRequest(Guid AllocationId, long? RowVersion);

public sealed record SplitOwnerRequest(string Kind, Guid OwnerId);

public sealed record SaveEqualSplitRequest(
    long? ExpectedBillRowVersion,
    IReadOnlyList<AllocationVersionRequest> ExpectedAllocations,
    IReadOnlyList<SplitOwnerRequest> Owners);

public sealed record AmountSplitTargetRequest(SplitOwnerRequest Owner, decimal Amount);

public sealed record SaveAmountSplitRequest(
    long? ExpectedBillRowVersion,
    IReadOnlyList<AllocationVersionRequest> ExpectedAllocations,
    IReadOnlyList<AmountSplitTargetRequest> Targets);

public sealed record ItemSplitTargetRequest(
    SplitOwnerRequest Owner,
    Guid BillItemId,
    decimal Quantity);

public sealed record SaveItemSplitRequest(
    long? ExpectedBillRowVersion,
    IReadOnlyList<AllocationVersionRequest> ExpectedAllocations,
    IReadOnlyList<ItemSplitTargetRequest> Targets);

public sealed record CustomSplitTargetRequest(
    SplitOwnerRequest Owner,
    decimal Amount,
    Guid? BillItemId,
    decimal? Quantity);

public sealed record SaveCustomSplitRequest(
    long? ExpectedBillRowVersion,
    IReadOnlyList<AllocationVersionRequest> ExpectedAllocations,
    IReadOnlyList<CustomSplitTargetRequest> Targets);

public sealed record ClearSplitDesignRequest(
    long? ExpectedBillRowVersion,
    IReadOnlyList<AllocationVersionRequest> ExpectedAllocations);

public sealed record BillSplitItemDto(
    Guid BillItemId,
    string ProductName,
    decimal Quantity,
    decimal GrossAmount,
    decimal TaxAmount,
    long RowVersion);

public sealed record BillSplitAllocationDto(
    Guid AllocationId,
    string Mode,
    string OwnerKind,
    Guid? OwnerId,
    string? LegacyOwnerReference,
    Guid? BillItemId,
    decimal? Quantity,
    decimal Amount,
    decimal TaxAmount,
    long RowVersion);

public sealed record BillSplitOwnerOptionDto(
    string Kind,
    Guid Id,
    string Label,
    string? SecondaryLabel);

public sealed record BillSplitDesignDto(
    Guid BillId,
    string BillNumber,
    string BillStatus,
    string CurrencyCode,
    decimal PayableAmount,
    decimal TaxTotal,
    long BillRowVersion,
    string Mode,
    string ExecutionState,
    IReadOnlyList<string> AllowedCommands,
    IReadOnlyList<BillSplitItemDto> Items,
    IReadOnlyList<BillSplitAllocationDto> Allocations);

public sealed record BillingSplitErrorEnvelope(
    [property: JsonPropertyName("error")] BillingSplitError Error);

public sealed record BillingSplitError(
    string Code,
    string Message,
    int Status,
    string TraceId,
    BillingSplitConflict? Conflict = null);

public sealed record BillingSplitConflict(
    string Resource,
    Guid ResourceId,
    long? ExpectedRowVersion,
    long? ActualRowVersion);

/// <summary>
/// V1-RMD-103 (B1): applies a bill-level discount via the existing
/// bills.discount grant-class permission. CalculationType is "Percentage"
/// (Value 0-100) or "FixedAmount" (Value is a currency amount).
/// </summary>
public sealed record ApplyBillDiscountRequestV1(
    string IdempotencyKey,
    string CalculationType,
    decimal Value,
    string ReasonCode,
    string? Notes = null);

/// <summary>
/// "Applied" carries Adjustment/Summary; "Pending" carries only GrantId —
/// mirrors <c>ApplyComplimentaryResultV1</c>'s Applied/Pending shape.
/// </summary>
public sealed record ApplyBillDiscountResultV1(
    string Status,
    Guid BillId,
    Guid? AdjustmentId,
    AdjustedBillSummaryV1? Summary,
    Guid? GrantId);

/// <summary>
/// V1-WTR-020: records a voluntary tip. No ReasonCode catalog (unlike
/// discount) — a tip needs no business justification, only the amount the
/// customer actually gave. No escalation path either: unlike bills.discount/
/// bills.comp, recording a tip already received is not a discretionary
/// business decision a role might lack the authority for, so bills.split
/// (already held outright by cashier and up) is checked directly.
/// </summary>
public sealed record ApplyBillTipRequestV1(
    string IdempotencyKey,
    decimal Amount,
    string? Notes = null);

public sealed record ApplyBillTipResultV1(
    Guid BillId,
    Guid AdjustmentId,
    AdjustedBillSummaryV1 Summary);

public sealed record AdjustedBillSummaryV1(
    decimal OriginalPayableAmount,
    decimal TotalDiscounts,
    decimal TotalFees,
    decimal TotalTips,
    decimal AdjustedPayableAmount);

public sealed record BillAdjustmentDto(
    Guid AdjustmentId,
    string AdjustmentType,
    string CalculationType,
    decimal? Rate,
    decimal Amount,
    bool IsDeduction,
    string Reason,
    string? Notes,
    DateTimeOffset CreatedAt);
