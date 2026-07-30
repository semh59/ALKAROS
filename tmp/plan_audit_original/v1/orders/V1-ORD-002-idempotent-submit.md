# V1-ORD-002 - Implement idempotent Order submission

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Implement waiter/cashier submit as a version-checked idempotent command with response replay.

## Owned surface

- `src/Modules/Orders/SubmitOrder/**`, `tests/Modules/Orders/SubmitOrder/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Client operation ID, request hash, row version, duplicate replay and stale edit rejection.

## Out of scope

- QR/online confirmation and inventory reservation.

## Dependencies

- V1-ORD-001,V1-FND-002,V1-IAM-003,V0-ARC-002

## Deliverables

- V1-ORD-002 için production implementation.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Double tap and reconnect retry produce one submitted Order; changed body with reused key is rejected; stale version mutates nothing.

## Handoff

- V1-KIT-001 and V14-QRO-001.

