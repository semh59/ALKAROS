# V1-RMD-801 - Result

PO decision (2026-11-04): cancelling a bill can also cancel its order; a prepared line is waste, an unprepared line goes back to stock; a dine-in order can be cancelled with a reason, permission and audit, and frees its table.

## What changed
- Domain: `Order.CancelWhole` cancels every Draft/Active line and the order; an order with a served line, or past the cancellable states, is refused.
- `OrderCancellationStore` (one transaction): bill-settlement locks (id order, taken again before the money check), money check (Paid bill, allocation not refunded, payment in flight -> 409 `ORDER_HAS_PAYMENT`), order write with version check, per-line effects, open bills of the order cancelled with reason and actor, table released, audit `order.cancelled`. No audit, no cancellation.
- `OrderItemVoidEffects`: the stock/portion/kitchen-ticket effect of taking back one line, extracted from `SentItemVoidStore` and shared: Held/Sent (and never-consumed) lines give stock and daily menu portions back, Preparing/Ready lines are waste.
- Endpoint `POST .../orders/{orderId}/cancel` (`bills.cancel`, reason 1-500, `IdempotencyKey`, `ExpectedRowVersion`). `POST .../billing/bills/{id}/cancel` takes `CancelOrder`; with it the order is cancelled in the same transaction (`orderCancelled` in the answer).
- B11: one cancel statement in the Billing module (`CancelBillAsync`, `CancelOpenBillsOfOrderAsync`, `CancelActiveBillsForOrderAsync` share it); `BillCancellationStore`, `PaymentRefunds` and the order cancellation call it. Table pointer and release live in the Tables module (`ITableReleasePort`); `OrderSettlementService`, bill cancel, refund and order cancel no longer write `table_mgmt`. Settling an order now also forgets its sent-check record (it used to stay). One money guard class (`BillMoneyGuard`) replaces the lock and money-check copies.
- Architecture lock (`HostForeignWriteLockTests`): bills are cancelled only by the Billing repository (any spelling of an `UPDATE billing.bills ... 'Cancelled'`), and Host files that write table rows are a fixed list with per-file counts that can only shrink. The three affected Host areas got the Tables edge in `ApprovedHostOrchestrationEdges` and the dependency record.
- Carried from 800: `OrderQuantityRule` boundary tests for the flag-off path and half steps far beyond the order limit (the rule has no multiplication left to overflow).

## Evidence (real Postgres docker alkaros-test-pg; password redacted)
- Tests written against raw JSON, so the endpoint tests fail with 404 on the old code; the project does not compile on the old code (new types), so the 17 mutants stand in for the red run.
- Green (final run `f2-*`, every exit=0; counts in each test.log): Billing project in full, Orders domain, ModuleBoundaries, ApiConventions, Tables, Billing PaymentClosure, VoidSent, Void, Comp, Confirmation, TableDraft, SecurityAdministration, PaymentTender. Skipped: none.
- Mutation (`mutation-summary.txt`): 17 mutants (m17: lock of the order bills skipped), all killed, every file restored with cmp. Three mutants first failed to compile and one survived because the domain message also matched the old text; they were rewritten and the domain test sharpened (`old/` keeps the invalid runs).
- plan-audit and consistency-audit: 0 errors / 0 warnings.

## Reviews
- qa-backend: lock order of a bill cancel with the order flag (all order bills now locked first, in id order), a bill opened after the first locks (locked again before the money check), settlement now forgets the sent-check record (test added), `BillCancellationStore` took optional/new-ed collaborators (now injected), missing 404 mapping for an unknown order on the bill route (added).
- qa-test-kalitesi: lock widened to any `billing.bills` update that names `'Cancelled'`, per-file write counts, fully refunded payment does not block, order without a bill and with a Draft line, cancellation waits for a held bill-settlement lock, exact exception in the audit test, replay does not return stock twice, mutation summary regenerated. Not tested: a cancel on a merged table (the release logic is the code the settlement tests already cover), a bill opened between the two lock rounds (cannot be driven deterministically; the second round mirrors the hand-off store).

## Left for the next tasks
- Hanging orders, empty-order sweeper, Paid bill repair, atomic closure (V1-RMD-802); screens (V1-RMD-803).
