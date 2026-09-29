namespace ALKAROS.Invoicing.SourceSelection;

/// <summary>
/// V14-INV-001: closes invoice periods and locks each customer's uninvoiced account transactions into a source set.
/// Reads customer_account.account_transactions and never writes it, so no balance changes.
/// </summary>
public interface IInvoiceSourceSelectionService
{
    /// <summary>
    /// Closes the period [<paramref name="periodStart"/>, <paramref name="periodEnd"/>) and records who closed it and
    /// when. Closing the same range again returns the recorded period; a range that overlaps another closed period or
    /// has not ended yet (Istanbul today) is refused.
    /// </summary>
    Task<InvoicePeriod> ClosePeriodAsync(
        DateOnly periodStart, DateOnly periodEnd, Guid closedBy, CancellationToken cancellationToken = default);

    Task<InvoicePeriod?> GetPeriodAsync(Guid periodId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Locks the customer's uninvoiced transactions of the closed period (every type but Invoice) into a source set.
    /// A rerun returns the same live set unchanged, even if transactions were recorded since; a customer with nothing
    /// to invoice is refused.
    /// </summary>
    Task<InvoiceSourceSelectionResult> SelectAsync(
        Guid periodId, Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>Selects every customer of the period with uninvoiced transactions (at most 500 per call).</summary>
    Task<IReadOnlyList<InvoiceSourceSelectionResult>> SelectAllAsync(
        Guid periodId, CancellationToken cancellationToken = default);

    Task<InvoiceSourceSet?> GetSourceSetAsync(Guid sourceSetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a live source set; its transactions become selectable again. The set and its lines stay recorded.
    /// </summary>
    Task CancelAsync(Guid sourceSetId, Guid cancelledBy, string reason, CancellationToken cancellationToken = default);
}
