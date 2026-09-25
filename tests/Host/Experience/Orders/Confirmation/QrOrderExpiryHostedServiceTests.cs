using ALKAROS.Billing.BillFoundation;
using ALKAROS.Host.Experience.Orders.OrderStockConsumption;
using ALKAROS.Host.Experience.Orders.PendingOrderConfirmation;
using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.CrossChannelReservation;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.ModifierStock;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Kitchen.TicketLifecycle;
using ALKAROS.Measurements;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Recipes.CatalogMapping;
using ALKAROS.Recipes.TheoreticalConsumption;
using ALKAROS.Recipes.Versioning;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ALKAROS.Host.Experience.Orders.Confirmation.Tests;

/// <summary>
/// V12-QRO-002: docs/design/modules/qr-nfc-ordering.md §5 — an unconfirmed
/// QR order must not hold a table Reserved forever. Exercises
/// QrOrderExpiryHostedService.ExpireOverdueOrdersAsync (the static,
/// deterministic entry point) against a real Postgres database, reusing
/// the same OrderManagementConfirmationTestDatabase and
/// PendingOrderConfirmationStore this file's sibling tests already set up.
/// </summary>
[Collection("Order confirmation PostgreSQL HTTP")]
public sealed class QrOrderExpiryHostedServiceTests : IAsyncLifetime
{
    private readonly OrderManagementConfirmationTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task AnOverdueQrOrderIsRejectedAndItsTableReleased()
    {
        var (orderId, tableId) = await _database.SeedOverdueQrPendingOrderAsync(TimeSpan.FromMinutes(10));
        var store = CreateStore();

        var expiredCount = await QrOrderExpiryHostedService.ExpireOverdueOrdersAsync(
            _database.DataSource, store, TimeSpan.FromMinutes(5), NullLogger.Instance, CancellationToken.None);

        Assert.Equal(1, expiredCount);
        var order = await _database.ReloadOrderAsync(orderId);
        Assert.Equal(OrderState.Rejected, order.Status);

        var (status, currentOrderId) = await _database.GetTableStateAsync(tableId);
        Assert.Equal("Available", status);
        Assert.Null(currentOrderId);
    }

    [Fact]
    public async Task AnOrderYoungerThanTheTimeoutIsLeftAlone()
    {
        var (orderId, tableId) = await _database.SeedOverdueQrPendingOrderAsync(TimeSpan.FromMinutes(2));
        var store = CreateStore();

        var expiredCount = await QrOrderExpiryHostedService.ExpireOverdueOrdersAsync(
            _database.DataSource, store, TimeSpan.FromMinutes(5), NullLogger.Instance, CancellationToken.None);

        Assert.Equal(0, expiredCount);
        var order = await _database.ReloadOrderAsync(orderId);
        Assert.Equal(OrderState.PendingConfirmation, order.Status);

        var (status, _) = await _database.GetTableStateAsync(tableId);
        Assert.Equal("Reserved", status);
    }

    /// <summary>A channel-agnostic order (e.g. Waiter, or NFC's own age-restricted hold) is never touched by the QR-only expiry sweep.</summary>
    [Fact]
    public async Task ANonQrPendingOrderIsNeverExpiredByThisSweep()
    {
        var (orderId, _, _) = await _database.SeedPendingConfirmationOrderAsync();
        var store = CreateStore();

        var expiredCount = await QrOrderExpiryHostedService.ExpireOverdueOrdersAsync(
            _database.DataSource, store, TimeSpan.Zero, NullLogger.Instance, CancellationToken.None);

        Assert.Equal(0, expiredCount);
        var order = await _database.ReloadOrderAsync(orderId);
        Assert.Equal(OrderState.PendingConfirmation, order.Status);
    }

    // V1-RMD-143: only RejectAsync is exercised by this file's own tests
    // (via ExpireOverdueOrdersAsync) — OrderStockConsumptionService is never
    // actually invoked (that only happens on AcceptAsync), but the
    // constructor still needs a real instance.
    private PendingOrderConfirmationStore CreateStore() => new(
        new PostgresOrderRepository(_database.DataSource),
        new PostgresKitchenTicketRepository(_database.DataSource),
        new PostgresBillRepository(_database.DataSource),
        _database.DataSource,
        new OrderStockConsumptionService(
            new PostgresProductStockMappingRepository(_database.DataSource),
            new PostgresStockItemRepository(_database.DataSource),
            new PostgresStockBalanceRepository(_database.DataSource),
            new PostgresStockMovementRepository(_database.DataSource),
            new PostgresModifierStockMappingRepository(_database.DataSource),
            new PostgresProductRecipeMappingRepository(_database.DataSource),
            new PostgresRecipeVersionRepository(_database.DataSource),
            new PostgresTheoreticalConsumptionRecordRepository(_database.DataSource),
            new UnitConverter(),
            new PostgresReservationAwareConsumptionGuard()),
        _database.CreateArbiter(),
        new PostgresProductStockMappingRepository(_database.DataSource),
        new PostgresStockItemRepository(_database.DataSource));
}
