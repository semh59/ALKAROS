# V12-PAY-002 - Implement tender command routing

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Route Cash, BankCard, MealCard and CustomerAccount payment commands to typed handlers without a SplitPayment tender.

## Owned surface

- `src/Modules/Payments/TenderRouting/**`, `tests/Modules/Payments/TenderRouting/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Typed tender request contracts and unsupported-method rejection.

## Out of scope

- Tender-specific provider logic and allocation persistence.

## Dependencies

- V12-PAY-001,V0-DAT-002

## Deliverables

- V12-PAY-002 için production implementation.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Every canonical method resolves to one handler; SplitPayment and unknown text values are rejected.

## Handoff

- V12-HUG-001, V12-CSH-001, V12-MCD-001 and V13-ACC-003.

