# V11-RCP-002 - Implement reproducible recipe cost snapshots

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Persist the ingredient-level cost basis required to reproduce historical recipe cost.

## Owned surface

- `src/Modules/Recipes/CostSnapshots/**`, `tests/Modules/Recipes/CostSnapshots/**`, `database/migrations/V11/V11-RCP-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Ingredient price source, quantity after waste factor, conversion, currency and snapshot lines.

## Out of scope

- Supplier-account posting and production batch execution.

## Dependencies

- V11-RCP-001,V11-UNT-001

## Deliverables

- V11-RCP-002 için production implementation.
- Aynı davranışın otomatik başarı, ret ve concurrency/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Snapshot total recomputes from immutable lines and remains unchanged after later price/unit updates.

## Handoff

- V11-PRD-001 and V13-PUR-001.

