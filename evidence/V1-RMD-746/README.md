# V1-RMD-746 evidence

Fix: `RaiseAlertAsync` takes a transaction-level advisory lock per deduplication identity (dedup key, else type + source reference) before looking for an active alert, so concurrent first raises serialize and only one opens an alert. No migration.

- `red.log`: before the fix 8 concurrent raises produced 8 new alerts (expected 1).
- `green.log`: AlertFoundation tests on real PostgreSQL (port 55432), 13/13, exit 0.
- `red2.log` + `mutations.log`: with the lock disabled the test fails again; source restored and verified with `cmp`.
