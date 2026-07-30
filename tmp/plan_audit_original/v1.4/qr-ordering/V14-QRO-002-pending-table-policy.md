# V14-QRO-002 - Implement PendingConfirmation table policy

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Apply the approved occupied/reserved/no-change table behavior without allowing remote QR denial-of-service.

## Owned surface

- `src/Modules/QrOrdering/TablePolicy/**`, `tests/Modules/QrOrdering/TablePolicy/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Staff-seating evidence, pending expiry, optimistic concurrency and rejection rollback.

## Out of scope

- Order confirmation and generic table lifecycle implementation.

## Dependencies

- V14-QRO-001,V1-TBL-001,V0-CMP-001

## Deliverables

- V14-QRO-002 için production implementation.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Photographed/stale QR alone cannot take a free table indefinitely; concurrent staff state change wins or returns explicit conflict.

## Handoff

- V14-QRO-003.

