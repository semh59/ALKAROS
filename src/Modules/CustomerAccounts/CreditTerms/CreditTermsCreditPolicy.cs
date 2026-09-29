using System.Globalization;
using ALKAROS.CustomerAccounts.BalanceProjection;
using ALKAROS.CustomerAccounts.BillCharges;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.CustomerAccounts.CreditTerms;

/// <summary>
/// V1-RMD-440 (PO 2026-09-29): the production credit policy. A charge is refused - never escalated - when
/// <list type="bullet">
/// <item>the customer has overdue debt: with a payment term set, the charges older than the term are not yet
/// covered by everything credited to the account (payments settle the oldest charges first), or</item>
/// <item>the current balance plus the new charge would exceed the customer's credit limit (0 when none is set).</item>
/// </list>
/// <see cref="AccountChargeHandler"/> evaluates it under a per-customer advisory lock, so two concurrent charges
/// cannot both pass against the same balance.
/// </summary>
public sealed class CreditTermsCreditPolicy : ICustomerCreditPolicy
{
    public const string NoLimitReason = "Müşteri için kredi limiti tanımlanmadığından cari hesaba borç yazılamaz.";

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    private readonly ICustomerCreditTermsStore _terms;
    private readonly IAccountBalanceProjection _balances;
    private readonly NpgsqlDataSource _dataSource;

    public CreditTermsCreditPolicy(ICustomerCreditTermsStore terms, IAccountBalanceProjection balances, NpgsqlDataSource dataSource)
    {
        _terms = terms ?? throw new ArgumentNullException(nameof(terms));
        _balances = balances ?? throw new ArgumentNullException(nameof(balances));
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<CreditPolicyResult> EvaluateAsync(Guid customerId, decimal amount, CancellationToken cancellationToken)
    {
        var terms = await _terms.GetAsync(customerId, cancellationToken) ?? CustomerCreditTerms.None(customerId);
        if (terms.CreditLimit == 0m)
            return new CreditPolicyResult(false, NoLimitReason);

        if (terms.PaymentTermDays is { } termDays)
        {
            var overdue = await OverdueAmountAsync(customerId, DateTimeOffset.UtcNow.AddDays(-termDays), cancellationToken);
            if (overdue > 0m)
            {
                return new CreditPolicyResult(false,
                    $"Müşterinin {termDays} günlük vadesi geçmiş {Money(overdue)} TL cari borcu var; ödenmeden yeni borç yazılamaz.");
            }
        }

        var balance = (await _balances.GetBalanceAsync(customerId, cancellationToken))?.CurrentBalance ?? 0m;
        if (balance + amount > terms.CreditLimit)
        {
            return new CreditPolicyResult(false,
                $"Cari borç kredi limitini aşıyor: bakiye {Money(balance)} TL, yeni borç {Money(amount)} TL, limit {Money(terms.CreditLimit)} TL.");
        }

        return new CreditPolicyResult(true, null);
    }

    /// <summary>
    /// What is still unpaid of the charges that occurred before <paramref name="cutoff"/>, applying every credit on
    /// the account to the oldest debits first.
    /// </summary>
    private async Task<decimal> OverdueAmountAsync(Guid customerId, DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT
                COALESCE(SUM(amount) FILTER (WHERE direction = 'Debit' AND occurred_at < @cutoff), 0),
                COALESCE(SUM(abs(amount)) FILTER (WHERE direction = 'Credit'), 0)
            FROM customer_account.account_transactions
            WHERE customer_id = @customer_id;
            """);
        command.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = customerId;
        command.Parameters.Add("cutoff", NpgsqlDbType.TimestampTz).Value = cutoff.UtcDateTime;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return Math.Max(0m, reader.GetDecimal(0) - reader.GetDecimal(1));
    }

    private static string Money(decimal value) => value.ToString("N2", Turkish);
}
