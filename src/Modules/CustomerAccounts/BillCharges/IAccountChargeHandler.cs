namespace ALKAROS.CustomerAccounts.BillCharges;

public interface IAccountChargeHandler
{
    Task<AccountChargeResult> HandleAsync(AccountChargeRequest request, CancellationToken cancellationToken = default);
}
