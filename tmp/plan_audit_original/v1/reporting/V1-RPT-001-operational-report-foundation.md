# V1-RPT-001 - Implement V1 operational reports

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF V1 scope plus referenced V0 correction task; undocumented behavior is out of scope.

## Goal

Implement order, table, waiter and print-failure reports using approved metric contracts.

## Owned surface

- `src/Modules/Reporting/V1Operations/**`, `tests/Modules/Reporting/V1Operations/**`, `database/migrations/V1/V1-RPT-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Business-date filters, order/table/waiter grain, print status and reconciliation totals.

## Out of scope

- Payment, inventory, invoice and online channel metrics.

## Dependencies

- V0-DOM-008,V1-ORD-001,V1-TBL-001,V1-KIT-003

## Deliverables

- V1-RPT-001 için production implementation.
- Public API/event contract varsa contract testleri dahil otomatik testler.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Report totals reconcile to source queries for seeded scenarios; timezone/service-day boundary tests pass.

## Handoff

- V15-RPT-001.

