namespace ALKAROS.Invoicing.Generation;

public sealed class InvoiceGenerationSourceSetNotFoundException(Guid sourceSetId)
    : InvalidOperationException($"Invoice source set {sourceSetId} was not found.");

public sealed class InvoiceGenerationSourceSetCancelledException(Guid sourceSetId)
    : InvalidOperationException($"Invoice source set {sourceSetId} is cancelled; select the period again.");

public sealed class InvoiceGenerationNothingToInvoiceException(Guid sourceSetId)
    : InvalidOperationException($"Invoice source set {sourceSetId} has no charge to invoice.");

public sealed class InvoiceAlreadyGeneratedAsAnotherProfileException(Guid sourceSetId, InvoiceProfile existing)
    : InvalidOperationException($"Invoice source set {sourceSetId} was already invoiced as {existing}.")
{
    public InvoiceProfile ExistingProfile { get; } = existing;
}

public enum InvoiceBuyerProblem
{
    CustomerNotFound,
    CustomerAnonymized,
    NameMissing,
    TaxIdentityMissing,
}

/// <summary>The customer record cannot back an invoice: GIB needs the buyer's name and VKN or TCKN.</summary>
public sealed class InvoiceBuyerIncompleteException(Guid customerId, InvoiceBuyerProblem problem)
    : InvalidOperationException($"Customer {customerId} cannot be invoiced: {problem}.")
{
    public Guid CustomerId { get; } = customerId;

    public InvoiceBuyerProblem Problem { get; } = problem;
}

/// <summary>A charge whose bill (through its payment) or KDV breakdown cannot be found; nothing is generated.</summary>
public sealed class InvoiceChargeSourceUnresolvedException(Guid transactionId, string detail)
    : InvalidOperationException($"Charge {transactionId} cannot be split into tax groups: {detail}.")
{
    public Guid TransactionId { get; } = transactionId;
}
