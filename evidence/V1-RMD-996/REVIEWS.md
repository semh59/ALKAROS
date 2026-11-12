# Reviews

## qa-backend
No race, replay or data finding: the order row is locked FOR UPDATE before the bill check and the bill creation path locks the same row; CancelWhole persists cancelled_at and history. Low: the empty-draft discard now reads then saves with row version, so a concurrent item add would raise a stale-version error instead of skipping; accepted: the terminal row lock already serializes that terminal's order edits and no data is lost.

## qa-sinir-mimari
consistency-audit clean; boundary tests: found the CashierHandoffStore foreign write count (984) which is fixed here; the two other red tests are older and unrelated. Notes: HasBillAsync now shared across two Experience.Orders sub-areas (same assembly), Tables already reads billing.bills.
