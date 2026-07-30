# V11-MNU-003 - Implement static Menu composition

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF V1.1 module/schema sections plus named correction dependency.

## Goal

Implement reusable Menu/MenuItem composition that selects catalog products without owning price or stock.

## Owned surface

- `src/Modules/Menu/StaticMenu/**`, `tests/Modules/Menu/StaticMenu/**`, `database/migrations/V11/V11-MNU-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Menu identity, item ordering, active state and catalog references.

## Out of scope

- Daily availability, pricing, production and UI.

## Dependencies

- V1-CAT-001

## Deliverables

- V11-MNU-003 için production implementation.
- Public contract ve otomatik başarı/ret/concurrency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Duplicate product in one menu is rejected; catalog deactivation has explicit display behavior without deleting history.

## Handoff

- V11-MNU-001 and V11-UI-001.

