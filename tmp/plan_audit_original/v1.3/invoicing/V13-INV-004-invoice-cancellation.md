# V13-INV-004 - Implement invoice cancellation and correction

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF scope plus V0 compliance/domain correction; conditional behavior requires recorded evidence.

## Goal

Represent allowed invoice cancellation/correction as new provider/domain actions without deleting issued invoice or double-changing account balance.

## Owned surface

- `src/Modules/Invoicing/Cancellation/**`, `tests/Modules/Invoicing/Cancellation/**`, `database/migrations/V13/V13-INV-004/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Eligibility, provider reference, local status, account effect, idempotency and reconciliation on uncertainty.

## Out of scope

- Generic payment refund and original invoice submission.

## Dependencies

- V13-INV-002,V13-QNB-002,V0-DOM-007,V0-CMP-001

## Deliverables

- V13-INV-004 için production implementation.
- Public contract/UI ve otomatik success/failure/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Issued invoice remains immutable; accepted correction creates one linked action; timeout opens reconciliation and does not assume success.

## Handoff

- V13-QNB-005 and V13-UI-002.

