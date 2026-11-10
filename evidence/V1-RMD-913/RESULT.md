# V1-RMD-913 result

docs/compliance/kvkk-retention-runbook.md now has a section "Customer balance records (end-of-day balances)": table customer_account.balance_snapshots, delete (not anonymize), two rules counted back 730 days (record age; customer's last ledger row), the 6-hour balance-snapshot job, and what is never touched (ledger, current balance, invoices). Before: the runbook did not mention the table (red.txt: 0 occurrences). The numbers match BalanceSnapshotMaintenanceJob (RetentionDays = 730, WindowDays = 100, Interval 6 hours).

Also, as instructed: V1-RMD-908 RESULT now states that the limit of 2 receipt resets is an assumption of the implementer, not a product decision; it stays a constant, not a manager setting.
