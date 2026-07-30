# V12-RPT-001 - Implement payment cash fiscal and meal-card reports

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF V1.2 scope plus validated provider contract and audit corrections.

## Goal

Implement payment mix, cash session, fiscal status and meal-card settlement reports.

## Owned surface

- `src/Modules/Reporting/Payments/**`, `tests/Modules/Reporting/Payments/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Business-date/terminal/provider filters, net refund amounts, cash difference and reconciliation totals.

## Out of scope

- Customer account, invoice and online channel reports.

## Dependencies

- V0-DOM-008,V12-ALC-003,V12-CSH-002,V12-MCD-002,V12-FSC-001

## Deliverables

- V12-RPT-001 için production implementation.
- Contract/UI ve otomatik success/failure/retry testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Report totals reconcile to authoritative ledgers after full/partial refunds; Unknown/ReconciliationRequired are separately visible.

## Handoff

- V15-RPT-001.

