# V12-MCD-001 - Implement MealCardPayment details

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Persist provider, gross, commission, deduction and net receivable for an approved MealCard payment.

## Owned surface

- `src/Modules/MealCard/Payments/**`, `tests/Modules/MealCard/Payments/**`, `database/migrations/V12/V12-MCD-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- One subtype row per Payment, amount formula, provider reference and Unsettled state.

## Out of scope

- Settlement grouping and provider transport.

## Dependencies

- V12-PAY-001,V12-PAY-002,V0-DAT-002

## Deliverables

- V12-MCD-001 için production implementation.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Non-MealCard payment cannot get detail row; net formula and uniqueness are enforced.

## Handoff

- V12-MCD-002.

