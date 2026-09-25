using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.CrossChannelReservation;
using ALKAROS.Inventory.ManualAdjustments;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.MovementReversal;
using ALKAROS.Inventory.PortionReservations.CancellationEffects;
using ALKAROS.Inventory.PortionReservations.Concurrency;
using ALKAROS.Inventory.PortionReservations.Lifecycle;
using ALKAROS.Inventory.ReservationBalanceProjection;
using ALKAROS.Inventory.ModifierStock;
using ALKAROS.Inventory.PhysicalCounts;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Inventory.Transactions;
using ALKAROS.Inventory.WasteRecording;
using ALKAROS.Measurements;
using ALKAROS.ModuleComposition;

namespace ALKAROS.Inventory;

/// <summary>
/// Composition module for the shared portion stock pool (V1.1: immutable
/// stock ledger, balance/reservation projections, waste and manual
/// adjustments, portion reservation lifecycle and arbitration).
/// </summary>
public sealed class InventoryModule : IModule
{
    public string Id => "Inventory";

    public string DisplayName => "Inventory";

    // No direct-call edge: the only cross-module type Inventory used to need
    // (IUnitConverter) moved to the shared ALKAROS.Measurements building
    // block, so Inventory no longer references any other module's project.
    public IReadOnlyCollection<string> DependsOn => Array.Empty<string>();

    public void Register(ModuleContext context)
    {
        context.RegisterTransient<IUnitConverter, UnitConverter>();
        // V1-RMD-125: shared atomic-transaction seam for InventoryAdjustmentService/
        // WasteRecordingService — see IInventoryTransactionRunner's own doc-comment.
        context.RegisterTransient<IInventoryTransactionRunner, PostgresInventoryTransactionRunner>();

        context.RegisterTransient<IStockItemRepository, PostgresStockItemRepository>();
        context.RegisterTransient<IStockLocationRepository, PostgresStockLocationRepository>();
        context.RegisterTransient<IProductStockMappingRepository, PostgresProductStockMappingRepository>();
        // V1-RMD-152: what a modifier draws from the store room.
        context.RegisterTransient<IModifierStockMappingRepository, PostgresModifierStockMappingRepository>();
        context.RegisterTransient<IStockMasterService, StockMasterService>();

        context.RegisterTransient<IStockMovementRepository, PostgresStockMovementRepository>();
        context.RegisterTransient<IStockMovementService, StockMovementService>();

        context.RegisterTransient<IStockBalanceRepository, PostgresStockBalanceRepository>();
        context.RegisterTransient<IStockBalanceProjector, StockBalanceProjector>();
        context.RegisterTransient<IStockMovementReversalService, StockMovementReversalService>();

        context.RegisterTransient<IWasteRecordRepository, PostgresWasteRecordRepository>();
        context.RegisterTransient<IWasteRecordingService, WasteRecordingService>();
        context.RegisterTransient<IInventoryAdjustmentService, InventoryAdjustmentService>();

        context.RegisterTransient<IPhysicalCountRepository, PostgresPhysicalCountRepository>();
        context.RegisterTransient<IPhysicalCountService, PhysicalCountService>();

        context.RegisterTransient<IPortionReservationRepository, PostgresPortionReservationRepository>();
        context.RegisterTransient<IPortionReservationLifecycleService, PortionReservationLifecycleService>();
        context.RegisterTransient<IPortionReservationArbitratorRepository, PostgresPortionReservationArbitratorRepository>();
        context.RegisterTransient<IPortionReservationArbitrator, PortionReservationArbitrator>();

        context.RegisterTransient<IReservationBalanceRepository, PostgresReservationBalanceRepository>();
        context.RegisterTransient<IReservationBalanceProjector, ReservationBalanceProjector>();

        context.RegisterTransient<IKitchenItemStateProvider, PostgresKitchenItemStateProvider>();
        context.RegisterTransient<IPortionCancellationDecisionService, PortionCancellationDecisionService>();

        // V12-STK-001: one reservation command for every channel, and the consumption-side guard
        // that stops a direct sale from taking a portion another order holds.
        context.RegisterTransient<ICrossChannelPortionArbiter, PostgresCrossChannelPortionArbiter>();
        context.RegisterTransient<IReservationAwareConsumptionGuard, PostgresReservationAwareConsumptionGuard>();
    }
}
