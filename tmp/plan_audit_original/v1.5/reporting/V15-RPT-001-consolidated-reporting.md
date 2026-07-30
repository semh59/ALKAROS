# V15-RPT-001 - Implement consolidated reporting

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Expose one reconciled reporting entry point over the approved operational, stock, payment, invoice and channel report contracts.

## Owned surface

- `src/Modules/Reporting/Consolidated/**`, `tests/Modules/Reporting/Consolidated/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Report catalog, shared business-date filters, authorization, export limits and cross-report drill-through identifiers.

## Out of scope

- Redefining source metrics, writing financial transactions and ad hoc SQL access.

## Dependencies

- V1-RPT-001, V11-RPT-001, V12-RPT-001, V13-RPT-001, V14-RPT-001, V15-REC-001

## Deliverables

- Versioned consolidated reporting API/interface.
- Role, export-boundary, time-zone and cross-report consistency tests.

## Acceptance evidence

- The same approved filter yields traceable source identifiers and consistent totals across summary and drill-down reports.

## Handoff

- V20-UAT-002 and V20-GAT-002.
