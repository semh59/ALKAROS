# V13-RPT-001 - Implement customer account and invoice reports

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF scope plus V0 compliance/domain correction; conditional behavior requires recorded evidence.

## Goal

Implement account aging, invoice aging/status, incoming match and supplier payable reports.

## Owned surface

- `src/Modules/Reporting/AccountsInvoicing/**`, `tests/Modules/Reporting/AccountsInvoicing/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- As-of date, period, customer/supplier, issued/cancelled/reconciliation states and ledger totals.

## Out of scope

- Operational dashboard UI and online channel metrics.

## Dependencies

- V0-DOM-008,V13-ACC-002,V13-INV-003,V13-PUR-001

## Deliverables

- V13-RPT-001 için production implementation.
- Public contract/UI ve otomatik success/failure/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Aging totals reconcile to immutable ledgers and exclude/reclassify cancelled invoice exactly per V0-DOM-007.

## Handoff

- V15-RPT-001.

