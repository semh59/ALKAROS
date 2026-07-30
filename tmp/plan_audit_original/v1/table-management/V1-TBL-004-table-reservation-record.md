# V1-TBL-004 - Implement Table reservation records

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF V1 scope plus referenced V0 correction task; undocumented behavior is out of scope.

## Goal

Persist the approved reservation actor, reason and expiry model behind Table.Reserved.

## Owned surface

- `src/Modules/TableManagement/Reservations/**`, `tests/Modules/TableManagement/Reservations/**`, `database/migrations/V1/V1-TBL-004/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Create/cancel/expire reservation and atomic Table status projection.

## Out of scope

- Reservation booking UI and QR pending policy.

## Dependencies

- V1-TBL-001,V0-DOM-005

## Deliverables

- V1-TBL-004 için production implementation.
- Public API/event contract varsa contract testleri dahil otomatik testler.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Expired/cancelled reservation releases only its own table version; concurrent occupancy is not overwritten.

## Handoff

- V14-QRO-002.

