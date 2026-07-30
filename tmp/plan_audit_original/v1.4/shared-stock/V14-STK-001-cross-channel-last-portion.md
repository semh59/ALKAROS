# V14-STK-001 - Implement cross-channel last-portion arbitration

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Use the same reservation command for cashier, waiter, QR and online acceptance so exactly one channel wins.

## Owned surface

- `src/Modules/Inventory/CrossChannelReservation/**`, `tests/Modules/Inventory/CrossChannelReservation/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Channel-neutral command, concurrency result mapping and provider rejection compensation.

## Out of scope

- Reservation lifecycle internals and provider status transport.

## Dependencies

- V11-RSV-002,V14-QRO-003,V14-ONL-002

## Deliverables

- V14-STK-001 için production implementation.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Parallel four-channel test with one portion yields one reservation and three explicit OutOfStock/rejection outcomes.

## Handoff

- V14-REC-001.

