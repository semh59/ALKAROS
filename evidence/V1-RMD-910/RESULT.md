# V1-RMD-910 result

Daily balance records now follow the customer's last ledger row: a customer whose last transaction is older than 730 days gets no new snapshot, and their existing snapshots are deleted (in addition to the existing 730-day row-age deletion).

- Writer: `TakeClosingSnapshotsAsync(..., activeFrom)` adds `HAVING MAX(occurred_at) >= @active_from`.
- Cleanup: new `DeleteOfInactiveCustomersAsync(activeFrom)`.
- Job: one cutoff `today - 730` shared by writer and both deleters, so nothing is written and then deleted on every run.
- Tests: store 48/48 (green-store.log), CustomerAgingHttpTests 12/12 (green-job.log). Mutations: 3 killed (mutations.txt).

Open notes: the KVKK retention policy table does not list balance records; there is no index on snapshot_date.
