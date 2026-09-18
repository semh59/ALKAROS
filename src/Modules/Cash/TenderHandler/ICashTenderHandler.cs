namespace ALKAROS.Cash.TenderHandler;

/// <summary>
/// Tenders cash against a Bill's remaining payable: creates a Payment,
/// PaymentAllocation and CashTransaction atomically (V13-CSH-003).
/// </summary>
public interface ICashTenderHandler
{
    Task<CashTenderResult> HandleAsync(CashTenderRequest request, CancellationToken cancellationToken = default);
}
