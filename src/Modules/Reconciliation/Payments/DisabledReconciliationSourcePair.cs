namespace ALKAROS.Reconciliation.Payments;

/// <summary>
/// A source pair whose real detection logic cannot exist yet because one
/// side of it is owned by a task Semih has separately forbidden from
/// getting even a stub or schema while its own external contract is
/// missing (see <see cref="IReconciliationSourcePair"/>'s own doc comment).
/// Always returns an empty scan result and reports itself as disabled — it
/// is never silently omitted from the source-pair list, so a caller can
/// always see the full, honest set of what this task's Goal names, and
/// which of them are live today.
/// </summary>
public sealed class DisabledReconciliationSourcePair : IReconciliationSourcePair
{
    public DisabledReconciliationSourcePair(string name, string disabledReason)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(disabledReason))
            throw new ArgumentException("Disabled reason cannot be empty.", nameof(disabledReason));

        Name = name;
        DisabledReason = disabledReason;
    }

    public string Name { get; }
    public bool IsEnabled => false;
    public string? DisabledReason { get; }

    public Task<IReadOnlyList<DetectedDiscrepancy>> ScanAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DetectedDiscrepancy>>(Array.Empty<DetectedDiscrepancy>());
}
