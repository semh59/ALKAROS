# V12-PUI-001 - Implement cashier payment and split allocation UI

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF V1.2 scope plus validated provider contract and audit corrections.

## Goal

Implement Cash, BankCard and approved MealCard payment composition over explicit Bill allocations.

## Owned surface

- `src/Clients/Cashier/Payments/SplitPayment/**`, `tests/Clients/Cashier/Payments/SplitPayment/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Equal/amount/item split, remaining amount, tender selection, idempotent submit and unknown-state lock.

## Out of scope

- CustomerAccount tender, refund and cash close.

## Dependencies

- V12-PAY-002,V12-ALC-001,V12-ALC-002,V1-BIL-002

## Deliverables

- V12-PUI-001 için production implementation.
- Contract/UI ve otomatik success/failure/retry testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- UI cannot submit over-allocation; unknown payment blocks duplicate tender; mixed payment closes only on server-approved total.

## Handoff

- V12-PUI-002 and V12-PUI-003.

