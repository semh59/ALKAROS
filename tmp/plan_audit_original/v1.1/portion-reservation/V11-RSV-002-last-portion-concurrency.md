# V11-RSV-002 - Implement atomic last-portion reservation

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Reserve portions with row locking/version checks so competing channels cannot oversell.

## Owned surface

- `src/Modules/Inventory/PortionReservations/Concurrency/**`, `tests/Modules/Inventory/PortionReservations/Concurrency/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Atomic availability check, balance mutation, reservation creation and losing-request result.

## Out of scope

- QR/online adapter behavior and cancellation classification.

## Dependencies

- V11-RSV-001,V11-INV-002

## Deliverables

- V11-RSV-002 için production implementation.
- Aynı davranışın otomatik başarı, ret ve concurrency/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Parallel test for one remaining portion yields one Reserved and one OutOfStock result with non-negative balance.

## Handoff

- V14-STK-001.

