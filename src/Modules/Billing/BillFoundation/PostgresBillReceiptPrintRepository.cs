using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Billing.BillFoundation;

public sealed class PostgresBillReceiptPrintRepository : IBillReceiptPrintRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresBillReceiptPrintRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<DateTimeOffset?> FindOriginalPrintedAtAsync(Guid billId, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand("SELECT receipt_first_printed_at FROM billing.bills WHERE bill_id = @bill LIMIT 1;");
        command.Parameters.Add("bill", NpgsqlDbType.Uuid).Value = billId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) && !reader.IsDBNull(0) ? reader.GetFieldValue<DateTimeOffset>(0) : null;
    }

    public async Task<ReceiptPrintClaim> ClaimOriginalAsync(
        Guid billId, Guid userId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        string status;
        DateTimeOffset? printedAt;
        await using (var locked = new NpgsqlCommand(
            "SELECT status, receipt_first_printed_at FROM billing.bills WHERE bill_id = @bill FOR NO KEY UPDATE;", connection, transaction))
        {
            locked.Parameters.Add("bill", NpgsqlDbType.Uuid).Value = billId;
            await using var reader = await locked.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return new ReceiptPrintClaim(ReceiptPrintOutcome.NotFound, null);
            status = reader.GetString(0);
            printedAt = reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1);
        }

        if (status != nameof(BillState.Paid))
            return new ReceiptPrintClaim(ReceiptPrintOutcome.NotPaid, null);
        if (printedAt is { } original)
            return new ReceiptPrintClaim(ReceiptPrintOutcome.Copy, original);

        await using var mark = new NpgsqlCommand(
            """
            UPDATE billing.bills SET receipt_first_printed_at = now(), receipt_first_printed_by = @user
            WHERE bill_id = @bill
            RETURNING receipt_first_printed_at;
            """, connection, transaction);
        mark.Parameters.Add("user", NpgsqlDbType.Uuid).Value = userId;
        mark.Parameters.Add("bill", NpgsqlDbType.Uuid).Value = billId;
        await using var marked = await mark.ExecuteReaderAsync(cancellationToken);
        await marked.ReadAsync(cancellationToken);
        return new ReceiptPrintClaim(ReceiptPrintOutcome.Original, marked.GetFieldValue<DateTimeOffset>(0));
    }

    public async Task<ReceiptResetClaim> ResetOriginalAsync(
        Guid billId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        DateTimeOffset? printedAt;
        Guid? printedBy;
        await using (var locked = new NpgsqlCommand(
            "SELECT receipt_first_printed_at, receipt_first_printed_by FROM billing.bills WHERE bill_id = @bill FOR NO KEY UPDATE;",
            connection, transaction))
        {
            locked.Parameters.Add("bill", NpgsqlDbType.Uuid).Value = billId;
            await using var reader = await locked.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return new ReceiptResetClaim(ReceiptResetOutcome.NotFound, null, null);
            printedAt = reader.IsDBNull(0) ? null : reader.GetFieldValue<DateTimeOffset>(0);
            printedBy = reader.IsDBNull(1) ? null : reader.GetGuid(1);
        }

        if (printedAt is null)
            return new ReceiptResetClaim(ReceiptResetOutcome.NothingToReset, null, null);

        await using var clear = new NpgsqlCommand(
            "UPDATE billing.bills SET receipt_first_printed_at = NULL, receipt_first_printed_by = NULL WHERE bill_id = @bill;",
            connection, transaction);
        clear.Parameters.Add("bill", NpgsqlDbType.Uuid).Value = billId;
        await clear.ExecuteNonQueryAsync(cancellationToken);
        return new ReceiptResetClaim(ReceiptResetOutcome.Reset, printedAt, printedBy);
    }
}
