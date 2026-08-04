# V0-DOM-005 Decision Record — approved

- Task: V0-DOM-005
- Approver: Semih
- Approval date: 2026-08-03
- Source basis: PDF:II.2.3, PDF:II.3.16, PDF:II.5.15, PDF:III.5, CORR:C5
- Access date: 2026-08-02
- Result: Approved
- Artifact: `docs/domain/table-reservation-policy.md`

## Decision summary

- `Reserved` = one pending QR order's seating lock; created only by the
  order state machine (`PendingConfirmation`), never by walk-in/personnel.
- `Accepted` → `Occupied`; `Rejected`/`Cancelled` → `Available`; optimistic
  concurrency (I.14) governs stale updates.
- No time-based expiry; walk-in uses only `Available`; transfer/merge only
  for `Occupied`.
- `current_order_id`/`current_bill_id` are cache pointers (PDF III.5.2).

## Verification

- PDF satırları: II.5.15 (state list + exclusion note, 1222-1233), III.5.2
  (tables schema + cache-pointer note, 1579-1601), CORR:C5 fix (2610-2615).
