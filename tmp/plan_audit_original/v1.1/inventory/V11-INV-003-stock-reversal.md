# V11-INV-003 - Implement compensating StockMovement reversal

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Create one idempotent Reversal movement linked to the exact original movement.

## Owned surface

- `src/Modules/Inventory/MovementReversal/**`, `tests/Modules/Inventory/MovementReversal/**`, `database/migrations/V11/V11-INV-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Eligibility, amount/unit equality, duplicate reversal prevention and reason/audit.

## Out of scope

- Payment refund and waste lifecycle.

## Dependencies

- V11-INV-001,V11-INV-002

## Deliverables

- V11-INV-003 için production implementation.
- Aynı davranışın otomatik başarı, ret ve concurrency/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- One reversal restores projected quantity; second reversal attempt is rejected; original row remains unchanged.

## Handoff

- V11 exit gate.

