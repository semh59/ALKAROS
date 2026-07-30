# V1-CUI-003 - Implement cashier operational status view

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF V1 scope plus referenced V0 correction task; undocumented behavior is out of scope.

## Goal

Display open Orders/Bills, kitchen progress and failed/uncertain print jobs with allowed recovery actions.

## Owned surface

- `src/Clients/Cashier/OperationsStatus/**`, `tests/Clients/Cashier/OperationsStatus/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Order/Bill link, ticket item state, print failure/reprint permission and audit reason.

## Out of scope

- Payment UI, reconciliation dashboard and kitchen display.

## Dependencies

- V1-BIL-001,V1-KIT-001,V1-KIT-004,V1-IAM-002

## Deliverables

- V1-CUI-003 için production implementation.
- Public API/event contract varsa contract testleri dahil otomatik testler.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- UI cannot mark backend state directly; reprint follows V1-KIT-004 and requires permission/reason.

## Handoff

- V15-RUN-001.

