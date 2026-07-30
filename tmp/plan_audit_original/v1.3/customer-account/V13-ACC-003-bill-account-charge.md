# V13-ACC-003 - Implement CustomerAccount tender posting

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Convert an approved CustomerAccount tender into one account charge and one payment allocation without double posting.

## Owned surface

- `src/Modules/CustomerAccounts/BillCharges/**`, `tests/Modules/CustomerAccounts/BillCharges/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Eligibility, credit policy result, account Charge source, Payment approval and allocation transaction boundary.

## Out of scope

- Periodic invoice issuance and general credit scoring.

## Dependencies

- V13-ACC-001,V12-PAY-002,V12-ALC-001

## Deliverables

- V13-ACC-003 için production implementation.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Retry produces one charge/allocation; insufficient policy approval mutates neither Bill nor account; amounts remain equal.

## Handoff

- V13-INV-001.

