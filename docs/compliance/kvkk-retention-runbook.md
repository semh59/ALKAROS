# KVKK retention / anonymization runbook

> Source basis: PDF:I.30-I.33, PDF:II.11-II.12, PO:2026-09-01
> Owner task: V1-RMD-094 · Inventory authority: `evidence/v0/compliance/V0-CMP-003/kvkk-data-inventory.md`

The `kvkk-retention` verb anonymizes personal data once it is past the retention
window set by the approved `V0-CMP-003` inventory. Which records are due is decided
by the `Privacy.RetentionExecution` module (`V15-KVK-001`) from a versioned policy
(`privacy.retention_rules`, policy 1 = the approved inventory); the dry run and the
apply always pick the same records. The multi-store checkpoint/resume workflow is
`Privacy.Anonymization` (`V15-KVK-002`).

## What it anonymizes

| Class | Table / columns | Window | Action |
| --- | --- | --- | --- |
| Staff record | `identity.users` — `username`, `display_name`, `email`, `phone`, `password_hash` | `active = false` **and** `updated_at` older than **1 year** (employment + 1 yr) | `username -> 'anon-<id8>'`, `display_name -> '[anonymized]'`, `email`/`phone -> NULL`, `password_hash -> '!kvkk-retention-disabled'`. The row is kept for referential integrity (roles, audit actor, reservation actor). |
| Order note | `orders.orders.notes` | order `status IN (Completed, Cancelled, Rejected)` **and** `created_at` older than **5 years** | `notes -> '[anonymized]'` |
| Order line note | `orders.order_items.notes` | parent order past the same window | `notes -> '[anonymized]'` |
| Reservation reason | `table_mgmt.table_reservations.reason`, `release_reason` | `status IN (Claimed, Cancelled, Expired)` **and** `reserved_at` older than **5 years** | both `-> '[anonymized]'` |

### Customers and suppliers

Customer profiles (10 years after the last ledger movement or invoice; never with an open
balance or a pending anonymization request) and suppliers (10 years after the last purchase
order) past their window are written as pending work items in `privacy.retention_work_items`
and then anonymized by the same `--apply` run:

