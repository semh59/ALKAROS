using System.Net;
using System.Security.Cryptography;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Experience.SecurityAdministration.Maintenance;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.CustomerAccounts.Tests;

/// <summary>
/// The reconcile job over the real host and a real PostgreSQL: it is registered with the maintenance runner, repairs a
/// cached balance that disagrees with the ledger and reports how many customers it repaired.
/// </summary>
[Collection("CustomerAccounts HTTP")]
public sealed class BalanceReconcileJobHttpTests : IAsyncLifetime
{
    private readonly CustomerAccountsHttpTestDatabase _database = new();
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"alkaros-rmd882-webroot-{Guid.NewGuid():N}");

    static BalanceReconcileJobHttpTests()
    {
        Environment.SetEnvironmentVariable(
            "ALKAROS_SECRET_ENVELOPE_MASTER_KEY", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_webRoot);
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<!doctype html><html><body>Test</body></html>");
        await _database.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
        if (Directory.Exists(_webRoot))
            Directory.Delete(_webRoot, recursive: true);
    }

    [Fact]
    public async Task TheJobRepairsADriftedBalanceAndASecondRunFindsNothing()
    {
        await using var app = await StartAsync();
        var drifted = await ChargedCustomerAsync(100m);
        var healthy = await ChargedCustomerAsync(40m);
        await ExecuteAsync("UPDATE customer_account.balances SET current_balance = 90 WHERE customer_id = @id;", drifted);

        var job = JobOf(app);
        var first = await RunAsync(app, job);
        var second = await RunAsync(app, job);

        Assert.Null(job.DisabledReason);
        Assert.Equal(MaintenanceJobStatusKind.Succeeded, first.Status);
        Assert.Equal("1 müşterinin bakiyesi defterle uyumsuzdu ve yeniden hesaplandı.", first.Summary);
        Assert.Equal("Tüm müşteri bakiyeleri defterle uyumlu.", second.Summary);
        Assert.Equal(100m, await CachedBalanceAsync(drifted));
        Assert.Equal(40m, await CachedBalanceAsync(healthy));
    }

    [Fact]
    public async Task ARowWithoutALedgerIsRemovedAndAMissingRowIsRecreated()
    {
        await using var app = await StartAsync();
        var missingRow = await ChargedCustomerAsync(40m);
        var staleRow = Guid.NewGuid();
        await ExecuteAsync("DELETE FROM customer_account.balances WHERE customer_id = @id;", missingRow);
        await ExecuteAsync("INSERT INTO customer_account.balances VALUES (@id, 70, now(), now());", staleRow);

        var summary = (await RunAsync(app, JobOf(app))).Summary;

        Assert.Equal("2 müşterinin bakiyesi defterle uyumsuzdu ve yeniden hesaplandı.", summary);
        Assert.Equal(40m, await CachedBalanceAsync(missingRow));
        Assert.Null(await TryCachedBalanceAsync(staleRow));
    }

    [Fact]
    public async Task ARunRepairsAtMostFiveHundredCustomersAndTheNextRunTakesTheRest()
    {
        await using var app = await StartAsync();
        await using (var command = _database.DataSource.CreateCommand(
            "INSERT INTO customer_account.balances SELECT gen_random_uuid(), 5, now(), now() FROM generate_series(1, 501);"))
            await command.ExecuteNonQueryAsync();

        var job = JobOf(app);
        var first = await RunAsync(app, job);
        var second = await RunAsync(app, job);
        var third = await RunAsync(app, job);

        Assert.Equal("500 müşterinin bakiyesi defterle uyumsuzdu ve yeniden hesaplandı.", first.Summary);
        Assert.Equal("1 müşterinin bakiyesi defterle uyumsuzdu ve yeniden hesaplandı.", second.Summary);
        Assert.Equal("Tüm müşteri bakiyeleri defterle uyumlu.", third.Summary);
    }

    [Fact]
    public async Task TheJobRunsDailyAndSaysWhatItDoes()
    {
        await using var app = await StartAsync();

        var job = JobOf(app);

        Assert.Equal(TimeSpan.FromHours(24), job.Interval);
        Assert.Equal("Önbellekteki cari bakiyeleri defterle karşılaştırır; uyumsuz olanları defterden yeniden hesaplar.", job.Description);
    }

    private static IMaintenanceJob JobOf(WebApplication app)
        => app.Services.GetServices<IMaintenanceJob>().Single(candidate => candidate.Name == "balance-reconcile");

    private async Task<decimal?> TryCachedBalanceAsync(Guid customerId)
    {
        await using var command = _database.DataSource.CreateCommand(
            "SELECT current_balance FROM customer_account.balances WHERE customer_id = @id;");
        command.Parameters.AddWithValue("id", customerId);
        return (decimal?)await command.ExecuteScalarAsync();
    }

    private static async Task<MaintenanceJobOutcome> RunAsync(WebApplication app, IMaintenanceJob job)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await job.ExecuteAsync(scope.ServiceProvider, CancellationToken.None);
    }

    private async Task<Guid> ChargedCustomerAsync(decimal amount)
    {
        var customerId = Guid.NewGuid();
        await using var command = _database.DataSource.CreateCommand(
            """
            INSERT INTO customer_account.account_transactions
                (id, customer_id, transaction_type, amount, source_reference_type, source_reference_id, occurred_at)
            VALUES (gen_random_uuid(), @id, 'Charge', @amount, 'Test', gen_random_uuid(), now());
            """);
        command.Parameters.AddWithValue("id", customerId);
        command.Parameters.AddWithValue("amount", amount);
        await command.ExecuteNonQueryAsync();
        return customerId;
    }

    private async Task ExecuteAsync(string sql, Guid customerId)
    {
        await using var command = _database.DataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", customerId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<decimal> CachedBalanceAsync(Guid customerId)
    {
        await using var command = _database.DataSource.CreateCommand(
            "SELECT current_balance FROM customer_account.balances WHERE customer_id = @id;");
        command.Parameters.AddWithValue("id", customerId);
        return (decimal)(await command.ExecuteScalarAsync())!;
    }

    private async Task<WebApplication> StartAsync()
    {
        var app = DualScreenApplication.Build(new DualScreenOptions(
            _database.ConnectionString, _webRoot, "http://127.0.0.1:0", TrustedProxies: [IPAddress.Loopback]));
        await app.StartAsync();
        return app;
    }
}
