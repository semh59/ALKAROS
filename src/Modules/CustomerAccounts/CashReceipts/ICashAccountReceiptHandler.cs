namespace ALKAROS.CustomerAccounts.CashReceipts;

/// <summary>V14-ACC-005: takes a customer's cash payment towards their account into an open cash session.</summary>
public interface ICashAccountReceiptHandler
{
    Task<CashAccountReceiptResult> ReceiveAsync(CashAccountReceiptRequest request, CancellationToken cancellationToken = default);
}
