# V12-CSH-002 - Implement CashTransaction ledger and close difference

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Post cash sale/refund/in/out entries and compute expected versus actual close difference.

## Owned surface

- `src/Modules/Cash/TransactionLedger/**`, `tests/Modules/Cash/TransactionLedger/**`, `database/migrations/V12/V12-CSH-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Positive magnitude/direction rules, payment linkage, explicit correction and close projection.

## Out of scope

- Bank/meal-card transactions and general ledger accounting.

## Dependencies

- V12-CSH-001,V12-PAY-001,V0-DAT-004

## Deliverables

- V12-CSH-002 için production implementation.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Session expected cash rebuilds from immutable entries; difference is recorded, never silently overwritten.

## Handoff

- V12-REC-001.

