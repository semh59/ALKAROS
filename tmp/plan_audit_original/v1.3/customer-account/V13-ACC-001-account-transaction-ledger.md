# V13-ACC-001 - Implement CustomerAccount transaction ledger

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Persist positive-magnitude account transactions with explicit direction semantics and immutable source links.

## Owned surface

- `src/Modules/CustomerAccounts/TransactionLedger/**`, `tests/Modules/CustomerAccounts/TransactionLedger/**`, `database/migrations/V13/V13-ACC-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Charge, Payment, Invoice, Credit, Debit, Adjustment and Refund sign/direction rules.

## Out of scope

- Cached current balance and invoice generation.

## Dependencies

- V13-CST-001,V0-DAT-002

## Deliverables

- V13-ACC-001 için production implementation.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Every transaction type has one signed effect; invalid sign/type combinations and in-place edits are rejected.

## Handoff

- V13-ACC-002 and V13-ACC-003.

