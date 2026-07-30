# V12-ALC-001 - Implement PaymentAllocation persistence constraints

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Implement allocation rows and database enforcement for payment/bill/segment identity, currency, amount and idempotency.

## Owned surface

- `src/Modules/Payments/Allocations/Persistence/**`, `tests/Modules/Payments/Allocations/Persistence/**`, `database/migrations/V12/V12-ALC-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Same-bill constraints, targeted/direct allocation uniqueness, remaining amount and immutable rows.

## Out of scope

- Bill status projection and refund compensation.

## Dependencies

- V12-PAY-001,V1-BIL-002,V0-DOM-004,V0-DAT-003

## Deliverables

- V12-ALC-001 için production implementation.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Cross-bill, cross-currency, duplicate and over-allocation inserts fail in both application and database tests.

## Handoff

- V12-ALC-002 and V12-ALC-003.

