using ALKAROS.Reporting.MenuInventory;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ALKAROS.Host.Experience.InventoryReporting.Tests;

/// <summary>
/// V11-RPT-002: proves the worker only broadcasts an item the FIRST time it
/// crosses into critical, never a repeat for one still critical on the next
/// scan (the whole point of tracking `_previouslyCritical`) — and that a
/// recovered-then-critical-again item alerts a second time.
/// </summary>
public sealed class LowStockAlertHostedServiceTests
{
    [Fact]
    public async Task FirstScanBroadcastsEveryCriticalItem()
    {
        var reporting = new FakeReportingService(CriticalReport(("A", 5m, 10m)));
        var (service, hub) = CreateService(reporting);

        await service.ScanAsync(CancellationToken.None);

        Assert.Equal(1, hub.BroadcastCount);
    }

    [Fact]
    public async Task SecondScanWithTheSameCriticalItemDoesNotReBroadcast()
    {
        var itemId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var reporting = new FakeReportingService();
        reporting.NextReport = CriticalReportWithId(itemId, locationId, "A", 5m, 10m);
        var (service, hub) = CreateService(reporting);

        await service.ScanAsync(CancellationToken.None);
        Assert.Equal(1, hub.BroadcastCount);

        reporting.NextReport = CriticalReportWithId(itemId, locationId, "A", 4m, 10m); // still critical
        await service.ScanAsync(CancellationToken.None);

        Assert.Equal(1, hub.BroadcastCount);
    }

    [Fact]
    public async Task RecoveringThenGoingCriticalAgainReBroadcasts()
    {
        var itemId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var reporting = new FakeReportingService();
        reporting.NextReport = CriticalReportWithId(itemId, locationId, "A", 5m, 10m);
        var (service, hub) = CreateService(reporting);

        await service.ScanAsync(CancellationToken.None);
        Assert.Equal(1, hub.BroadcastCount);

        reporting.NextReport = NotCriticalReportWithId(itemId, locationId, "A", 50m, 10m); // recovered
        await service.ScanAsync(CancellationToken.None);
        Assert.Equal(1, hub.BroadcastCount);

        reporting.NextReport = CriticalReportWithId(itemId, locationId, "A", 3m, 10m); // critical again
        await service.ScanAsync(CancellationToken.None);
        Assert.Equal(2, hub.BroadcastCount);
    }

    [Fact]
    public async Task NoCriticalItemsBroadcastsNothing()
    {
        var reporting = new FakeReportingService(new CriticalStockReport([], 0));
        var (service, hub) = CreateService(reporting);

        await service.ScanAsync(CancellationToken.None);

        Assert.Equal(0, hub.BroadcastCount);
    }

    private static (LowStockAlertHostedService Service, RecordingHubContext Hub) CreateService(FakeReportingService reporting)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMenuInventoryReportingService>(reporting);
        var provider = services.BuildServiceProvider();
        var hub = new RecordingHubContext();
        var service = new LowStockAlertHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(), hub, NullLogger<LowStockAlertHostedService>.Instance);
        return (service, hub);
    }

    private static CriticalStockReport CriticalReport(params (string Code, decimal Available, decimal Threshold)[] items)
        => new(
            items.Select(i => new CriticalStockReportItem(
                Guid.NewGuid(), i.Code, i.Code, "RawMaterial", "kg", Guid.NewGuid(), "Depo",
                i.Available, 0m, i.Available, i.Threshold, IsCritical: true, IsReconciled: true)).ToList(),
            items.Length);

    private static CriticalStockReport CriticalReportWithId(Guid stockItemId, Guid stockLocationId, string code, decimal available, decimal threshold)
        => new(
            [new CriticalStockReportItem(
                stockItemId, code, code, "RawMaterial", "kg", stockLocationId, "Depo",
                available, 0m, available, threshold, IsCritical: true, IsReconciled: true)],
            1);

    private static CriticalStockReport NotCriticalReportWithId(Guid stockItemId, Guid stockLocationId, string code, decimal available, decimal threshold)
        => new(
            [new CriticalStockReportItem(
                stockItemId, code, code, "RawMaterial", "kg", stockLocationId, "Depo",
                available, 0m, available, threshold, IsCritical: false, IsReconciled: true)],
            0);

    private sealed class FakeReportingService : IMenuInventoryReportingService
    {
        public CriticalStockReport NextReport { get; set; }

        public FakeReportingService(CriticalStockReport? initial = null)
        {
            NextReport = initial ?? new CriticalStockReport([], 0);
        }

        public Task<CriticalStockReport> GetCriticalStockReportAsync(CriticalStockReportQuery query, CancellationToken ct = default)
            => Task.FromResult(NextReport);

        public Task<PortionConsumptionReport> GetPortionConsumptionReportAsync(PortionConsumptionReportQuery query, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<ProductionYieldReport> GetProductionYieldReportAsync(ProductionYieldReportQuery query, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<WasteReport> GetWasteReportAsync(WasteReportQuery query, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<ActualVsTheoreticalReport> GetActualVsTheoreticalReportAsync(ActualVsTheoreticalReportQuery query, CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}

internal sealed class RecordingHubContext : IHubContext<LowStockAlertHub>
{
    public int BroadcastCount { get; private set; }

    public IHubClients Clients => new RecordingClients(() => BroadcastCount++);

    public IGroupManager Groups => throw new NotSupportedException();
}

internal sealed class RecordingClients : IHubClients
{
    private readonly Action _onSend;

    public RecordingClients(Action onSend)
    {
        _onSend = onSend;
    }

    public IClientProxy All => new RecordingProxy(_onSend);

    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
    public IClientProxy Client(string connectionId) => throw new NotSupportedException();
    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();
    public IClientProxy Group(string groupName) => throw new NotSupportedException();
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
    public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();
    public IClientProxy OthersInGroup(string groupName) => throw new NotSupportedException();
    public IClientProxy User(string userId) => throw new NotSupportedException();
    public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
}

internal sealed class RecordingProxy : IClientProxy
{
    private readonly Action _onSend;

    public RecordingProxy(Action onSend)
    {
        _onSend = onSend;
    }

    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
    {
        _onSend();
        return Task.CompletedTask;
    }
}
