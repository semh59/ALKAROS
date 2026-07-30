# V1-ORD-001 - Implement the channel-independent Order aggregate

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Implement Order and OrderItem lifecycle, price snapshot, modifiers and table/customer context.

## Owned surface

- `src/Modules/Orders/OrderAggregate/**`, `tests/Modules/Orders/OrderAggregate/**`, `database/migrations/V1/V1-ORD-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Draft mutation, submit preconditions, item cancellation, snapshots and canonical transition enforcement.

## Out of scope

- Inventory reservation, kitchen ticket creation and payment.

## Dependencies

- V1-FND-001,V1-CAT-001,V1-CAT-002,V0-DOM-001

## Deliverables

- V1-ORD-001 için production implementation.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Order state transitions match contract; historical price/name snapshots remain unchanged after catalog edits.

## Handoff

- V1-ORD-002, V1-KIT-001 and V1-BIL-001.

