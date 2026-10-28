# V1-RMD-748 evidence

Fix: the transactional `ReverseMovementAsync` applies the reversal with `TryApplyGuardedOnHandDeltaAsync` and throws the typed `ReversalWouldMakeStockNegativeException` (a `StockMovementReversalException`, already handled by `SentItemVoidStore`) instead of letting an unguarded update drive on-hand negative.

- `red.log`: before the fix, reversing a receipt that had been partly consumed was not refused (Expected the typed exception, none was thrown).
- `green.log`: MovementReversal tests on real PostgreSQL (port 55432), 23/23, exit 0.
- `red2.log` + `mutations.log`: with the unguarded call restored the test fails again; source restored and verified with `cmp`.
- Not covered (separate decision, no production caller): the non-transactional `ReverseMovementAsync` overload still writes the ledger and the balance in two steps.
