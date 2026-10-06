using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.StockMaster;
using Npgsql;
using ALKAROS.Purchasing.Suppliers;

namespace ALKAROS.Purchasing.PurchaseInvoices;

public interface IPurchaseInvoiceService
{
    Task<PurchaseInvoice> ImportAsync(string xml, string importedBy, string source, CancellationToken ct = default);

    Task<PurchaseInvoice> GetAsync(Guid invoiceId, CancellationToken ct = default);

    Task<IReadOnlyList<PurchaseInvoiceSummary>> ListAsync(string? status, CancellationToken ct = default);

    /// <summary>Maps a draft line to a stock item and returns the invoice as it stands afterwards.</summary>
    Task<PurchaseInvoice> MapLineAsync(Guid invoiceId, Guid lineId, Guid stockItemId, decimal conversionFactor, CancellationToken ct = default);

}

public sealed class PurchaseInvoiceService : IPurchaseInvoiceService
{
    private readonly IPurchaseInvoiceRepository _invoices;
    private readonly ISupplierRepository _suppliers;
    private readonly IStockItemRepository _stockItems;

    public PurchaseInvoiceService(IPurchaseInvoiceRepository invoices, ISupplierRepository suppliers, IStockItemRepository stockItems)
    {
        _invoices = invoices ?? throw new ArgumentNullException(nameof(invoices));
        _suppliers = suppliers ?? throw new ArgumentNullException(nameof(suppliers));
        _stockItems = stockItems ?? throw new ArgumentNullException(nameof(stockItems));
    }

    public async Task<PurchaseInvoice> ImportAsync(string xml, string importedBy, string source, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(importedBy);
        var parsed = UblPurchaseInvoiceParser.Parse(xml);

        var supplier = await _suppliers.GetByTaxNumberAsync(parsed.SupplierTaxNumber, ct).ConfigureAwait(false);
        var remembered = (await _invoices.FindMappingsAsync(
            parsed.SupplierTaxNumber, parsed.Lines.Select(l => l.ItemKey).Distinct().ToArray(), ct).ConfigureAwait(false))
            .ToDictionary(m => (m.ItemKey, m.PurchaseUnitCode));

        var lines = parsed.Lines.Select(l =>
        {
            remembered.TryGetValue((l.ItemKey, l.UnitCode), out var mapping);
            return new PurchaseInvoiceLine(
                Guid.NewGuid(), l.LineNumber, l.ItemKey, l.SupplierItemCode, l.Description, l.Quantity, l.UnitCode, l.UnitPrice, l.LineNet,
                mapping?.StockItemId, mapping?.ConversionFactor);
        }).ToArray();

        var invoice = new PurchaseInvoice(
            Guid.NewGuid(), parsed.Ettn, parsed.InvoiceNumber, parsed.IssueDate, parsed.SupplierTaxNumber, parsed.SupplierName,
            supplier?.Id, parsed.Currency, source, PurchaseInvoiceStatuses.Draft, importedBy, DateTimeOffset.UtcNow, lines,
            parsed.Kind, parsed.ReferencedInvoiceNumber);
        await _invoices.InsertAsync(invoice, xml, ct).ConfigureAwait(false);
        return invoice;
    }

    public async Task<PurchaseInvoice> GetAsync(Guid invoiceId, CancellationToken ct = default)
        => await _invoices.GetAsync(invoiceId, ct).ConfigureAwait(false) ?? throw new PurchaseInvoiceNotFoundException(invoiceId);

    public Task<IReadOnlyList<PurchaseInvoiceSummary>> ListAsync(string? status, CancellationToken ct = default)
    {
        if (status is not null && status is not (PurchaseInvoiceStatuses.Draft or PurchaseInvoiceStatuses.Approved or PurchaseInvoiceStatuses.Rejected))
            throw new InvalidPurchaseInvoiceException($"Unknown status '{status}'.");
        return _invoices.ListAsync(status, ct);
    }

    public async Task<PurchaseInvoice> MapLineAsync(Guid invoiceId, Guid lineId, Guid stockItemId, decimal conversionFactor, CancellationToken ct = default)
    {
        if (conversionFactor <= 0)
            throw new InvalidPurchaseInvoiceException("The conversion factor must be positive.");
        if (await _stockItems.GetByIdAsync(stockItemId, ct).ConfigureAwait(false) is null)
            throw new InvalidPurchaseInvoiceException($"Stock item '{stockItemId}' was not found.");

        await _invoices.MapLineAsync(invoiceId, lineId, stockItemId, conversionFactor, ct).ConfigureAwait(false);
        return await GetAsync(invoiceId, ct).ConfigureAwait(false);
    }
}

public interface IPurchaseInvoiceApprovalService
{
    /// <summary>
    /// Turns a fully mapped draft into an order-less goods receipt and its stock effect in one transaction; returns the receipt id.
    /// Quantities and unit prices are converted to the stock item's tracking unit with each line's conversion factor.
    /// A return invoice instead takes its quantities out of stock at that location (all or nothing) and returns the invoice id.
    /// </summary>
    Task<Guid> ApproveAsync(Guid invoiceId, Guid locationId, string approvedBy, CancellationToken ct = default);

    Task RejectAsync(Guid invoiceId, CancellationToken ct = default);
}

public sealed class PurchaseInvoiceApprovalService : IPurchaseInvoiceApprovalService
{
    private readonly IPurchaseInvoiceRepository _invoices;
    private readonly ISupplierRepository _suppliers;
    private readonly IStockItemRepository _stockItems;
    private readonly NpgsqlDataSource _dataSource;
    private readonly IStockBalanceRepository _balances;
    private readonly IStockMovementRepository _movements;

