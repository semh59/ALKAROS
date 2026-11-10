# V1-RMD-910 reviews

## qa-veritabani
- SQL (HAVING, NOT EXISTS) correct; writer and both deleters share one cutoff.
- 3-hour gap between UTC midnight cutoff and business-day start: accepted, retention is a 730-day approximation and the same value is used everywhere.
- Backfill day for a customer who was inactive before the cutoff and later returned is skipped; snapshots are a cache (report unchanged without them, see DeletingOldSnapshotsDoesNotChangeTheReport), so accepted.
- Full-table NOT EXISTS every 6 hours uses ix_account_transactions_customer; no scale problem found.

## qa-test-kalitesi
- Boundary at exactly the active date: test added (ACustomerWhoseLastLedgerRowIsExactlyOnTheActiveDateIsStillActive), mutant 4 killed.
- Customer with no ledger rows: not tested (snapshots only exist for customers with rows); accepted.
- Remaining findings (weak NotEmpty in job test, midnight window) covered by the store tests.
