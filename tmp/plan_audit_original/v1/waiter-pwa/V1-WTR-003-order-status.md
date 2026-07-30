# V1-WTR-003 - Implement Waiter PWA Order status view

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF V1 scope plus referenced V0 correction task; undocumented behavior is out of scope.

## Goal

Display server-authoritative Order and kitchen item progress with reconnect-safe refresh.

## Owned surface

- `src/Clients/WaiterPwa/OrderStatus/**`, `tests/Clients/WaiterPwa/OrderStatus/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- SignalR/reconnect fallback, state labels, cancelled items and stale indicator.

## Out of scope

- Kitchen status mutation and cashier payment state.

## Dependencies

- V1-WTR-001,V1-KIT-001,V1-OBS-001

## Deliverables

- V1-WTR-003 için production implementation.
- Public API/event contract varsa contract testleri dahil otomatik testler.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Disconnected UI marks data stale; reconnect converges to source state; waiter cannot mutate kitchen state through view.

## Handoff

- V1 exit gate.

