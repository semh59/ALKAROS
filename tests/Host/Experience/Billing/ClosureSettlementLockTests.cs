using ALKAROS.Billing.PaymentClosure;
using ALKAROS.Host.Experience.Orders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.Billing.Tests;

/// <summary>Closing a bill reads its payments and saves it under the lock every payment and refund of the bill takes.</summary>
public sealed class ClosureSettlementLockTests : IAsyncLifetime
{
    private readonly BillingSplitTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task ClosingWaitsForABillSettlementInProgressAndThenSeesItsResult()
    {
        var (userId, _) = await _database.SeedSessionWithPermissionsAsync(Guid.NewGuid(), "cashier", "bills.split");
        var bill = await _database.SeedBillAsync();
        await _database.SeedCollectedAsync(bill.BillId, await _database.BillPayableAsync(bill.BillId), userId);
        await using var app = await StartAsync();
        var closure = app.Services.GetRequiredService<IBillClosureService>();

        await using var source = NpgsqlDataSource.Create(_database.ConnectionString);
        await using var connection = await source.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var hold = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext($1)::bigint);", connection, transaction))
        {
            hold.Parameters.AddWithValue($"bill-settlement:{bill.BillId:N}");
            await hold.ExecuteNonQueryAsync();
        }

        var closing = closure.TryCloseAsync(bill.BillId);
        var finishedWhileLocked = await Task.WhenAny(closing, Task.Delay(TimeSpan.FromSeconds(2))) == closing;
        await transaction.CommitAsync();
        var result = await closing.WaitAsync(TimeSpan.FromSeconds(20));

        Assert.False(finishedWhileLocked);
        Assert.Equal(BillClosureOutcome.Closed, result.Outcome);
        Assert.Equal("Paid", await _database.BillStatusAsync(bill.BillId));
    }

    private async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(NpgsqlDataSource.Create(_database.ConnectionString));
        builder.Services.AddOrderManagementExperience();
        builder.Services.AddBillingSplitExperience();
        var app = builder.Build();
        await app.StartAsync();
        return app;
    }
}
