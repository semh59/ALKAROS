namespace ALKAROS.Billing.SplitDesign;

public enum AllocationOwnerKind
{
    Person,
    Seat,
}

public sealed record AllocationVersion(Guid AllocationId, long RowVersion);

public sealed record OperationalSplitSaveResult(
    long BillRowVersion,
    IReadOnlyList<BillAllocation> Allocations);

public sealed record OperationalAllocationOwner(
    SplitMode Mode,
    AllocationOwnerKind Kind,
    Guid Id);

public sealed record CustomSplitTarget(
    string OwnerReference,
    decimal Amount,
    Guid? BillItemId = null,
    decimal? Quantity = null);

public static class OperationalOwnerReference
{
    public static string Format(SplitMode mode, AllocationOwnerKind kind, Guid id)
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (id == Guid.Empty)
            throw new ArgumentException("Owner ID cannot be empty.", nameof(id));

        return $"{mode}|{kind}:{id:D}";
    }

    public static bool TryParse(string value, out OperationalAllocationOwner? owner)
    {
        owner = null;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var modeSeparator = value.IndexOf('|');
        var kindSeparator = value.IndexOf(':', modeSeparator + 1);
        if (modeSeparator <= 0 || kindSeparator <= modeSeparator + 1)
            return false;

        if (!Enum.TryParse<SplitMode>(value[..modeSeparator], false, out var mode)
            || !Enum.IsDefined(mode)
            || !Enum.TryParse<AllocationOwnerKind>(
                value[(modeSeparator + 1)..kindSeparator],
                false,
                out var kind)
            || !Enum.IsDefined(kind)
            || !Guid.TryParseExact(value[(kindSeparator + 1)..], "D", out var id)
            || id == Guid.Empty)
        {
            return false;
        }

        owner = new OperationalAllocationOwner(mode, kind, id);
        return true;
    }
}

public sealed class SplitDesignConcurrencyException : Exception
{
    public SplitDesignConcurrencyException(string resource, Guid id, long? expected, long? actual)
        : base($"Concurrency conflict on {resource} {id}.")
    {
        Resource = resource;
        Id = id;
        Expected = expected;
        Actual = actual;
    }

    public string Resource { get; }

    public Guid Id { get; }

    public long? Expected { get; }

    public long? Actual { get; }
}

public sealed class SplitDesignUnsupportedBillStateException : Exception
{
    public SplitDesignUnsupportedBillStateException(Guid billId, string state)
        : base($"Bill {billId} in state {state} does not support split-design changes.")
    {
    }
}
