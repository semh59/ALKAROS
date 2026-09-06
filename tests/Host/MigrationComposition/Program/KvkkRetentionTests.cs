using ALKAROS.Host.Composition;
using ALKAROS.Host.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Tests.Program;

[Collection("Host database password environment")]
public sealed class KvkkRetentionTests : IAsyncLifetime
{
    private const string Marker = "[anonymized]";

    private readonly TestDatabase _database = new();
    private NpgsqlDataSource? _dataSource;
    private string? _originalPassword;

    public async Task InitializeAsync()
    {
        _originalPassword = Environment.GetEnvironmentVariable("ALKAROS_DB_PASSWORD");
        await _database.InitializeAsync();

        var root = FindRepositoryRoot();
        var exit = HostComposition.Run(
            new HostCompositionOptions(
                Path.Combine(root, "database", "MigrationComposition", "order.json"),
                Path.Combine(root, "database", "migrations"),
                _database.PsqlOptions),
            TextWriter.Null);
        Assert.Equal(HostExitCode.Success, exit);

        _dataSource = NpgsqlDataSource.Create(ConnectionString());
        Environment.SetEnvironmentVariable("ALKAROS_DB_PASSWORD", _database.PsqlOptions.Password);
    }

    public async Task DisposeAsync()
    {
        Environment.SetEnvironmentVariable("ALKAROS_DB_PASSWORD", _originalPassword);
        if (_dataSource is not null)
            await _dataSource.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task DryRunReportsCountsButChangesNothingThenApplyAnonymizesAndIsIdempotent()
    {
        var oldStaff = await SeedUserAsync(active: false, updatedAgoDays: 800);
        var recentStaff = await SeedUserAsync(active: false, updatedAgoDays: 30);
        var activeStaff = await SeedUserAsync(active: true, updatedAgoDays: 800);

        var productId = await SeedProductAsync();
        var oldOrder = await SeedOrderAsync(productId, status: "Completed", createdAgoYears: 6, note: "Ahmet Bey, penceresiz masa");
        var recentOrder = await SeedOrderAsync(productId, status: "Completed", createdAgoYears: 1, note: "yakın tarih notu");
        var oldDraft = await SeedOrderAsync(productId, status: "Draft", createdAgoYears: 6, note: "taslak notu");
        var heldOrder = await SeedOrderAsync(productId, status: "Completed", createdAgoYears: 6, note: "davalı - saklanacak");

        var tableId = await SeedTableAsync();
        await SeedReservationAsync(tableId, status: "Expired", reservedAgoYears: 6, reason: "Mehmet Aile - 6 kişi");
        await SeedReservationAsync(tableId, status: "Active", reservedAgoYears: 0, reason: "aktif rezervasyon");

        var excludeFile = Path.Combine(Path.GetTempPath(), $"kvkk-hold-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(excludeFile, $"# legal hold\n{heldOrder}\n");

        try
        {
            // --- dry run: reports counts, changes nothing ---
            using var dryOut = new StringWriter();
            var originalOut = Console.Out;
            int dryExit;
            try
            {
                Console.SetOut(dryOut);
                dryExit = ALKAROS.Host.Program.Main(
                    ["kvkk-retention", "--db-url", _database.Url, "--exclude-order-ids-file", excludeFile]);
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            Assert.Equal((int)HostExitCode.Success, dryExit);
            var dryLine = dryOut.ToString();
            Assert.Contains("staff=1", dryLine, StringComparison.Ordinal);
            Assert.Contains("order_notes=1", dryLine, StringComparison.Ordinal);
            Assert.Contains("item_notes=1", dryLine, StringComparison.Ordinal);
            Assert.Contains("reservation_reasons=1", dryLine, StringComparison.Ordinal);
            Assert.Contains("apply=false", dryLine, StringComparison.Ordinal);

            Assert.NotEqual(Marker, await ScalarAsync<string>(
                "SELECT notes FROM orders.orders WHERE order_id = @id;", ("id", oldOrder)));
            Assert.NotEqual("anon-" + oldStaff.ToString()[..8], await ScalarAsync<string>(
                "SELECT username FROM identity.users WHERE user_id = @id;", ("id", oldStaff)));

            // --- apply ---
            using var applyErr = new StringWriter();
            var originalErr = Console.Error;
            int applyExit;
            try
            {
                Console.SetError(applyErr);
                applyExit = ALKAROS.Host.Program.Main(
                    ["kvkk-retention", "--db-url", _database.Url, "--apply", "--exclude-order-ids-file", excludeFile]);
            }
            finally
            {
                Console.SetError(originalErr);
            }
            Assert.True(applyExit == (int)HostExitCode.Success, $"apply failed: {applyErr}");

            // old rows anonymized
            Assert.Equal(Marker, await ScalarAsync<string>(
                "SELECT notes FROM orders.orders WHERE order_id = @id;", ("id", oldOrder)));
            Assert.Equal(Marker, await ScalarAsync<string>(
                "SELECT notes FROM orders.order_items WHERE order_id = @id;", ("id", oldOrder)));
            Assert.Equal(Marker, await ScalarAsync<string>(
                "SELECT display_name FROM identity.users WHERE user_id = @id;", ("id", oldStaff)));
            Assert.True(await ScalarAsync<bool>(
                "SELECT email IS NULL AND phone IS NULL FROM identity.users WHERE user_id = @id;", ("id", oldStaff)));
            Assert.Equal(1L, await ScalarAsync<long>(
                "SELECT count(*) FROM table_mgmt.table_reservations WHERE reason = @m AND status = 'Expired';", ("m", Marker)));

            // untouched: recent, active, wrong-status, legal-held
            Assert.NotEqual(Marker, await ScalarAsync<string>(
                "SELECT notes FROM orders.orders WHERE order_id = @id;", ("id", recentOrder)));
            Assert.NotEqual(Marker, await ScalarAsync<string>(
                "SELECT notes FROM orders.orders WHERE order_id = @id;", ("id", oldDraft)));
            Assert.NotEqual(Marker, await ScalarAsync<string>(
                "SELECT notes FROM orders.orders WHERE order_id = @id;", ("id", heldOrder)));
            Assert.NotEqual(Marker, await ScalarAsync<string>(
                "SELECT display_name FROM identity.users WHERE user_id = @id;", ("id", recentStaff)));
            Assert.NotEqual(Marker, await ScalarAsync<string>(
                "SELECT display_name FROM identity.users WHERE user_id = @id;", ("id", activeStaff)));
            Assert.Equal(1L, await ScalarAsync<long>(
                "SELECT count(*) FROM table_mgmt.table_reservations WHERE reason <> @m;", ("m", Marker)));

            // --- second apply: idempotent, everything reports zero ---
            using var reOut = new StringWriter();
            var originalOut2 = Console.Out;
            try
            {
                Console.SetOut(reOut);
                var reExit = ALKAROS.Host.Program.Main(
                    ["kvkk-retention", "--db-url", _database.Url, "--apply", "--exclude-order-ids-file", excludeFile]);
                Assert.Equal((int)HostExitCode.Success, reExit);
            }
            finally
            {
                Console.SetOut(originalOut2);
            }

            var reLine = reOut.ToString();
            Assert.Contains("staff=0 order_notes=0 item_notes=0 reservation_reasons=0", reLine, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(excludeFile);
        }
    }

    [Fact]
    public void MissingDbUrlFailsClosed()
    {
        var exit = ALKAROS.Host.Program.Main(["kvkk-retention"]);
        Assert.Equal((int)HostExitCode.StartupFailed, exit);
    }

    [Fact]
    public void UnknownArgumentFailsClosed()
    {
        var exit = ALKAROS.Host.Program.Main(["kvkk-retention", "--db-url", _database.Url, "--nope"]);
        Assert.Equal((int)HostExitCode.StartupFailed, exit);
    }

    private async Task<Guid> SeedUserAsync(bool active, int updatedAgoDays)
    {
        var id = Guid.NewGuid();
        await using var command = _dataSource!.CreateCommand(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, email, phone, active, created_at, updated_at)
            VALUES (@id, @u, 'hash', 'Gerçek Ad', 'a@b.com', '05001112233', @active,
                    now() - interval '900 days', now() - make_interval(days => @ago));
            """);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("u", $"user-{id:N}"[..24]);
        command.Parameters.AddWithValue("active", active);
        command.Parameters.AddWithValue("ago", updatedAgoDays);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    private async Task<Guid> SeedProductAsync()
    {
        var id = Guid.NewGuid();
        await using var command = _dataSource!.CreateCommand(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active, display_order, current_price)
            VALUES (@id, @sku, 'Test', 1, 1, true, 0, 10.00);
            """);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("sku", $"K-{id:N}"[..20]);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    private async Task<Guid> SeedOrderAsync(Guid productId, string status, int createdAgoYears, string note)
    {
        var orderId = Guid.NewGuid();
        await using (var order = _dataSource!.CreateCommand(
            """
            INSERT INTO orders.orders
              (order_id, source, table_id, status, confirmation_status, order_number, currency_code, notes, created_at, updated_at, row_version)
            VALUES (@id, 'Cashier', NULL, @st, 'NotRequired', @num, 'TRY', @note,
                    now() - make_interval(years => @ago), now() - make_interval(years => @ago), 1);
            """))
        {
            order.Parameters.AddWithValue("id", orderId);
            order.Parameters.AddWithValue("st", status);
            order.Parameters.AddWithValue("num", $"K-{orderId:N}"[..20]);
            order.Parameters.AddWithValue("note", note);
            order.Parameters.AddWithValue("ago", createdAgoYears);
            await order.ExecuteNonQueryAsync();
        }

        await using var item = _dataSource!.CreateCommand(
            """
            INSERT INTO orders.order_items
              (order_item_id, order_id, product_id, product_name_snapshot, quantity, unit_price, tax_rate, tax_amount, net_amount, gross_amount, status, kitchen_state, portion_reservation_status, notes, created_at, updated_at, row_version)
            VALUES (@iid, @oid, @pid, 'Test', 1, 10, 10, 1, 10, 11, 'Active', 'Served', 'NotApplicable', @note,
                    now() - make_interval(years => @ago), now() - make_interval(years => @ago), 1);
            """);
        item.Parameters.AddWithValue("iid", Guid.NewGuid());
        item.Parameters.AddWithValue("oid", orderId);
        item.Parameters.AddWithValue("pid", productId);
        item.Parameters.AddWithValue("note", note);
        item.Parameters.AddWithValue("ago", createdAgoYears);
        await item.ExecuteNonQueryAsync();
        return orderId;
    }

    private async Task<Guid> SeedTableAsync()
    {
        var id = Guid.NewGuid();
        await using var command = _dataSource!.CreateCommand(
            """
            INSERT INTO table_mgmt.tables (table_id, zone_id, table_number, capacity, active, current_status, current_order_id, current_bill_id, row_version)
            VALUES (@id, NULL, @n, 2, true, 'Available', NULL, NULL, 1);
            """);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("n", $"K-{id:N}"[..12]);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    private async Task SeedReservationAsync(Guid tableId, string status, int reservedAgoYears, string reason)
    {
        await using var command = _dataSource!.CreateCommand(
            """
            INSERT INTO table_mgmt.table_reservations
              (table_reservation_id, table_id, actor_type, status, reason, party_size, reserved_at, row_version)
            VALUES (@id, @tid, 'User', @st, @reason, 4, now() - make_interval(years => @ago), 1);
            """);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("tid", tableId);
        command.Parameters.AddWithValue("st", status);
        command.Parameters.AddWithValue("reason", reason);
        command.Parameters.AddWithValue("ago", reservedAgoYears);
        await command.ExecuteNonQueryAsync();
    }


    private async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = _dataSource!.CreateCommand(sql);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        var result = await command.ExecuteScalarAsync();
        return (T)(result ?? throw new InvalidOperationException("Scalar returned null."));
    }

    private string ConnectionString()
    {
        var uri = new Uri(_database.Url);
        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port,
            Database = uri.AbsolutePath.Trim('/'),
            Username = uri.UserInfo,
            Password = _database.PsqlOptions.Password,
        }.ConnectionString;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "database", "MigrationComposition", "order.json")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }
}
