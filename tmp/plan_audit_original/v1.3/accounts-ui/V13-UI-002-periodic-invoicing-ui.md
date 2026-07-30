# V13-UI-002 - Implement periodic invoicing UI

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF scope plus V0 compliance/domain correction; conditional behavior requires recorded evidence.

## Goal

Implement source preview, registered-user result, draft review, submit/status and cancellation workflow.

## Owned surface

- `src/Clients/Cashier/Invoicing/**`, `tests/Clients/Cashier/Invoicing/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Period lock, source/line trace, tax totals, QNB states, retry-safe actions and reconciliation links.

## Out of scope

- Incoming supplier invoice and general ledger accounting.

## Dependencies

- V13-INV-001,V13-INV-002,V13-INV-003,V13-INV-004,V13-QNB-001,V13-QNB-002,V13-QNB-005

## Deliverables

- V13-UI-002 için production implementation.
- Public contract/UI ve otomatik success/failure/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- User cannot resubmit PendingProvider blindly; totals trace to sources; cancellation uncertainty remains visible and unresolved.

## Handoff

- V13 exit gate.

