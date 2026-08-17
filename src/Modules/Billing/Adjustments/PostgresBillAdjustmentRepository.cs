using System.Globalization;
using Npgsql;

namespace ALKAROS.Billing.Adjustments;

/// <summary>
/// PostgreSQL implementation of <see cref="IBillAdjustmentRepository"/>.
/// </summary>
public sealed class PostgresBillAdjustmentRepository : IBillAdjustmentRepository
{
    private const string TableName = "billing.bill_adjustments";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresBillAdjustmentRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<IReadOnlyList<BillAdjustment>> GetByBillIdAsync(
        Guid billId,
        CancellationToken cancellationToken = default)
    {
        if (billId == Guid.Empty)
            throw new ArgumentException("Bill id cannot be empty.", nameof(billId));

        var sql = $"""
            SELECT bill_adjustment_id, bill_id, bill_item_id, adjustment_type,
                   calculation_type, rate, amount, tax_rate, tax_amount,
                   net_amount, gross_amount, is_deduction, reason, authorized_by,
                   notes, created_at, created_by, row_version
            FROM {TableName}
            WHERE bill_id = @bill_id
            ORDER BY created_at ASC;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("bill_id", billId);

        var list = new List<BillAdjustment>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new BillAdjustment(
                id: reader.GetGuid(0),
                billId: reader.GetGuid(1),
                billItemId: reader.IsDBNull(2) ? null : reader.GetGuid(2),
                adjustmentType: Enum.Parse<AdjustmentType>(reader.GetString(3)),
                calculationType: Enum.Parse<AdjustmentCalculationType>(reader.GetString(4)),
                rate: reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                amount: reader.GetDecimal(6),
                taxRate: reader.GetDecimal(7),
                taxAmount: reader.GetDecimal(8),
                netAmount: reader.GetDecimal(9),
                grossAmount: reader.GetDecimal(10),
                isDeduction: reader.GetBoolean(11),
                reason: reader.GetString(12),
                authorizedBy: reader.GetGuid(13),
                notes: reader.IsDBNull(14) ? null : reader.GetString(14),
                createdAt: reader.GetFieldValue<DateTimeOffset>(15),
                createdBy: reader.IsDBNull(16) ? null : reader.GetGuid(16),
                rowVersion: reader.GetInt64(17)));
        }

        return list;
    }

    public async Task AddAsync(
        BillAdjustment adjustment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adjustment);

        var sql = $"""
            INSERT INTO {TableName} (
                bill_adjustment_id, bill_id, bill_item_id, adjustment_type,
                calculation_type, rate, amount, tax_rate, tax_amount,
                net_amount, gross_amount, is_deduction, reason, authorized_by,
                notes, created_at, created_by, row_version)
            VALUES (
                @bill_adjustment_id, @bill_id, @bill_item_id, @adjustment_type,
                @calculation_type, @rate, @amount, @tax_rate, @tax_amount,
                @net_amount, @gross_amount, @is_deduction, @reason, @authorized_by,
                @notes, @created_at, @created_by, @row_version);
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("bill_adjustment_id", adjustment.Id);
        command.Parameters.AddWithValue("bill_id", adjustment.BillId);
        command.Parameters.AddWithValue("bill_item_id", (object?)adjustment.BillItemId ?? DBNull.Value);
        command.Parameters.AddWithValue("adjustment_type", adjustment.AdjustmentType.ToString());
        command.Parameters.AddWithValue("calculation_type", adjustment.CalculationType.ToString());
        command.Parameters.AddWithValue("rate", (object?)adjustment.Rate ?? DBNull.Value);
        command.Parameters.AddWithValue("amount", adjustment.Amount);
        command.Parameters.AddWithValue("tax_rate", adjustment.TaxRate);
        command.Parameters.AddWithValue("tax_amount", adjustment.TaxAmount);
        command.Parameters.AddWithValue("net_amount", adjustment.NetAmount);
        command.Parameters.AddWithValue("gross_amount", adjustment.GrossAmount);
        command.Parameters.AddWithValue("is_deduction", adjustment.IsDeduction);
        command.Parameters.AddWithValue("reason", adjustment.Reason);
        command.Parameters.AddWithValue("authorized_by", adjustment.AuthorizedBy);
        command.Parameters.AddWithValue("notes", (object?)adjustment.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("created_at", adjustment.CreatedAt);
        command.Parameters.AddWithValue("created_by", (object?)adjustment.CreatedBy ?? DBNull.Value);
        command.Parameters.AddWithValue("row_version", adjustment.RowVersion);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RemoveAsync(
        Guid adjustmentId,
        CancellationToken cancellationToken = default)
    {
        if (adjustmentId == Guid.Empty)
            throw new ArgumentException("Adjustment id cannot be empty.", nameof(adjustmentId));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            $"DELETE FROM {TableName} WHERE bill_adjustment_id = @id;",
            connection);
        command.Parameters.AddWithValue("id", adjustmentId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<decimal> GetTotalDiscountAmountAsync(
        Guid billId,
        CancellationToken cancellationToken = default)
    {
        if (billId == Guid.Empty)
            throw new ArgumentException("Bill id cannot be empty.", nameof(billId));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            $"SELECT COALESCE(SUM(gross_amount), 0) FROM {TableName} WHERE bill_id = @bill_id AND is_deduction = true;",
            connection);
        command.Parameters.AddWithValue("bill_id", billId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? 0m : Convert.ToDecimal(result, CultureInfo.InvariantCulture);
    }

    public async Task<decimal> GetTotalFeeAmountAsync(
        Guid billId,
        CancellationToken cancellationToken = default)
    {
        if (billId == Guid.Empty)
            throw new ArgumentException("Bill id cannot be empty.", nameof(billId));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            $"SELECT COALESCE(SUM(gross_amount), 0) FROM {TableName} WHERE bill_id = @bill_id AND is_deduction = false;",
            connection);
        command.Parameters.AddWithValue("bill_id", billId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? 0m : Convert.ToDecimal(result, CultureInfo.InvariantCulture);
    }
}
