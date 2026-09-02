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
