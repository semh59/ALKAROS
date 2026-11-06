using Npgsql;

namespace ALKAROS.Billing.BillFoundation;

public sealed class PostgresBillTerminalOwnerRepository : IBillTerminalOwnerRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresBillTerminalOwnerRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<Guid?> ClaimAsync(Guid billId, Guid terminalId, CancellationToken cancellationToken = default)
    {
        if (billId == Guid.Empty)
            throw new ArgumentException("Bill id cannot be empty.", nameof(billId));
        if (terminalId == Guid.Empty)
            throw new ArgumentException("Terminal id cannot be empty.", nameof(terminalId));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using (var claim = new NpgsqlCommand(
            "UPDATE billing.bills SET terminal_id = @terminal_id WHERE bill_id = @bill_id AND terminal_id IS NULL;", connection))
        {
            claim.Parameters.AddWithValue("bill_id", billId);
            claim.Parameters.AddWithValue("terminal_id", terminalId);
            await claim.ExecuteNonQueryAsync(cancellationToken);
        }

        return await ReadOwnerAsync(connection, billId, cancellationToken);
    }

    public async Task<Guid?> FindOwnerAsync(Guid billId, CancellationToken cancellationToken = default)
    {
        if (billId == Guid.Empty)
            throw new ArgumentException("Bill id cannot be empty.", nameof(billId));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadOwnerAsync(connection, billId, cancellationToken);
    }

    private static async Task<Guid?> ReadOwnerAsync(NpgsqlConnection connection, Guid billId, CancellationToken cancellationToken)
    {
        await using var read = new NpgsqlCommand("SELECT terminal_id FROM billing.bills WHERE bill_id = @bill_id;", connection);
        read.Parameters.AddWithValue("bill_id", billId);
        return await read.ExecuteScalarAsync(cancellationToken) as Guid?;
    }
}
