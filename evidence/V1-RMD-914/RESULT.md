# V1-RMD-914 result

Migration 224 adds ix_balance_snapshots_date on customer_account.balance_snapshots (snapshot_date): the maintenance job's old-record delete and its read of the existing days no longer scan the whole table (the only index was (customer_id, snapshot_date)). The down migration drops it. Registered in order.json (phase B, max 224), MigrationManifest.PhaseBMax and ManifestTests (ids, count 223, last entry).

- Test: TheDailyBalanceRecordsHaveADateIndexAndRollingBackRemovesItAndItAppliesOnceMore (shape snapshot_date, non-unique, full; rollback; reapply). Red before (red.log); green: ManifestTests + new test 18/18 (green-1.log), migration schema/file tests 42/42 (green-2.log).
- Mutations: 4 killed (mutations.txt).
