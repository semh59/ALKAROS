# V13-QNB-005 - Implement QNB invoice cancellation transport

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF scope plus V0 compliance/domain correction; conditional behavior requires recorded evidence.

## Goal

Map the approved QNB cancellation/correction operation and query uncertain results.

## Owned surface

- `src/Modules/Invoicing/Qnb/Cancellation/**`, `tests/Modules/Invoicing/Qnb/Cancellation/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Official request/response, idempotency, provider status query, sanitized evidence and local action reference.

## Out of scope

- Cancellation eligibility/accounting calculation.

## Dependencies

- V13-INV-004,V0-QNB-001

## Deliverables

- V13-QNB-005 için production implementation.
- Public contract/UI ve otomatik success/failure/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Sandbox evidence covers accepted, rejected and timeout/query outcomes; retry creates one provider action.

## Handoff

- V13-QNB-004.