| Class | Store | Action |
| --- | --- | --- |
| Customer | `customer_data.profiles` | the encrypted contact envelope is replaced by a value no key can open; `anonymized = true`. The row and id stay. |
| Customer | `customer_data.anonymization_requests` | any open request becomes `Anonymized`. |
| Supplier | `purchasing.suppliers` | `name -> '[anonymized]'`, `phone` and `email` cleared. `tax_number` and `tax_office` stay (the supplier's legal identifiers on incoming invoices). |

A customer whose balance became non-zero after being queued is held back (the run reports `blocked=N`) and picked up
again once the balance is settled. The account ledger (`customer_account.account_transactions`, append-only) and the invoice
buyer data are legal retention and are never touched.

### How a record is processed

Every record is one job in `privacy.anonymization_jobs`. Each store it touches is a step: the write, its checkpoint
(`privacy.anonymization_checkpoints`) and an event (`privacy.anonymization_events`, append-only, no personal data) commit together.
If a step fails the job is `Failed` with the error class (e.g. an SQLSTATE) and the work item stays pending; the next
`--apply` resumes at the first store without a checkpoint and does not repeat the finished ones. After the last store every
store is checked for leftovers; only a clean record is marked done in the retention list (`failed=N` otherwise, and the
command exits with an error). A legal hold placed meanwhile also stops the job (`blocked`) before the work item is completed.

### Audit events — out of scope here

The `V0-CMP-003` inventory lists audit logs as *anonymize after 10 years*, but
`audit.audit_events` is enforced **append-only** by a database trigger (AUD-01):
`UPDATE`/`DELETE` are rejected. In-place anonymization is therefore impossible by
design. The 10-year disposal for audit needs a partition-drop approach (partition
the table by year, drop whole old partitions) and is deferred (planned as `V1-RMD-481`).
`IAuditSanitizer` already redacts secrets/PII patterns from audit payloads at
write time, so the highest-risk data is not stored in the first place.

The financial columns of orders and bills are **not** touched — the row stays a
complete financial record, only the free-text / identifying fields are scrubbed.

## What it never touches (legal retention)

Per the `V0-CMP-003` disposal column, these are **retain (legal)** and are out
of scope entirely:

- Fiscal receipt data, Z reports (`Fiscal` module)
- Invoice data — customer name, tax ID, amount (`Accounts` module)
- The financial amounts on `orders` / `bills`

Provider raw payload retention is a separate workstream (`V15-SEC-003`).

## Legal hold

A legal hold placed in `privacy.legal_holds` (per data class and record) keeps that record out
of every plan and run, the database refuses a work item for a held record, and a held record's
work item cannot be completed until the hold is released. For orders you can also pass
`--exclude-order-ids-file <path>` — a text file of order GUIDs, one per
line (`#` comments allowed). Any order in that list, and its line items, are
skipped (litigation, tax audit, active dispute). Maintain this list from your
open-cases register before every run.

## Running it

**Always dry-run first** (this is the default — it changes nothing and rolls
back its own transaction):

```sh
dotnet ALKAROS.Host.dll kvkk-retention --db-url postgresql://alkaros@postgres:5432/alkaros \
  [--as-of 2026-09-01] [--exclude-order-ids-file /opt/alkaros/kvkk-hold.txt]
```

Output:

```text
kvkk-retention: staff=<n> order_notes=<n> item_notes=<n> reservation_reasons=<n> \
                customers=<n> suppliers=<n> policy=<version> as_of=<date> excluded_orders=<n> apply=false
```

Review the counts. When they look right, add `--apply` to write. The verb is
**idempotent** — a second `--apply` run finds nothing already-anonymized and
reports all zeros. `--as-of` overrides the reference date (default: now).

### Suggested schedule

Quarterly, outside trading hours, with the hold list refreshed first:

```cron
# 05:00 on the 1st of Jan/Apr/Jul/Oct
0 5 1 1,4,7,10 * cd /opt/alkaros && \
  dotnet ALKAROS.Host.dll kvkk-retention --db-url ... --exclude-order-ids-file /opt/alkaros/kvkk-hold.txt --apply \
  >> /var/log/alkaros-kvkk-retention.log 2>&1
```

Because every window is 5-10 years, the first several runs of a new deployment
will report all zeros — that is expected.

## Verification

`evidence/V1-RMD-094/` holds a dry-run + apply transcript and the
`KvkkRetentionTests` result (old rows anonymized, recent / active / wrong-status
/ legally-held rows untouched, second apply a no-op).

## Audit log disposal (10 years)

`audit.audit_events` is append-only (a trigger refuses UPDATE and DELETE), so its rows are never edited. The table is split into one partition per
calendar year (`audit.audit_events_y2026`, ...) and a year is removed as a whole:

```
dotnet ALKAROS.Host.dll audit-disposal --db-url ...                  # dry run: lists partitions, rows and which are expired
dotnet ALKAROS.Host.dll audit-disposal --db-url ... --as-of 2040-01-01   # preview for a date (never combined with --apply)
dotnet ALKAROS.Host.dll audit-disposal --db-url ... --apply          # drops expired partitions
```

- Retention counts from the end of the year: year Y is expired on 1 January of Y+11 (UTC). Nothing can expire before 1 January 2031 (first partition 2020), and the log of 2026 is kept until 2037.
- Each drop writes an `audit.partition.disposed` event with the partition name, year and row count only.
- `audit_events_default` holds any date outside the pre-created years (2020-2060); it is never dropped by the command. After 2060 new partitions must be created before the year starts.
- There is no legal-hold class for the audit log; a hold would have to be added to `Privacy.RetentionExecution` first.
- Run it with the same cron cadence as `kvkk-retention` (at least every six months).
