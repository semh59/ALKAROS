using ALKAROS.Host.Composition;
using ALKAROS.Host.Composition.Modules;
using ALKAROS.Reconciliation.CaseFoundation;
using ALKAROS.Reconciliation.OnlineOrders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ALKAROS.Host.Experience.Reconciliation.Tests;

/// <summary>
/// The scheduled online order scan against the real schema: a divergence becomes a case with nobody calling the
/// scan endpoint, repeated passes never double it, and a pass that fails does not stop the next one.
/// </summary>
[Collection("Reconciliation case PostgreSQL HTTP")]
public sealed class OnlineOrderReconciliationHostedServiceTests : IAsyncLifetime
{
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(50);

    private readonly ReconciliationCaseTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private ServiceProvider BuildProvider(int failingResolutions = 0)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_database.DataSource);
        services.AddLogging();
        var composition = ModuleRegistry.ComposeRoot(ModuleRegistry.DefaultCatalog);
        HostComposition.ApplyComposedModuleServices(services, composition.Services);
        services.AddReconciliationCaseExperience();
        var remaining = failingResolutions;
        services.AddTransient(provider => remaining-- > 0
            ? throw new InvalidOperationException("scanner unavailable")
            : new OnlineOrderReconciliationScanner(
                provider.GetRequiredService<IReadOnlyList<IOnlineOrderSourcePair>>(),
                provider.GetRequiredService<IReconciliationService>()));
        return services.BuildServiceProvider();
    }

    private async Task SeedRefusedOrderAsync(string externalOrderId)
    {
        var inboxId = Guid.NewGuid();
        await _database.ExecuteAsync(
            """
            INSERT INTO online_ordering.provider_inbox
                (provider, inbox_id, event_key, external_order_id, provider_status, body_sha256, payload_envelope,
                 processed_at, processing_outcome, outcome_detail)
            VALUES ('yemeksepeti', @inbox, lpad(replace(@inbox::text, '-', ''), 64, '0'), @external, 'RECEIVED',
                    lpad(replace(@inbox::text, '-', ''), 64, '0'), '\x00'::bytea, now(), 'Rejected',
                    '{"rejection":"UnmappedSku","providerCancellationRequested":false}'::jsonb);
            """,
            ("inbox", inboxId), ("external", externalOrderId));
    }

    private async Task<long> CasesAsync(string externalOrderId) => await _database.ScalarAsync<long>(
        $"SELECT count(*) FROM reconciliation.cases WHERE deduplication_key = 'online-order:provider-accepted-locally-refused:yemeksepeti:{externalOrderId}';");

    private static async Task WaitForAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!await condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The scheduled scan did not reach the expected state in time.");
            await Task.Delay(Tick);
        }
    }

    private static OnlineOrderReconciliationHostedService Create(ServiceProvider provider) => new(
        provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<OnlineOrderReconciliationHostedService>.Instance, Tick, Tick);

    [Fact]
    public async Task AScheduledPassTurnsARefusedOrderIntoACaseAndRepeatedPassesNeverDoubleIt()
    {
        var externalOrderId = "ys-sched-" + Guid.NewGuid().ToString("N")[..12];
        await SeedRefusedOrderAsync(externalOrderId);
        await using var provider = BuildProvider();
        using var service = Create(provider);

        await service.StartAsync(CancellationToken.None);
        await WaitForAsync(async () => await CasesAsync(externalOrderId) == 1);
        await Task.Delay(TimeSpan.FromMilliseconds(400));
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(1L, await CasesAsync(externalOrderId));
    }

    [Fact]
    public async Task AFailedPassIsRetriedOnTheNextPass()
    {
        var externalOrderId = "ys-sched-" + Guid.NewGuid().ToString("N")[..12];
        await SeedRefusedOrderAsync(externalOrderId);
        await using var provider = BuildProvider(failingResolutions: 2);
        using var service = Create(provider);

        await service.StartAsync(CancellationToken.None);
        await WaitForAsync(async () => await CasesAsync(externalOrderId) == 1);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(1L, await CasesAsync(externalOrderId));
    }
}
