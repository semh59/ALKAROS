# V1-RMD-934 Reviews

## qa-backend
No side effect: online orders never get a bill and are completed at hand-over, so only genuinely undelivered ones count; preview, close and audit read the same SQL. Noted, handled outside this task: the cash-session screen text says "open bills" although the count now includes bill-less sent orders (listed for the wording task); the end-of-day report counts bills only (own task, next).

## qa-test-kalitesi
Test widened: preparing and ready orders, completed and cancelled online orders; mutants for preparing and ready added. Accepted: no online order with a bill (the bill-exists clause is source-independent SQL covered by the cashier-billed case).
