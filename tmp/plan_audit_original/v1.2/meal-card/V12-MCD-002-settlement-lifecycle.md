# V12-MCD-002 - Implement MealCardSettlement lifecycle

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Group meal-card payments into provider periods and transition parent/child settlement state atomically.

## Owned surface

- `src/Modules/MealCard/Settlements/**`, `tests/Modules/MealCard/Settlements/**`, `database/migrations/V12/V12-MCD-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Period uniqueness, item membership, parent totals, child projection and disputed outcome.

## Out of scope

- Customer account and bank-card settlement.

## Dependencies

- V12-MCD-001,V0-DAT-004,V0-DOM-001

## Deliverables

- V12-MCD-002 için production implementation.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Parent Settled and child statuses cannot drift; rebuilding totals reproduces stored values; mismatch opens reconciliation.

## Handoff

- V12-REC-001.

