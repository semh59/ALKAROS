# V11-INV-002 - Implement rebuildable on-hand stock projection

- Task ID: V11-INV-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.2.12
- PDF:II.3.9
- PDF:II.5.6
- PDF:II.5.14
- PDF:III.14

## Goal

StockMovement ledger'dan location/item bazında authoritative on-hand balance projection'ını üretmek ve rebuild etmek.

## Owned surface

- `src/Modules/Inventory/BalanceProjection/**`, `tests/Modules/Inventory/BalanceProjection/**`,
  `database/migrations/V11/V11-INV-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- NULL politikası, on-hand sign rules, satır sürümü, location uniqueness ve full ledger rebuild.

## Out of scope

- Reserved/available projection, reservation command behavior ve DailyMenu counters.

## Dependencies

- V11-INV-004
- V11-INV-001
- V0-DAT-003
- V0-DAT-004

## Deliverables

- `src/Modules/Inventory/BalanceProjection/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Aynı davranışın otomatik başarı, ret ve concurrency/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Ledger replay aynı on-hand balance değerlerini yeniden oluşturur; missing/duplicate StockMovement ve concurrent
  writers drift veya ikinci etki üretmez.

## Handoff

- V11-RSV-002
- V11-RSV-001
