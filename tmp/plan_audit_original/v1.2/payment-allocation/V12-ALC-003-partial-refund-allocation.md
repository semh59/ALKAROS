# V12-ALC-003 - Implement allocation-level full and partial refunds

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Post immutable compensating allocation records and recompute net paid amount for full or partial refund.

## Owned surface

- `src/Modules/Payments/Allocations/Refunds/**`, `tests/Modules/Payments/Allocations/Refunds/**`, `database/migrations/V12/V12-ALC-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Allocation target, cumulative refund limit, idempotency, bill state effect and inventory separation.

## Out of scope

- Hugin refund transport and fiscal refund document.

## Dependencies

- V12-ALC-001,V12-ALC-002,V0-DOM-003

## Deliverables

- V12-ALC-003 için production implementation.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- 100 payment / 20 refund leaves 80 net paid; second identical callback is replayed; cumulative refund above 100 is rejected.

## Handoff

- V12-HUG-003 and V12-FSC-001.

