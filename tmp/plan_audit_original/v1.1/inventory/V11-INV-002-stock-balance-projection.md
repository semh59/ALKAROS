# V11-INV-002 - Implement rebuildable StockBalance projection

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Maintain on-hand, reserved and available balances as a projection of ledger/reservation events.

## Owned surface

- `src/Modules/Inventory/BalanceProjection/**`, `tests/Modules/Inventory/BalanceProjection/**`, `database/migrations/V11/V11-INV-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Location uniqueness with NULL policy, non-negative checks, row version and full rebuild.

## Out of scope

- Reservation command behavior and daily-menu presentation counters.

## Dependencies

- V11-INV-001,V0-DAT-003,V0-DAT-004

## Deliverables

- V11-INV-002 için production implementation.
- Aynı davranışın otomatik başarı, ret ve concurrency/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Ledger replay rebuilds identical balances; concurrent writers cannot create a negative available value.

## Handoff

- V11-RSV-002 and V11-MNU-002.

