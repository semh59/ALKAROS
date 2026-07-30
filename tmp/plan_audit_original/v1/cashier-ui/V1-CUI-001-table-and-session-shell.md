# V1-CUI-001 - Implement cashier shell and table view

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF V1 scope plus referenced V0 correction task; undocumented behavior is out of scope.

## Goal

Implement Turkish cashier shell, authenticated session and concurrency-aware table state view.

## Owned surface

- `src/Clients/Cashier/TableShell/**`, `tests/Clients/Cashier/TableShell/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Login/session, table zones/status, stale refresh and clear error presentation.

## Out of scope

- Order entry, bill payment and settings administration.

## Dependencies

- V1-IAM-003,V1-TBL-001,V1-TBL-005

## Deliverables

- V1-CUI-001 için production implementation.
- Public API/event contract varsa contract testleri dahil otomatik testler.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Expired session returns to login; table updates reflect row-version conflicts; no UI-only lock is treated authoritative.

## Handoff

- V1-CUI-002.

