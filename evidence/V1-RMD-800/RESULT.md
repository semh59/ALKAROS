# V1-RMD-800 - Result

PO decision (2026-11-04): a product is sold in whole units unless its form says "yarim porsiyon olur"; only then 1.5-style quantities are accepted.

## What changed
- Migration 216: `catalog.products.allows_half_portion` (default false). Products already sold in fractions (`orders.order_items.quantity` with a fractional part) start with the flag on, so existing half-portion selling is not cut off (up/down/up/up: `migration-forward-backward.log`).
- `Product` and the catalog repository carry the flag; the management API returns it (list, create) and the product details update sets it (omitted = unchanged).
- One rule: `OrderQuantityRule` (whole number; multiple of 0.5 when the product allows it). Applied in the till add and change-quantity paths (`DualScreenStore`), the table draft (`TableDraftService`; the old minimum-quantity check is covered by the rule and removed) and the refund pricing (`PaymentRefunds`). Refused orders answer 400 `ORDER_QUANTITY_NOT_ALLOWED` with a Turkish reason; refused refunds 400 `REFUND_QUANTITY_NOT_WHOLE`.
- Refund backward compatibility: a line sold in fractions keeps fractional refunds, and the fraction left of a line already refunded in halves can still be refunded after the flag is switched off. Stored refund requests are never revalidated.
- Daily menu counters (NUMERIC(10,3)) already count fractions; new tests prove reserve 2.5, sell 1.5, refund share 0.5, void 1.5 (returned) and 2.5 (wasted) against rebuild and drift. No counter code changed.
- Customer channels (QR, NFC) carry `int` quantities and online providers set their own; unchanged.

## Evidence (real Postgres docker alkaros-test-pg; password redacted)
- Red against baseline code (`red/`, sources restored with cmp): refund 2 of 4 fail, table draft 4 of 8, till 5 of 9. The catalog/domain tests cannot compile on the old code; mutation stands in for them.
- Green: Catalog host 24, Catalog module 84, Orders domain 174+14, TableDraft 123 (+8 new), Billing 152, VoidSent 30, Void 5, Comp 21, Confirmation 39, Nfc 29, Qr 35, OnlineOrdering host 202, four online module projects 14/16/33/24, Menu CounterProjection 34, Host.Errors 225, Host.Tests Manifest + MigrationSchema + DualScreenHalfPortion (final run `v3-host`, `v4-host`: 17 migration tests incl. the new backfill/rollback test), ApiConventions 17, ModuleBoundaries 17. Skipped: none. Not run: the rest of Host.Tests (a full run was killed by memory pressure; only the classes touching this change ran).
- Mutation (`mutation-summary*.txt`): rounds 1-3, 20 mutants, all killed (round 2 added the review fixes, round 3 the test-review gaps: draft exemption by id only, create ignores flag, list reads false, wrong backfill condition), every file restored with cmp (rule always true, no half branch, tenth steps, each of the four call sites, refund legacy line, refund finish-the-rest, refund flag ignored, repository update skips column, details ignores flag, details null resets).
- plan-audit and consistency-audit 0 errors / 0 warnings. The scope tool is run from the latest scope commit (13b7ff31); it lists only another session's customer-display files, which are not part of this task.

## Review outcomes
- Backend review: refund rule on flagged fractional lines, table-draft re-save after the flag is switched off, merged-total check on the till add path, formatting - all fixed with tests and mutants. The flag is read without a row lock on the till path; a flag switched off between the read and the write can let one already-validated line through, loses no data and is bounded by the next save (accepted, documented here).
- Test review: automated migration backfill/rollback test added; draft quantity-change on a held line (two cases); create and list carry the flag; refund tests assert amounts; till merge total (1 + 0.5 = 1.5); overflow-free rule (`quantity % step`, no multiplication); two older migration tests that hard-coded rollback of 210 as the newest now unwind from the manifest (they failed on the existing 211-215 chain).
- A product row missing at quantity-change time is treated as whole-units-only (the rule cannot be proven, so the safer answer).

## Open notes for the screens task
- WaiterPwa steps every line in 0.5 (`waiter-app.js` stepper and product sheet) and must read the flag; the sale product list needs the flag in the till catalog contract (`DualScreenContracts.cs`, another task's file while customer-display work is open).
- The client error dictionaries need `ORDER_QUANTITY_NOT_ALLOWED` and `REFUND_QUANTITY_NOT_WHOLE`.
