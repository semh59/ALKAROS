namespace ALKAROS.Invoicing.SourceSelection;

public sealed class InvoicePeriodRangeInvalidException(DateOnly periodStart, DateOnly periodEnd)
    : InvalidOperationException($"Invoice period start {periodStart:yyyy-MM-dd} must be before its end {periodEnd:yyyy-MM-dd}.");

public sealed class InvoicePeriodNotEndedException(DateOnly periodEnd)
    : InvalidOperationException($"Invoice period ending {periodEnd:yyyy-MM-dd} has not ended yet and cannot be closed.");

public sealed class InvoicePeriodOverlapException(DateOnly periodStart, DateOnly periodEnd)
    : InvalidOperationException(
        $"Invoice period {periodStart:yyyy-MM-dd}..{periodEnd:yyyy-MM-dd} overlaps an already closed period.");

public sealed class InvoicePeriodNotFoundException(Guid periodId)
    : InvalidOperationException($"Invoice period {periodId} was not found.");

public sealed class NoInvoiceableTransactionsException(Guid periodId, Guid customerId)
    : InvalidOperationException($"Customer {customerId} has no uninvoiced account transactions in period {periodId}.");

public sealed class InvoiceSourceSetNotFoundException(Guid sourceSetId)
    : InvalidOperationException($"Invoice source set {sourceSetId} was not found.");

public sealed class InvoiceSourceSetAlreadyCancelledException(Guid sourceSetId)
    : InvalidOperationException($"Invoice source set {sourceSetId} is already cancelled.");

public sealed class InvoiceSourceSetTooLargeException(Guid periodId, Guid customerId, int limit)
    : InvalidOperationException(
        $"Customer {customerId} has more than {limit} uninvoiced transactions in period {periodId}; split the period.");
