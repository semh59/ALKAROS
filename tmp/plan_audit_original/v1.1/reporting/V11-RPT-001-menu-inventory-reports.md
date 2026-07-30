# V11-RPT-001 - Implement menu production and inventory reports

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF V1.1 module/schema sections plus named correction dependency.

## Goal

Implement sell-through, portion consumption, production, waste and critical stock reports.

## Owned surface

- `src/Modules/Reporting/MenuInventory/**`, `tests/Modules/Reporting/MenuInventory/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Metric contracts, service-day filters, location, recipe version and source reconciliation.

## Out of scope

- Financial sales, supplier payable and dashboard styling.

## Dependencies

- V0-DOM-008,V11-MNU-002,V11-PRD-002,V11-INV-002,V11-INV-006

## Deliverables

- V11-RPT-001 için production implementation.
- Public contract ve otomatik başarı/ret/concurrency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Report totals reconcile to movement/projection rebuilds; no report treats projection as a second source of truth.

## Handoff

- V15-RPT-001.

