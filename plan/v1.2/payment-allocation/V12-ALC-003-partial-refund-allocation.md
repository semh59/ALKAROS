# V12-ALC-003 - Implement refund intents

- Task ID: V12-ALC-003
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.26-I.29
- PDF:II.2.6
- PDF:II.3.4-II.3.5
- PDF:II.5.3
- PDF:III.8

## Goal

Full veya partial refund talebinin eligibility, target allocation, amount ve idempotency değerlerini RefundIntent olarak
kalıcılaştırmak.

## Owned surface

- `src/Modules/Payments/Allocations/RefundIntents/**`, `tests/Modules/Payments/Allocations/RefundIntents/**`,
  `database/migrations/V12/V12-ALC-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Allocation target, requested amount, cumulative eligibility snapshot, idempotency ve Pending/Rejected intent geçişi.

## Out of scope

- Provider transport, compensating allocation, net-paid mutation, fiscal refund ve inventory return.

## Dependencies

- V12-ALC-001
- V12-ALC-002
- V0-DOM-003
- V11-RSV-003

## Deliverables

- `src/Modules/Payments/Allocations/RefundIntents/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- 100 payment için 20 talep tek Pending RefundIntent üretir; duplicate aynı intent'i döndürür ve 100 üzeri talep
  provider çağrısından önce reddedilir.
- Bu görev PaymentAllocation veya net-paid amount değiştirmez.

## Handoff

- V12-HUG-003
- V12-ALC-004
