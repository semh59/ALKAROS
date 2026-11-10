using ALKAROS.Identity.Authorization.Grants;
using Npgsql;

namespace ALKAROS.Host.Experience.Billing;

/// <summary>
/// The comp and discount total of a bill is what its deduction adjustments take off now, so an undone one drops out of it,
/// plus the full value of the items a waiter comped on their own (a comped line pays nothing but keeps its value as discount).
/// </summary>
public sealed class BillAppliedAdjustmentTotals : IBillAdjustmentTotals
{
    private readonly NpgsqlDataSource _dataSource;

    public BillAppliedAdjustmentTotals(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<decimal> GetAppliedAsync(Guid billId, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT COALESCE((SELECT SUM(gross_amount) FROM billing.bill_adjustments WHERE bill_id = @bill AND is_deduction), 0)
                 + COALESCE((SELECT SUM(discount_amount) FROM billing.bill_items WHERE bill_id = @bill AND line_type = 'Complimentary'), 0);
            """);
        command.Parameters.AddWithValue("bill", billId);
        return (decimal)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}

/// <summary>
/// Held from the cap check of a comp or discount request until the adjustment is saved: the total the cap reads comes
/// from saved adjustments, so two requests for one bill that overlapped would both see the old total.
/// </summary>
internal sealed class BillAdjustmentLock : IAsyncDisposable
{
    private readonly NpgsqlConnection _connection;
    private readonly NpgsqlTransaction _transaction;
    private bool _released;

    private BillAdjustmentLock(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public static async Task<BillAdjustmentLock> AcquireAsync(NpgsqlDataSource dataSource, Guid billId, CancellationToken cancellationToken)
    {
        var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        try
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext($1)::bigint);", connection, transaction);
            command.Parameters.AddWithValue($"bill-adjustment-apply:{billId:N}");
            await command.ExecuteNonQueryAsync(cancellationToken);
            return new BillAdjustmentLock(connection, transaction);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    /// <summary>Releases the lock; the handler does it as soon as the adjustment is saved and the scope end repeats it harmlessly.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_released)
            return;
        _released = true;
        await _transaction.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
