# V12-PAY-001 - Implement Payment aggregate

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Implement payment identity, canonical status transitions and money fields under the V0 financial contracts.

## Owned surface

- `src/Modules/Payments/PaymentAggregate/**`, `tests/Modules/Payments/PaymentAggregate/**`, `database/migrations/V12/V12-PAY-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Requested, approved, tendered, change and currency invariants; transition history and row version.

## Out of scope

- PaymentAllocation, provider calls and refunds.

## Dependencies

- V1.2 entry gate,V0-DOM-001,V0-CMP-002

## Deliverables

- V12-PAY-001 için production implementation.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Invalid status/money combinations are rejected; payment history remains immutable and currency is explicit.

## Handoff

- V12-PAY-002 and V12-ALC-001.

