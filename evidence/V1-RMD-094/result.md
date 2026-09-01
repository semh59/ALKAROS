# V1-RMD-094 - KVKK retention anonymization job result

- Date: 2026-09-01
- Owner: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Approved by Semih (PO:2026-09-01) to pull the V1-relevant core of
  `V15-KVK-001` forward into V1.

## Deliverable

`src/Host/Program.cs` — new `kvkk-retention` verb:

```
kvkk-retention --db-url <url> [--apply] [--as-of <ISO date>] [--exclude-order-ids-file <path>]
```

Default is a **dry run** (opens a transaction, counts, rolls back — cannot
change data). `--apply` runs the anonymization UPDATEs in one transaction.
Idempotent (an already-`[anonymized]` row is skipped). `--exclude-order-ids-file`
is the legal-hold list.

### Classes anonymized (from the V0-CMP-003 inventory)

| Class | Table / columns | Window | Action |
| --- | --- | --- | --- |
| Staff | `identity.users` (username, display_name, email, phone, password_hash) | `active = false` and `updated_at` > 1 yr | mask username/display_name, null email/phone, disable password_hash |
| Order note | `orders.orders.notes` | terminal status, `created_at` > 5 yr, not legally held | `-> '[anonymized]'` |
| Order line note | `orders.order_items.notes` | parent order past the same window | `-> '[anonymized]'` |
| Reservation reason | `table_mgmt.table_reservations.reason`, `release_reason` | closed status, `reserved_at` > 5 yr | `-> '[anonymized]'` |

### Not touched

- Fiscal receipts, Z reports, invoices, financial amounts — legal retention.
- `audit.audit_events` — enforced **append-only** by a DB trigger (AUD-01); the
  first `--apply` attempt to anonymize it raised
  `P0001: audit_events table is append-only`. In-place anonymization is
  impossible by design; the 10-year disposal needs a partition-drop approach and
  is deferred to `V15-KVK-002`. `IAuditSanitizer` already redacts secrets on
  write. Documented in `docs/compliance/kvkk-retention-runbook.md`.

## Tests

`tests/Host/MigrationComposition/Program/KvkkRetentionTests.cs` — real test DB:

- `DryRunReportsCountsButChangesNothingThenApplyAnonymizesAndIsIdempotent`:
  seeds old + recent + wrong-status + legally-held rows for every class; the
  dry run reports `staff=1 order_notes=1 item_notes=1 reservation_reasons=1
  apply=false` and changes nothing; `--apply` anonymizes exactly the old rows
  and leaves recent / active / Draft / legally-held rows untouched; a second
  `--apply` reports all zeros.
- `MissingDbUrlFailsClosed`, `UnknownArgumentFailsClosed`: exit 2.

Result (Docker `alkaros-sdk10-rt8` + `alkaros-pg`):

```
dotnet test tests/Host/MigrationComposition --filter FullyQualifiedName~KvkkRetention
Passed!  - Failed: 0, Passed: 3, Skipped: 0, Total: 3
```

## Runbook

`docs/compliance/kvkk-retention-runbook.md` — class table, retention windows,
what is never touched (fiscal/invoice/financial + append-only audit), legal-hold
file format, dry-run-first procedure, quarterly cron. Notes that a fresh
deployment will report all zeros for years, which is expected.

## Scope

V1 single-store idempotent core of `V15-KVK-001`. The multi-store
resumable/checkpoint workflow and audit partition-drop are `V15-KVK-002`.
