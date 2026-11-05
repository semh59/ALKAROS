# V1-RMD-802 RESULT

Backend of the order-cleanup group: empty-order sweeper, manager hanging-order clearing, paid-bill-live-order repair, 801 test gaps, four finding closures, customer-display/queue notifications on cancel.

## Delivered
- Empty-order sweeper (`OrderCleanupStore`, `EmptyOrderSweeper`): draft Cashier/Waiter orders with no live item, no bill, untouched for 2h are cancelled by the System actor; candidate rule re-checked under the order row lock and bill locks.
- Manager API: `GET /orders/hanging`, `POST /orders/clear-hanging` (dryRun default true, reason required, required `orderIds` (400 ORDERS_NOT_CONFIRMED when missing/empty) = only the orders the manager confirmed; a confirmed order that is no longer listed answers `Changed`, age floor 6h, audit start/finish, queue "Cancelled" announce). Outcomes: Cleared, HasPayment, HasServedItems, Changed.
- Paid-bill repair (`BillClosureSweeper.RepairPaidBillOrdersAsync`): a paid bill whose order stayed live after a grace period is completed through the closure path.
- Bill cancel returns `orderId`/`orderCancelled`; order cancel and bill cancel notify queue ("Cancelled") and customer display ("OrderCancelled"/"BillCancelled").
- Menu DTO carries `allowsHalfPortion` (for 803).

## Findings closed
- F0058: bill and table pointer written in one transaction (`BillingSplitStore.cs:437`, `IBillRepository.AddAsync(bill, conn, tx)`, `ITableReleasePort.AttachBillPointerAsync`); the logged-warning path is gone.
- F0925: closure runs under the bill-settlement advisory lock (`BillClosureService.cs:25`, `IBillRepository.AcquireSettlementLockAsync`).
- F0396: cashier queue events go only to the `cashier-queue` hub group (`WaiterOrderStatusHub.cs:74`, `SignalRCashierQueueAnnouncer.cs:24`); the group is joined only with `orders.create`.
- F0451: all four payment creators call `Tender()` before `AddAsync`, so an `Initiated` payment never persists (CashTenderHandler, AccountChargeHandler, CardSettlementOrchestrator, EftTenderHandler); `Refunded` added to the settled-status theory (`PaymentAwareTableTopologyPolicyTests`), `Initiated` deliberately not added.
- 801 gaps: merged-table cancel, multi-bill cancel, deterministic two-lock-round test (pg_locks polling; a bill with an uncommitted payment opened between the lock rounds is refused with ORDER_HAS_PAYMENT).

## Tests (real Postgres, final runs `z-*`, `z2-cancel`, `final4-*`, `f451-*`)
- Billing project 201/201 (OrderCancellationHttpTests rerun 22/22 after the lock-wait query was limited to the test database); SecurityAdministration 43/43; PaymentTender 81/81; CustomerAccounts 22/22; ModuleBoundaries 31/31; PaymentTopology 13/13; WaiterNotifications 14/14; Closure 25/25; ApiConventions 17/17; TableDraft 126/126.
- CashSession 105/106: the one failure (`ClientLimitLockTests.TheRefundScreensTextAndSlipLimitsAreTheServerLimits`) concerns `split-payment.js` length limits (128 and 32), a file this task does not touch (unmodified in the working tree); it belongs to the 803 screen work.
- Red to green: each new test class first failed on the missing feature, then passed. The final run caught a real regression (host start failed when the stock service is absent because the sweeper built the store eagerly); the store is now resolved per sweep.

## Review round
- qa-backend: clear-hanging could cancel orders that aged in after the dry run (fixed: confirmed `orderIds` required, tests + mutants m20, m22); 1h age floor cancelled recent kitchen orders (floor 6h, test + m21); dead `NotCancellable` outcome removed (every hanging state can be cancelled); documentation names fixed. Pool use of the closure settlement lock: one extra connection per closure in progress, bounded by the number of tills, far below the pool size; kept. Audit-finish failure after cancellations returns 500 and each order already has its own `order.cancelled` audit record; kept.
- qa-test-kalitesi: added tests for queue announce on clear, audit actor/reason, `Changed` outcome (trigger touches the order between intent and cancel), anonymous/view-only paths, 500 contract, sweeper boundary 119/121 min, Waiter source, poison order not stopping the sweep, production timeout/interval pinned, `PartiallyRefunded` settled, `Initiated` never persisted (EFT, BankCard, cash, account). The `Initiated` asserts cannot be mutation-tested: the domain refuses `Approve` before `Tender`, so removing `Tender()` fails earlier; the asserts check the final state, which is what the paths guarantee, not an in-between write.

## Mutation (22 mutants in `mutation-summary.txt`, every file restored and cmp-verified)
All killed except m11, an equivalent mutant: the repair's completion call re-checks sibling bills itself, so the filter only saves work.

## Notes for 803
Hanging endpoints contract above; bill-cancel body `{reason, cancelOrder}` result `{orderCancelled, orderId}`; order cancel needs row version; menu `allowsHalfPortion`; clear-hanging real run body `{reason, orderIds}` (the ids from the dry-run list; required).
