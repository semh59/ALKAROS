# V12-FSC-003 - Implement approved adisyon or e-Adisyon strategy

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF scope plus V0 compliance/domain correction; conditional behavior requires recorded evidence.

## Goal

Implement the document opening/update/closure behavior selected by V0-CMP-001 without assuming QNB or T300 ownership.

## Owned surface

- `src/Modules/Fiscal/AdisyonStrategy/**`, `tests/Modules/Fiscal/AdisyonStrategy/**`, `database/migrations/V12/V12-FSC-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Applicable document identity, order/item updates, final fiscal/invoice reference, retry, archive and reconciliation.

## Out of scope

- Provider transport not validated by V0 evidence and unrelated invoice generation.

## Dependencies

- V0-CMP-001,V0-HUG-001,V0-QNB-001,V1-ORD-001,V12-FSC-001

## Deliverables

- V12-FSC-003 için production implementation.
- Public contract/UI ve otomatik success/failure/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- If applicable, official contract tests prove lifecycle and final reference; if not applicable, V0 evidence explicitly closes the requirement before this task starts.

## Handoff

- V20-CMP-001.
