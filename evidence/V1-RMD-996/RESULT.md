# V1-RMD-996 Result

- Waiter table-draft on a table whose order has a non-cancelled bill: 409 TABLE_BILL_OPEN with a Turkish message; nothing reaches the kitchen. Red proof: red-a.log (Expected Conflict, Actual OK). Replay of the same submission id still returns the stored order (gate is after the replay branch). The waiter PWA shows the server message as is (api.js error.message) and the offline queue treats this 409 as final, not transient.
- ClearBillPointerAsync re-selects the table's other open bill instead of NULL.
- StartOrderAsync "table has bill" 409 now carries a Turkish UserMessage.
- Empty quick-sale draft discard (993) now goes through Order.CancelWhole and the order repository instead of raw SQL on the orders schema (consistency-audit now clean); 993 tests still pass (DualScreenStoreTests 44/44).
- HostForeignWriteLockTests expectation for CashierHandoffStore 4 -> 5 (the pointer clear added in 984 had left that test red); 14/14.
- Runs: TableDraft 132/132, Billing MergedBillOrderLink 19, OrderCancellation 22, Billing split/refund/cancel 196, DualScreenStoreTests 44. Mutations M1-M3 killed (mutations.txt).
- Pre-existing red architecture tests not from this task: EveryHostAreaIsCoveredByAnApprovedEdgeEntry (CustomerAging), HostOrchestrationEdgesStayWithinTheApprovedList (Authorization). Reported separately.
