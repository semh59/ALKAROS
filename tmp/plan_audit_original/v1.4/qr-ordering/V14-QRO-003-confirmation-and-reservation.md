# V14-QRO-003 - Implement QR confirmation and portion reservation

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Confirm or reject a pending QR order and reserve portions only on successful acceptance.

## Owned surface

- `src/Modules/QrOrdering/Confirmation/**`, `tests/Modules/QrOrdering/Confirmation/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Permission, row version, atomic Order transition, table policy and reservation command.

## Out of scope

- Relay security and kitchen preparation behavior.

## Dependencies

- V14-QRO-001,V14-QRO-002,V11-RSV-002,V1-IAM-002

## Deliverables

- V14-QRO-003 için production implementation.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Confirmation creates one Accepted Order and reservation atomically; rejection/stock loss leaves no reservation or partial table state.

## Handoff

- V14-STK-001.

