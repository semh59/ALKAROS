# V13-ACC-002 - Implement CustomerAccount balance projection

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Compute current balance and dated snapshots from the immutable account ledger.

## Owned surface

- `src/Modules/CustomerAccounts/BalanceProjection/**`, `tests/Modules/CustomerAccounts/BalanceProjection/**`, `database/migrations/V13/V13-ACC-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Atomic projection update, full rebuild, aging basis and snapshot uniqueness.

## Out of scope

- Posting new account transactions and invoice source selection.

## Dependencies

- V13-ACC-001,V0-DAT-004

## Deliverables

- V13-ACC-002 için production implementation.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Deleting/rebuilding projection reproduces current balance and snapshots; mixed debit/credit example matches expected result.

## Handoff

- V13-INV-001.

