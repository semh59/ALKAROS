namespace ALKAROS.Host.DualScreen;

public sealed partial class DualScreenStore
{
    /// <summary>
    /// V1-WTR-018: the QR guest's own read-only live bill (garson-karsilastirma
    /// idea #6, a guest watching their own table's running tab). Keyed by
    /// <paramref name="tableId"/> — the caller (QrOrderingEndpoints) gets that
    /// from the guest's own validated customer session, exactly like
    /// <see cref="GetCatalogAsync"/>'s /menu caller does, never from a
    /// client-supplied id. Reads <c>table_mgmt.tables.current_order_id</c>
    /// then the order's own totals/lines with plain SQL, the same read-model
    /// approach <see cref="GetSnapshotAsync"/> uses for the physical customer
    /// display — a guest never sees a void/comp/cancelled line or an internal
    /// id, only what they would recognise as "my order".
    /// </summary>
    public async Task<QrLiveBillDto> GetLiveBillAsync(Guid tableId, CancellationToken cancellationToken)
    {
        Guid? orderId;
        await using (var tableCommand = _dataSource.CreateCommand(
            "SELECT current_order_id FROM table_mgmt.tables WHERE table_id = @table_id;"))
        {
            tableCommand.Parameters.AddWithValue("table_id", tableId);
            var result = await tableCommand.ExecuteScalarAsync(cancellationToken);
            orderId = result is null or DBNull ? null : (Guid)result;
        }

        if (orderId is null)
            return QrLiveBillDto.Empty;

        string status;
        decimal subtotal, taxTotal, total;
        long revision;
        await using (var orderCommand = _dataSource.CreateCommand(
            """
            SELECT status, subtotal, tax_total, total, row_version
            FROM orders.orders
            WHERE order_id = @order_id;
            """))
        {
            orderCommand.Parameters.AddWithValue("order_id", orderId.Value);
            await using var reader = await orderCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return QrLiveBillDto.Empty;
            status = reader.GetString(0);
            subtotal = reader.GetDecimal(1);
            taxTotal = reader.GetDecimal(2);
            total = reader.GetDecimal(3);
            revision = reader.GetInt64(4);
        }

        // A closed-out or abandoned check is not "live" anymore - the guest's
        // page falls back to the same empty state as "nothing ordered yet"
        // rather than showing a stale total from a tab that is no longer
        // theirs to watch (table_mgmt's own current_order_id is cleared on
        // close in the normal case; this is the defensive read-side mirror
        // of that, same reasoning GetSnapshotAsync's IdleSnapshot fallback
        // above uses for the physical display).
        if (status is "Completed" or "Cancelled" or "Rejected")
            return QrLiveBillDto.Empty;

        var lines = new List<QrLiveBillLineDto>();
        await using (var lineCommand = _dataSource.CreateCommand(
            """
            SELECT product_name_snapshot, quantity, unit_price, gross_amount
            FROM orders.order_items
            WHERE order_id = @order_id AND status IN ('Draft', 'Active')
            ORDER BY created_at, order_item_id;
            """))
        {
            lineCommand.Parameters.AddWithValue("order_id", orderId.Value);
            await using var reader = await lineCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                lines.Add(new QrLiveBillLineDto(
                    reader.GetString(0), reader.GetDecimal(1), reader.GetDecimal(2), reader.GetDecimal(3)));
            }
        }

        return new QrLiveBillDto(true, lines, subtotal, taxTotal, total, revision);
    }
}
