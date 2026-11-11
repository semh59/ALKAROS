namespace ALKAROS.Inventory.StockTransfers;

public interface IStockTransferService
{
    /// <summary>Moves stock of one item between two locations as an Out and an In ledger movement in one transaction; a repeated key is answered with the first transfer.</summary>
    Task<StockTransferResult> TransferAsync(StockTransferRequest request, CancellationToken ct = default);
}
