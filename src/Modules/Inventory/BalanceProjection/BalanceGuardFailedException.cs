namespace ALKAROS.Inventory.BalanceProjection;

/// <summary>
/// V1-RMD-125: internal control-flow signal thrown inside an
/// <see cref="ALKAROS.Inventory.Transactions.IInventoryTransactionRunner"/>
/// operation body when <see cref="IStockBalanceRepository.TryApplyGuardedOnHandDeltaAsync"/>'s
/// guard fails, so the runner's automatic rollback-on-dispose fires before
/// the caller builds a real, informative domain exception from a fresh,
/// post-rollback read. Never crosses a service's own public API — always
/// caught internally within the same method that threw it.
/// </summary>
internal sealed class BalanceGuardFailedException : Exception
{
}
