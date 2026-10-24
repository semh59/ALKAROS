using ALKAROS.Billing.BillFoundation;
using ALKAROS.Host.Experience.Orders;
using ALKAROS.Orders.OrderAggregate;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Tests.Experience.Tables;

[Collection("Table management PostgreSQL HTTP")]
public sealed class MergeSettlementTests : IAsyncLifetime
{
    private readonly TableManagementTestDatabase _database = new();
    private NpgsqlDataSource Source => _database.Source;

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task PayingTheLastOrderOfAMergedTableFreesBothTablesAndEndsTheMerge()
    {
        var merged = await SeedMergedTablesAsync();

        Assert.True(await SettleAsync(merged.PrimaryBill));

        // The other table order is still open: the primary keeps going and the merge stays.
        var primary = await ReadTableAsync(merged.Primary);
        Assert.Equal("Occupied", primary.Status);
        Assert.Equal(merged.ParticipantOrder, primary.OrderId);
        Assert.Equal(merged.ParticipantBill, primary.BillId);
        Assert.Equal("Active", await MergeStatusAsync(merged.Group));
        Assert.Equal("Occupied", (await ReadTableAsync(merged.Participant)).Status);

        Assert.True(await SettleAsync(merged.ParticipantBill));

        primary = await ReadTableAsync(merged.Primary);
        Assert.Equal("Available", primary.Status);
        Assert.Null(primary.OrderId);
        Assert.Null(primary.BillId);
        Assert.Equal("Unmerged", await MergeStatusAsync(merged.Group));
        var participant = await ReadTableAsync(merged.Participant);
        Assert.Equal("Available", participant.Status);
        Assert.Null(participant.OrderId);
    }

    [Fact]
    public async Task PayingAnOrderThatIsNotThePrimaryPointerLeavesTheMergeAndTablesAlone()
    {
        var merged = await SeedMergedTablesAsync();

        Assert.True(await SettleAsync(merged.ParticipantBill));

        var primary = await ReadTableAsync(merged.Primary);
        Assert.Equal("Occupied", primary.Status);
        Assert.Equal(merged.PrimaryOrder, primary.OrderId);
        Assert.Equal("Active", await MergeStatusAsync(merged.Group));
        Assert.Equal("Occupied", (await ReadTableAsync(merged.Participant)).Status);
    }

    private async Task<bool> SettleAsync(Guid billId)
    {
        await Exec(
            "UPDATE billing.bills SET status = 'Paid', paid_amount = payable_amount, allocated_amount = payable_amount WHERE bill_id = @id;",
            ("id", billId));
        return await new OrderSettlementService(
            new PostgresOrderRepository(Source), new PostgresBillRepository(Source), Source)
            .CompleteForPaidBillAsync(billId);
    }

    private async Task<string> MergeStatusAsync(Guid group)
    {
        await using var command = Source.CreateCommand("SELECT status FROM table_mgmt.table_merges WHERE merge_group_id = @g;");
        command.Parameters.AddWithValue("g", group);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private async Task<(string Status, Guid? OrderId, Guid? BillId)> ReadTableAsync(Guid tableId)
    {
        await using var command = Source.CreateCommand(
            "SELECT current_status, current_order_id, current_bill_id FROM table_mgmt.tables WHERE table_id = @id;");
        command.Parameters.AddWithValue("id", tableId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2));
    }

    private async Task Exec(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = Source.CreateCommand(sql);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    // A primary and a participant table merged: both orders and bills sit on the primary, whose
    // pointer shows its own order; the participant is Occupied with no pointers.
    private async Task<(Guid Primary, Guid Participant, Guid Group, Guid PrimaryOrder, Guid PrimaryBill, Guid ParticipantOrder, Guid ParticipantBill)> SeedMergedTablesAsync()
    {
        Guid primary = Guid.NewGuid(), participant = Guid.NewGuid(), zone = Guid.NewGuid(), user = Guid.NewGuid();
        Guid primaryOrder = Guid.NewGuid(), primaryBill = Guid.NewGuid(), participantOrder = Guid.NewGuid(), participantBill = Guid.NewGuid();
        var group = Guid.NewGuid();
        var sku = zone.ToString("N")[..8];

        await Exec(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user, @username, 'not-used', 'Settlement Test', true);
            INSERT INTO table_mgmt.zones (zone_id, code, name, sort_order, active) VALUES (@zone, @code, 'Hall', 1, true);
            INSERT INTO table_mgmt.tables (table_id, zone_id, table_number, capacity, active, current_status, row_version)
            VALUES (@primary, @zone, 'S-1', 4, true, 'Occupied', 2), (@participant, @zone, 'S-2', 4, true, 'Occupied', 2);
            """,
            ("user", user), ("username", "settle-" + sku), ("zone", zone), ("code", "Z" + sku), ("primary", primary), ("participant", participant));

        foreach (var (order, bill, number) in new[] { (primaryOrder, primaryBill, "A"), (participantOrder, participantBill, "B") })
        {
            await Exec(
                """
                INSERT INTO orders.orders (
                    order_id, source, table_id, status, confirmation_status, order_number,
                    subtotal, discount_total, tax_total, total, created_at, updated_at, row_version)
                VALUES (@order, 'Cashier', @primary, 'Submitted', 'Accepted', @order_number, 100, 0, 0, 100, now(), now(), 1);
                INSERT INTO billing.bills (
                    bill_id, bill_number, table_id, order_id, status, subtotal, discount_total, tax_total,
                    payable_amount, allocated_amount, paid_amount, change_amount, currency_code,
                    opened_at, created_at, updated_at, row_version)
                VALUES (@bill, @bill_number, @primary, @order, 'Open', 100, 0, 0, 100, 0, 0, 0, 'TRY', now(), now(), now(), 1);
                """,
                ("order", order), ("bill", bill), ("primary", primary),
                ("order_number", "ORD-SET-" + number + sku), ("bill_number", "BIL-SET-" + number + sku));
        }

        await Exec(
            """
            UPDATE table_mgmt.tables SET current_order_id = @order, current_bill_id = @bill WHERE table_id = @primary;
            INSERT INTO table_mgmt.table_merges (
                table_merge_id, merge_group_id, primary_table_id, merged_table_id, original_order_id, original_bill_id,
                original_order_ids, original_bill_ids, status, reason, merged_by, merged_at, row_version)
            VALUES (gen_random_uuid(), @group, @primary, @participant, @p_order, @p_bill,
                ARRAY[@p_order], ARRAY[@p_bill], 'Active', 'Joined', @user, now(), 1);
            """,
            ("order", primaryOrder), ("bill", primaryBill), ("primary", primary), ("group", group),
            ("participant", participant), ("p_order", participantOrder), ("p_bill", participantBill), ("user", user));

        return (primary, participant, group, primaryOrder, primaryBill, participantOrder, participantBill);
    }
}