    public PurchaseInvoiceApprovalService(
        IPurchaseInvoiceRepository invoices, ISupplierRepository suppliers, IStockItemRepository stockItems,
        NpgsqlDataSource dataSource, IStockBalanceRepository balances, IStockMovementRepository movements)
    {
        _invoices = invoices ?? throw new ArgumentNullException(nameof(invoices));
        _suppliers = suppliers ?? throw new ArgumentNullException(nameof(suppliers));
        _stockItems = stockItems ?? throw new ArgumentNullException(nameof(stockItems));
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _balances = balances ?? throw new ArgumentNullException(nameof(balances));
        _movements = movements ?? throw new ArgumentNullException(nameof(movements));
    }

    public async Task<Guid> ApproveAsync(Guid invoiceId, Guid locationId, string approvedBy, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approvedBy);
        var invoice = await _invoices.GetAsync(invoiceId, ct).ConfigureAwait(false) ?? throw new PurchaseInvoiceNotFoundException(invoiceId);
        if (invoice.Status != PurchaseInvoiceStatuses.Draft)
            throw new PurchaseInvoiceStatusException($"A {invoice.Status} purchase invoice cannot be approved.");
        var unmapped = invoice.Lines.Count(l => l.StockItemId is null);
        if (unmapped > 0)
            throw new PurchaseInvoiceNotReadyException($"{unmapped} invoice line(s) are not mapped to a stock item.");
        var supplierId = invoice.SupplierId
            ?? (await _suppliers.GetByTaxNumberAsync(invoice.SupplierTaxNumber, ct).ConfigureAwait(false))?.Id
            ?? throw new PurchaseInvoiceNotReadyException($"No supplier is registered for tax number '{invoice.SupplierTaxNumber}'.");

        var isReturn = invoice.Kind == PurchaseInvoiceKinds.Return;
        var receiptId = isReturn ? invoiceId : Guid.NewGuid();
        var receivedAt = new DateTimeOffset(invoice.IssueDate.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(3)).ToUniversalTime();
        var lines = new List<InvoiceReceiptLine>();
        foreach (var line in invoice.Lines)
        {
            var stockItem = await _stockItems.GetByIdAsync(line.StockItemId!.Value, ct).ConfigureAwait(false)
                ?? throw new PurchaseInvoiceNotReadyException($"Stock item '{line.StockItemId}' no longer exists.");
            var quantity = Math.Round(line.Quantity * line.ConversionFactor!.Value, 4, MidpointRounding.AwayFromZero);
            if (quantity <= 0)
                throw new PurchaseInvoiceNotReadyException($"Line {line.LineNumber} converts to no stock quantity.");
            lines.Add(new InvoiceReceiptLine(stockItem.Id, quantity, stockItem.TrackingUnitCode, Math.Round(line.LineNet / quantity, 4, MidpointRounding.AwayFromZero)));
        }

        var receipt = new InvoiceReceipt(
            receiptId, (isReturn ? "IADE-" : "FAT-") + invoiceId.ToString("N")[..12].ToUpperInvariant(), invoiceId, supplierId, locationId, receivedAt, approvedBy,
            $"Alış faturası {invoice.InvoiceNumber}", lines);

        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        if (!await _invoices.TryTransitionAsync(invoiceId, PurchaseInvoiceStatuses.Draft, PurchaseInvoiceStatuses.Approved, connection, transaction, ct).ConfigureAwait(false))
            throw new PurchaseInvoiceStatusException("The purchase invoice is no longer a draft.");
        if (!isReturn)
            await _invoices.InsertReceiptAsync(receipt, connection, transaction, ct).ConfigureAwait(false);
        foreach (var line in lines)
        {
            if (isReturn)
            {
                if (await _balances.TryApplyGuardedOnHandDeltaAsync(line.StockItemId, locationId, -line.Quantity, connection, transaction, ct).ConfigureAwait(false) is null)
                    throw new PurchaseInvoiceNotReadyException($"Not enough stock at the location to return {line.Quantity} {line.UnitCode} of stock item '{line.StockItemId}'.");
            }
            else
            {
                await _balances.ApplyOnHandDeltaAsync(line.StockItemId, locationId, line.Quantity, connection, transaction, ct).ConfigureAwait(false);
            }

            await _movements.AppendAsync(new StockMovement(
                id: Guid.NewGuid(), stockItemId: line.StockItemId, stockLocationId: locationId,
                movementType: isReturn ? StockMovementType.Return : StockMovementType.PurchaseReceipt,
                direction: isReturn ? MovementDirection.Out : MovementDirection.In, quantity: line.Quantity, unitCode: line.UnitCode,
                sourceType: isReturn ? StockMovementSourceType.Manual : StockMovementSourceType.GoodsReceipt,
                sourceReferenceId: receiptId, reason: receipt.ReceiptNumber, createdBy: null, createdAt: DateTimeOffset.UtcNow),
                connection, transaction, ct).ConfigureAwait(false);
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return receiptId;
    }

    public async Task RejectAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var invoice = await _invoices.GetAsync(invoiceId, ct).ConfigureAwait(false) ?? throw new PurchaseInvoiceNotFoundException(invoiceId);
        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        if (!await _invoices.TryTransitionAsync(invoiceId, PurchaseInvoiceStatuses.Draft, PurchaseInvoiceStatuses.Rejected, connection, transaction, ct).ConfigureAwait(false))
            throw new PurchaseInvoiceStatusException($"A {invoice.Status} purchase invoice cannot be rejected.");
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }
}
