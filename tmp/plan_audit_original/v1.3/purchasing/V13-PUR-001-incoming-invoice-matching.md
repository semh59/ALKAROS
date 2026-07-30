# V13-PUR-001 - Implement supplier account and incoming-invoice matching

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Match incoming invoice lines to supplier, purchase receipt and payable account entries without changing inventory twice.

## Owned surface

- `src/Modules/Purchasing/InvoiceMatching/**`, `tests/Modules/Purchasing/InvoiceMatching/**`, `database/migrations/V13/V13-PUR-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Supplier account ledger, line match tolerances, receipt links, cost update source and mismatch case.

## Out of scope

- QNB retrieval and purchase-order receipt posting.

## Dependencies

- V11-PUR-001,V11-RCP-002,V13-QNB-003

## Deliverables

- V13-PUR-001 için production implementation.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Matched invoice creates one payable entry and no duplicate stock movement; quantity/price mismatch opens reconciliation.

## Handoff

- V15-REC-001.

