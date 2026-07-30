# V13-UI-003 - Implement incoming invoice matching UI

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF scope plus V0 compliance/domain correction; conditional behavior requires recorded evidence.

## Goal

Implement incoming document validation, supplier/receipt match, difference review and payable posting.

## Owned surface

- `src/Clients/Cashier/IncomingInvoices/**`, `tests/Clients/Cashier/IncomingInvoices/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Duplicate/invalid states, line match, tolerance difference, reconciliation and protected raw document access.

## Out of scope

- Outgoing customer invoicing and purchase receipt creation.

## Dependencies

- V13-QNB-003,V13-PUR-001,V1-IAM-002

## Deliverables

- V13-UI-003 için production implementation.
- Public contract/UI ve otomatik success/failure/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Duplicate cannot post payable; mismatch requires explicit action; raw PII/document access follows role policy.

## Handoff

- V13 exit gate.

