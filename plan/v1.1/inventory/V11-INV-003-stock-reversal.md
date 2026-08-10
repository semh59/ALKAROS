# V11-INV-003 - Implement compensating StockMovement reversal

- Task ID: V11-INV-003
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

Tam original movement'a bağlı tek bir idempotent `Reversal` movement oluşturmak.

## Owned surface

- `src/Modules/Inventory/MovementReversal/**`, `tests/Modules/Inventory/MovementReversal/**`,
  `database/migrations/V11/V11-INV-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Uygunluk, miktar/birim eşitliği, mükerrer iptallerin önlenmesi ve gerekçe/denetim.

## Out of scope

- Payment geri ödeme ve atık yaşam döngüsü.

## Dependencies

- V11-INV-001
- V11-INV-002
- V0-DOM-010

## Deliverables

- `src/Modules/Inventory/MovementReversal/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Aynı davranışın otomatik başarı, ret ve concurrency/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Bir geri dönüş, öngörülen miktarı geri yükler; ikinci tersine çevirme girişimi reddedilir; orijinal satır değişmeden
  kalır.

## Handoff

- None
