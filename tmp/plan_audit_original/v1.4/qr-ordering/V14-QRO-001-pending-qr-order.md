# V14-QRO-001 - Implement pending QR order intake

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Convert one authenticated QR submission into one internal Order in PendingConfirmation.

## Owned surface

- `src/Modules/QrOrdering/PendingOrders/**`, `tests/Modules/QrOrdering/PendingOrders/**`, `database/migrations/V14/V14-QRO-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Payload validation, price snapshot, table binding, idempotency and pending expiry metadata.

## Out of scope

- Restaurant confirmation, table state and inventory reservation.

## Dependencies

- V14-QRS-002,V1-ORD-001,V1-ORD-002

## Deliverables

- V14-QRO-001 için production implementation.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Duplicate relay delivery creates one Order; invalid product/price/table creates none; no stock reservation occurs yet.

## Handoff

- V14-QRO-002 and V14-QRO-003.

