# V1-CUI-002 - Implement cashier Order entry

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF V1 scope plus referenced V0 correction task; undocumented behavior is out of scope.

## Goal

Implement product/modifier selection, notes, draft editing and idempotent submit in Turkish UI.

## Owned surface

- `src/Clients/Cashier/OrderEntry/**`, `tests/Clients/Cashier/OrderEntry/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Catalog search, modifiers, draft totals, submit key and domain error mapping.

## Out of scope

- Payment, split bill and production/inventory screens.

## Dependencies

- V1-CUI-001,V1-ORD-001,V1-ORD-002

## Deliverables

- V1-CUI-002 için production implementation.
- Public API/event contract varsa contract testleri dahil otomatik testler.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Double click/retry creates one Order; invalid modifier/price change shows server result and preserves recoverable draft.

## Handoff

- V1-CUI-003.

