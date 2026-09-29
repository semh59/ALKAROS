using System.Globalization;
using ALKAROS.CustomerAccounts.BalanceProjection;
using ALKAROS.CustomerData.AnonymizationState;

namespace ALKAROS.CustomerAccounts.Retention;

/// <summary>
/// V1-RMD-435 (V1-RMD-393 F-13): a customer whose receivable account (V14-ACC-001/002) still carries a balance is
/// not anonymized - either side of that balance is a financial record the venue has to keep tied to a real person.
/// The request is stored as RetentionBlocked with this guard's reason, and
/// <see cref="CustomerAnonymizationService.ReevaluateAsync"/> opens it once the balance reaches zero. A customer with
/// no account activity at all has no balance row and is never blocked here.
/// </summary>
public sealed class OutstandingBalanceRetentionGuard : IAnonymizationRetentionGuard
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    private readonly IAccountBalanceProjection _balances;

    public OutstandingBalanceRetentionGuard(IAccountBalanceProjection balances)
    {
        _balances = balances ?? throw new ArgumentNullException(nameof(balances));
    }

    public async Task<string?> CheckAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var balance = await _balances.GetBalanceAsync(customerId, cancellationToken);
        if (balance is null || balance.CurrentBalance == 0m)
            return null;

        var amount = Math.Abs(balance.CurrentBalance).ToString("N2", Turkish);
        return balance.CurrentBalance > 0m
            ? $"Müşterinin kapanmamış cari borcu var ({amount} TL); borç kapanmadan anonimleştirilemez."
            : $"Müşterinin cari hesabında alacağı var ({amount} TL); iade edilmeden anonimleştirilemez.";
    }
}
