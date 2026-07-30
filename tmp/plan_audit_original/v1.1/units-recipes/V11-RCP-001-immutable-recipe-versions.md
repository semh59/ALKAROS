# V11-RCP-001 - Implement immutable RecipeVersion lifecycle

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Implement recipes, version creation, activation and retirement with immutability after operational use.

## Owned surface

- `src/Modules/Recipes/Versioning/**`, `tests/Modules/Recipes/Versioning/**`, `database/migrations/V11/V11-RCP-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Recipe ingredients, version uniqueness, one active period and mutation prohibition after batch reference.

## Out of scope

- Cost calculation and production execution.

## Dependencies

- V11-UNT-001,V0-DOM-001

## Deliverables

- V11-RCP-001 için production implementation.
- Aynı davranışın otomatik başarı, ret ve concurrency/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- A referenced RecipeVersion cannot change or delete; a new version preserves old production inputs.

## Handoff

- V11-RCP-002, V11-MNU-001 and V11-PRD-001.

