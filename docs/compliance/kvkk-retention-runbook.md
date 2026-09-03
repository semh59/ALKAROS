# KVKK retention / anonymization runbook

> Source basis: PDF:I.30-I.33, PDF:II.11-II.12, PO:2026-09-01
> Owner task: V1-RMD-094 · Inventory authority: `evidence/v0/compliance/V0-CMP-003/kvkk-data-inventory.md`

The `kvkk-retention` verb anonymizes personal data once it is past the retention
window set by the approved `V0-CMP-003` inventory. It is the V1 core of
`V15-KVK-001`; the multi-store checkpoint/resume workflow is `V15-KVK-002`.

## What it anonymizes

| Class | Table / columns | Window | Action |
| --- | --- | --- | --- |
| Staff record | `identity.users` — `username`, `display_name`, `email`, `phone`, `password_hash` | `active = false` **and** `updated_at` older than **1 year** (employment + 1 yr) | `username -> 'anon-<id8>'`, `display_name -> '[anonymized]'`, `email`/`phone -> NULL`, `password_hash -> '!kvkk-retention-disabled'`. The row is kept for referential integrity (roles, audit actor, reservation actor). |
| Order note | `orders.orders.notes` | order `status IN (Completed, Cancelled, Rejected)` **and** `created_at` older than **5 years** | `notes -> '[anonymized]'` |
| Order line note | `orders.order_items.notes` | parent order past the same window | `notes -> '[anonymized]'` |
| Reservation reason | `table_mgmt.table_reservations.reason`, `release_reason` | `status IN (Claimed, Cancelled, Expired)` **and** `reserved_at` older than **5 years** | both `-> '[anonymized]'` |

### Audit events — out of scope here

The `V0-CMP-003` inventory lists audit logs as *anonymize after 10 years*, but
`audit.audit_events` is enforced **append-only** by a database trigger (AUD-01):
`UPDATE`/`DELETE` are rejected. In-place anonymization is therefore impossible by
design. The 10-year disposal for audit needs a partition-drop approach (partition
the table by year, drop whole old partitions) and is deferred to `V15-KVK-002`.
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

Pass `--exclude-order-ids-file <path>` — a text file of order GUIDs, one per
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
                as_of=<date> excluded_orders=<n> apply=false
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
