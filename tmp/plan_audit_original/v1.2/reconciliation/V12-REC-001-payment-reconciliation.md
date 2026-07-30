# V12-REC-001 - Implement payment fiscal cash and meal-card reconciliation

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Create deduplicated ReconciliationCase records when V1.2 authoritative sources diverge.

## Owned surface

- `src/Modules/Reconciliation/Payments/**`, `tests/Modules/Reconciliation/Payments/**`, `database/migrations/V12/V12-REC-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Hugin unknown, fiscal mismatch, cash difference and meal-card settlement mismatch source pairs.

## Out of scope

- QNB, online provider and unified dashboard.

## Dependencies

- V12-HUG-002,V12-HUG-003,V12-FSC-002,V12-CSH-002,V12-MCD-002

## Deliverables

- V12-REC-001 için production implementation.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Same unresolved mismatch yields one open case with both sides identified; resolution is append-only and audited.

## Handoff

- V15-REC-001 and V15-REC-002.

