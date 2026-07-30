# V11-PRD-002 - Implement production consumption and output effects

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Create ingredient Consumption and prepared-portion ProductionOutput movements in the batch transaction.

## Owned surface

- `src/Modules/Production/StockEffects/**`, `tests/Modules/Production/StockEffects/**`, `database/migrations/V11/V11-PRD-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Waste factor order, unit conversion, movement references, actual output and rollback-safe transaction.

## Out of scope

- Production planning and purchasing.

## Dependencies

- V11-PRD-001,V11-RCP-002,V11-INV-001

## Deliverables

- V11-PRD-002 için production implementation.
- Aynı davranışın otomatik başarı, ret ve concurrency/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Completed batch creates balanced traceable movements once; duplicate completion creates none; insufficient raw stock fails atomically.

## Handoff

- V11-MNU-002 and V11-INV-002.

