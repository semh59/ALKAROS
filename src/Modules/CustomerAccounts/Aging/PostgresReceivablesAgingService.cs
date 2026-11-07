using ALKAROS.CustomerAccounts.CreditTerms;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.CustomerAccounts.Aging;

/// <summary>
/// Ages every customer who owes or is owed anything, in the database. Payments settle the oldest charges first - the
/// same rule the credit policy uses to decide what is overdue - and a payment beyond all open debt is kept as credit
/// that settles the next charge to arrive.
/// </summary>
/// <remarks>
/// With S the running signed ledger sum, the debt is max(0, S), so a charge adds max(0, S) - max(0, S - charge) to the
/// queue of open amounts; the payments have removed the first (queued total - final debt) of that queue, which leaves
/// each charge's open amount as the part of its share beyond that removed head.
/// </remarks>
public sealed class PostgresReceivablesAgingService : IReceivablesAgingService
{
    /// <summary>A report that outgrows this fails loud instead of silently dropping customers.</summary>
    public const int MaxCustomers = 100_000;

    private static readonly int[] BucketDays = [30, 60, 90];

    /// <summary>The payment terms a customer can have (1..MaxPaymentTermDays days), each with its cutoff, so the terms need no separate read.</summary>
    private static readonly int[] TermDays = Enumerable.Range(1, CustomerCreditTerms.MaxPaymentTermDays).ToArray();

    private readonly NpgsqlDataSource _dataSource;

    public PostgresReceivablesAgingService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<IReadOnlyList<CustomerAging>> GetAsync(
        DateOnly asOf, BusinessCalendar calendar, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        // A customer whose ledger nets to zero but whose cached balance does not is included, so the report can flag it.
        await using var command = _dataSource.CreateCommand(
            """
            WITH effects AS (
                SELECT customer_id, occurred_at, id,
                       CASE WHEN transaction_type = 'Adjustment' OR direction = 'Debit' THEN amount ELSE -amount END AS effect
                FROM customer_account.account_transactions
            ),
            relevant AS (
                SELECT customer_id FROM effects GROUP BY customer_id HAVING SUM(effect) <> 0
                UNION
                SELECT customer_id FROM customer_account.balances WHERE current_balance <> 0
            ),
            running AS (
                SELECT e.customer_id, e.occurred_at, e.id, e.effect,
                       SUM(e.effect) OVER (PARTITION BY e.customer_id ORDER BY e.occurred_at, e.id) AS running_sum,
                       SUM(e.effect) OVER (PARTITION BY e.customer_id) AS ledger_sum
                FROM effects e
                JOIN relevant r ON r.customer_id = e.customer_id
            ),
            queued AS (
                SELECT *,
                       CASE WHEN effect > 0 THEN GREATEST(running_sum, 0) - GREATEST(running_sum - effect, 0) ELSE 0 END AS added
                FROM running
            ),
            cumulative AS (
                SELECT *,
                       SUM(added) OVER (PARTITION BY customer_id ORDER BY occurred_at, id) AS added_to_date,
                       SUM(added) OVER (PARTITION BY customer_id) AS added_total
                FROM queued
            ),
            open_charges AS (
                SELECT customer_id, occurred_at, ledger_sum,
                       GREATEST(0, LEAST(added, added_to_date - (added_total - GREATEST(ledger_sum, 0)))) AS open_amount
                FROM cumulative
            )
            SELECT o.customer_id,
                   COALESCE(SUM(o.open_amount) FILTER (WHERE o.occurred_at >= @first_cutoff), 0),
                   COALESCE(SUM(o.open_amount) FILTER (WHERE o.occurred_at < @first_cutoff AND o.occurred_at >= @second_cutoff), 0),
                   COALESCE(SUM(o.open_amount) FILTER (WHERE o.occurred_at < @second_cutoff AND o.occurred_at >= @third_cutoff), 0),
                   COALESCE(SUM(o.open_amount) FILTER (WHERE o.occurred_at < @third_cutoff), 0),
                   GREATEST(-MAX(o.ledger_sum), 0),
                   COALESCE(MAX(b.current_balance), 0),
                   MAX(t.payment_term_days),
                   SUM(o.open_amount) FILTER (WHERE o.occurred_at < due.cutoff)
            FROM open_charges o
            LEFT JOIN customer_account.balances b ON b.customer_id = o.customer_id
            LEFT JOIN customer_account.credit_terms t ON t.customer_id = o.customer_id
            LEFT JOIN unnest(@term_days, @term_cutoffs) AS due(days, cutoff) ON due.days = t.payment_term_days
            GROUP BY o.customer_id
            LIMIT @limit;
            """);
        command.Parameters.Add("limit", NpgsqlDbType.Integer).Value = MaxCustomers + 1;
        var cutoffs = BucketDays.Select(days => calendar.StartOf(asOf.AddDays(-days)).ToUniversalTime()).ToArray();
        command.Parameters.Add("first_cutoff", NpgsqlDbType.TimestampTz).Value = cutoffs[0];
        command.Parameters.Add("second_cutoff", NpgsqlDbType.TimestampTz).Value = cutoffs[1];
        command.Parameters.Add("third_cutoff", NpgsqlDbType.TimestampTz).Value = cutoffs[2];
        command.Parameters.Add("term_days", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = TermDays;
        command.Parameters.Add("term_cutoffs", NpgsqlDbType.Array | NpgsqlDbType.TimestampTz).Value =
            TermDays.Select(days => calendar.StartOf(asOf.AddDays(-days)).ToUniversalTime()).ToArray();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var result = new List<CustomerAging>();
        while (await reader.ReadAsync(cancellationToken))
        {
            if (result.Count == MaxCustomers)
                throw new InvalidOperationException($"More than {MaxCustomers} customers have an open balance; the aging report must be paged.");
            result.Add(new CustomerAging(
                reader.GetFieldValue<Guid>(0),
                reader.GetFieldValue<decimal>(1),
                reader.GetFieldValue<decimal>(2),
                reader.GetFieldValue<decimal>(3),
                reader.GetFieldValue<decimal>(4),
                reader.GetFieldValue<decimal>(5),
                reader.GetFieldValue<decimal>(6),
                reader.IsDBNull(7) ? null : reader.GetFieldValue<int>(7),
                reader.IsDBNull(7) ? null : reader.IsDBNull(8) ? 0m : reader.GetFieldValue<decimal>(8)));
        }

        return result.OrderByDescending(aging => aging.TotalDebt).ThenBy(aging => aging.CustomerId).ToArray();
    }
}
