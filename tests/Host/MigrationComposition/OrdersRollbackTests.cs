using ALKAROS.Host.Composition.Migrations;
using ALKAROS.Host.Tests.Fixtures;
using Xunit;

namespace ALKAROS.Host.Tests.Composition;

[Collection("Host database password environment")]
public sealed class OrdersRollbackTests : IAsyncLifetime
{
    private readonly TestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task RollingBackThroughTheNfcAndCourseMigrationsSucceedsWhileNfcOrdersAndHeldItemsExist()
    {
        await ExecuteAsync(
            """
            CREATE SCHEMA orders;
            CREATE TABLE orders.orders (
                order_id UUID PRIMARY KEY,
                source TEXT NOT NULL,
                CONSTRAINT orders_source_check CHECK (source IN ('Cashier', 'Waiter', 'Qr', 'Online')));
            CREATE TABLE orders.order_items (
                order_item_id UUID PRIMARY KEY,
                order_id UUID NOT NULL REFERENCES orders.orders (order_id),
                kitchen_state TEXT NOT NULL,
                CONSTRAINT order_items_kitchen_state_check
                    CHECK (kitchen_state IN ('NotSent', 'Sent', 'Preparing', 'Ready', 'Served', 'Cancelled')));
            """);
        await ApplyAsync("V12", "V12-NFC-001", "077-orders-nfc-source.up.sql");
        await ApplyAsync("V1", "V1-WTR-025", "105-order-items-course.up.sql");
        await ApplyAsync("V1", "V1-RMD-724", "206-orders-check-rollback-safe.up.sql");
        await ExecuteAsync(
            """
            INSERT INTO orders.orders VALUES ('00000000-0000-0000-0000-000000000001', 'Nfc');
            INSERT INTO orders.order_items VALUES
                ('00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000001', 'Held');
            """);

        await ApplyAsync("V1", "V1-RMD-724", "206-orders-check-rollback-safe.down.sql");
        await ApplyAsync("V1", "V1-WTR-025", "105-order-items-course.down.sql");
        await ApplyAsync("V12", "V12-NFC-001", "077-orders-nfc-source.down.sql");

        Assert.Equal("Qr", await ScalarAsync("SELECT source FROM orders.orders;"));
        Assert.Equal("Sent", await ScalarAsync("SELECT kitchen_state FROM orders.order_items;"));
    }

    private async Task ApplyAsync(string version, string taskDirectory, string fileName)
    {
        var path = Path.Combine(FindRepositoryRoot(), "database", "migrations", version, taskDirectory, fileName);
        var result = await PsqlScriptRunner.RunAsync(path, _database.PsqlOptions, CancellationToken.None);
        Assert.True(result.Success, $"{fileName} failed: {result.ErrorSummary}");
    }

    private async Task ExecuteAsync(string sql)
    {
        var result = await PsqlScriptRunner.RunCommandAsync(sql, _database.PsqlOptions, CancellationToken.None);
        Assert.True(result.Success, result.ErrorSummary);
    }

    private async Task<string> ScalarAsync(string sql)
    {
        var result = await PsqlScriptRunner.RunCommandAsync(sql, _database.PsqlOptions, CancellationToken.None);
        Assert.True(result.Success, result.ErrorSummary);
        return result.StandardOutput.Trim();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ALKAROS.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }
}
