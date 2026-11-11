namespace ALKAROS.Inventory.StockTransfers;

public sealed record StockTransferRequest(
    Guid StockItemId,
    Guid FromLocationId,
    Guid ToLocationId,
    decimal Quantity,
    string Reason,
    Guid AuthorizedBy,
    string IdempotencyKey);

/// <param name="TransferId">The source reference both ledger movements carry, derived from the idempotency key.</param>
/// <param name="WasReplayed">True when the key had already been applied; both quantities are then the balances now, not the ones right after the first transfer.</param>
public sealed record StockTransferResult(Guid TransferId, decimal FromOnHand, decimal ToOnHand, bool WasReplayed);

public sealed class InvalidStockTransferException(string message) : Exception(message);

public sealed class InsufficientStockForTransferException(string message) : Exception(message);

public sealed class StockTransferKeyReusedException(string message) : Exception(message);
