# V11-PRD-001 - Implement ProductionBatch lifecycle

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Implement planned, in-progress, completed and cancelled batches bound to one immutable RecipeVersion.

## Owned surface

- `src/Modules/Production/BatchLifecycle/**`, `tests/Modules/Production/BatchLifecycle/**`, `database/migrations/V11/V11-PRD-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Batch transitions, planned/actual quantity and immutable recipe link.

## Out of scope

- Ingredient consumption, stock movement and daily-menu counters.

## Dependencies

- V11-RCP-001,V11-MNU-001,V0-DOM-001

## Deliverables

- V11-PRD-001 için production implementation.
- Aynı davranışın otomatik başarı, ret ve concurrency/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Completed/cancelled batch rejects recipe reassignment; forbidden lifecycle transitions fail without side effects.

## Handoff

- V11-PRD-002.

