# V12-CSH-001 - Implement CashSession lifecycle

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Implement terminal/cashier-bound Open, Counting, Closing, Closed and Reconciled transitions.

## Owned surface

- `src/Modules/Cash/SessionLifecycle/**`, `tests/Modules/Cash/SessionLifecycle/**`, `database/migrations/V12/V12-CSH-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- One open session policy, opening balance, counts, row version and transition permissions.

## Out of scope

- Cash payment ledger entries and notification alerts.

## Dependencies

- V12-PAY-002,V1-IAM-002,V0-DOM-001

## Deliverables

- V12-CSH-001 için production implementation.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- A terminal cannot open a second conflicting session; stale close fails; Closed cannot reopen silently.

## Handoff

- V12-CSH-002.

