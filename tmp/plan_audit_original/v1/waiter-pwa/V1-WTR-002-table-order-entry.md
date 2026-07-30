# V1-WTR-002 - Implement Waiter PWA table Order entry

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF V1 scope plus referenced V0 correction task; undocumented behavior is out of scope.

## Goal

Implement table selection, product/modifier/note entry and idempotent submit for waiter permissions.

## Owned surface

- `src/Clients/WaiterPwa/OrderEntry/**`, `tests/Clients/WaiterPwa/OrderEntry/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Table availability, catalog, draft, queued submit, conflict and server error mapping.

## Out of scope

- Payment, table merge administration and QR customer UI.

## Dependencies

- V1-WTR-001,V1-ORD-002,V1-TBL-001,V1-CAT-001

## Deliverables

- V1-WTR-002 için production implementation.
- Public API/event contract varsa contract testleri dahil otomatik testler.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Reconnect/double submit creates one Order; stale table conflict is visible and does not silently move Order.

## Handoff

- V1-WTR-003.

