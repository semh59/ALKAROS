# V11-RSV-001 - Implement PortionReservation lifecycle

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Implement Reserved, Released, Consumed and Waste transitions tied to an OrderItem and StockBalance.

## Owned surface

- `src/Modules/Inventory/PortionReservations/Lifecycle/**`, `tests/Modules/Inventory/PortionReservations/Lifecycle/**`, `database/migrations/V11/V11-RSV-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Direct stock-balance/location identity, timestamps, transition version and idempotency key.

## Out of scope

- Last-portion lock strategy and order acceptance orchestration.

## Dependencies

- V11-INV-001,V11-INV-002,V1-ORD-001,V0-DOM-001

## Deliverables

- V11-RSV-001 için production implementation.
- Aynı davranışın otomatik başarı, ret ve concurrency/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Consume/release race permits exactly one terminal transition; duplicate command replays the original result.

## Handoff

- V11-RSV-002 and V11-RSV-003.

