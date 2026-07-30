# V11-UNT-001 - Implement dimension-safe units and conversions

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Implement unit definitions, dimensions and deterministic conversions that reject cross-dimension and inconsistent cycles.

## Owned surface

- `src/Modules/Recipes/Units/**`, `tests/Modules/Recipes/Units/**`, `database/migrations/V11/V11-UNT-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Unit registry, dimension checks, inverse conversion, precision and rounding.

## Out of scope

- Recipe versioning and stock balance mutation.

## Dependencies

- V1.1 entry gate,V0-CMP-002

## Deliverables

- V11-UNT-001 için production implementation.
- Aynı davranışın otomatik başarı, ret ve concurrency/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- kg-g and liter-ml conversions are reversible within declared tolerance; kg-liter and contradictory cycles are rejected.

## Handoff

- V11-RCP-001 and V11-PRD-002.

