using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.CustomerAccounts.CreditTerms;

/// <summary>Postgres-backed <see cref="ICustomerCreditTermsStore"/> (migration 164).</summary>
public sealed class PostgresCustomerCreditTermsStore : ICustomerCreditTermsStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresCustomerCreditTermsStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<CustomerCreditTerms?> GetAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT t.credit_limit, t.payment_term_days, t.updated_at, t.updated_by
            FROM customer_data.profiles p
            LEFT JOIN customer_account.credit_terms t ON t.customer_id = p.customer_id
            WHERE p.customer_id = @customer_id AND NOT p.anonymized;
            """);
        command.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = customerId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;
        if (await reader.IsDBNullAsync(0, cancellationToken))
            return CustomerCreditTerms.None(customerId);

        return new CustomerCreditTerms(
            customerId,
            reader.GetDecimal(0),
            await reader.IsDBNullAsync(1, cancellationToken) ? null : reader.GetInt32(1),
            reader.GetFieldValue<DateTimeOffset>(2),
            reader.GetGuid(3));
    }

    public async Task<CustomerCreditTerms?> SetAsync(
        Guid customerId, decimal creditLimit, int? paymentTermDays, Guid updatedBy, CancellationToken cancellationToken = default)
    {
        CustomerCreditTerms.Validate(creditLimit, paymentTermDays);

        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO customer_account.credit_terms (customer_id, credit_limit, payment_term_days, updated_at, updated_by)
            SELECT p.customer_id, @credit_limit, @payment_term_days, now(), @updated_by
            FROM customer_data.profiles p
            WHERE p.customer_id = @customer_id AND NOT p.anonymized
            ON CONFLICT (customer_id) DO UPDATE
                SET credit_limit = EXCLUDED.credit_limit,
                    payment_term_days = EXCLUDED.payment_term_days,
                    updated_at = EXCLUDED.updated_at,
                    updated_by = EXCLUDED.updated_by
            RETURNING credit_limit, payment_term_days, updated_at, updated_by;
            """);
        command.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = customerId;
        command.Parameters.Add("credit_limit", NpgsqlDbType.Numeric).Value = creditLimit;
        command.Parameters.Add("payment_term_days", NpgsqlDbType.Integer).Value = (object?)paymentTermDays ?? DBNull.Value;
        command.Parameters.Add("updated_by", NpgsqlDbType.Uuid).Value = updatedBy;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new CustomerCreditTerms(
            customerId,
            reader.GetDecimal(0),
            await reader.IsDBNullAsync(1, cancellationToken) ? null : reader.GetInt32(1),
            reader.GetFieldValue<DateTimeOffset>(2),
            reader.GetGuid(3));
    }
}
