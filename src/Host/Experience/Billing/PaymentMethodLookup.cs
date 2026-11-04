using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.Experience.Billing;

/// <summary>
/// The way an approved payment was taken, read from the records each way leaves behind. One definition for the
/// refund flow and the receipt.
/// </summary>
public static class PaymentMethodLookup
{
    public const string Account = "Account";
    public const string Cash = "Cash";
    public const string Card = "Card";
    public const string Eft = "Eft";

    /// <summary>
    /// A SQL CASE over the payment id expression <paramref name="paymentId"/>. Transfer is what is left because the
    /// producers of approved payments are a closed set (the cash, account, card and transfer handlers and the manual
    /// card confirmation) and the meal card has no handler.
    /// </summary>
    public static string Sql(string paymentId) =>
        $"""
        CASE
            WHEN EXISTS (SELECT 1 FROM customer_account.account_transactions a
                         WHERE a.source_reference_type = 'Payment' AND a.source_reference_id = {paymentId}
                           AND a.transaction_type = 'Charge') THEN '{Account}'
            WHEN EXISTS (SELECT 1 FROM cash.cash_transactions c
                         WHERE c.related_payment_id = {paymentId} AND c.type = 'Sale') THEN '{Cash}'
            WHEN EXISTS (SELECT 1 FROM payments.card_settlement_attempts s
                         WHERE s.payment_id = {paymentId} AND s.outcome = 'Approved')
              OR EXISTS (SELECT 1 FROM payments.manual_card_confirmations m
                         WHERE m.payment_id = {paymentId} AND m.status = 'Approved') THEN '{Card}'
            ELSE '{Eft}' END
        """;

    public static async Task<string> ForPaymentAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid paymentId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"SELECT {Sql("@id")};", connection, transaction);
        command.Parameters.Add("id", NpgsqlDbType.Uuid).Value = paymentId;
        return (string)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public static async Task<Dictionary<Guid, string>> ForPaymentsAsync(
        NpgsqlDataSource dataSource, Guid[] paymentIds, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, string>();
        if (paymentIds.Length == 0)
            return result;

        await using var command = dataSource.CreateCommand($"SELECT p.id, {Sql("p.id")} FROM unnest(@ids) AS p(id);");
        command.Parameters.Add("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = paymentIds;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result[reader.GetGuid(0)] = reader.GetString(1);
        return result;
    }
}
