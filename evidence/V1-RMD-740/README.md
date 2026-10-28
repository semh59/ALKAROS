# V1-RMD-740 evidence

Fix: `ReceiveGoodsAsync` now locks the order lines (`SELECT ... FOR UPDATE`) inside the receipt transaction and rejects with `PurchaseOrderStatusException` when any received quantity differs from the value read before.

- `green.log`: Purchasing OrdersAndReceipts tests on real PostgreSQL (port 55432), 26/26, exit 0.
- `red.log` / `red2.log` + `mutations.log`: removing the lock call (M1) or neutralising the comparison (M2) fails the new tests; sources restored and verified with `cmp`.
- New tests: `ConcurrentReceiptsOfSameOrderPostStockOnce` (8 parallel receipts -> exactly one succeeds, stock movement sum 100), `StaleReceivedQuantityIsRejectedWhenLockingOrderLines` (deterministic stale read).
