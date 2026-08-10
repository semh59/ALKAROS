# V11-RSV-001 - Implement PortionReservation lifecycle

- Task ID: V11-RSV-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.21-I.25
- PDF:II.2.12
- PDF:II.3.9
- PDF:II.5.6
- PDF:II.5.14
- PDF:III.14
- CORR:C2

## Goal

Bir OrderItem ve StockBalance'a bağlı `Reserved`, `Released`, `Consumed` ve `Wasted` geçişlerini uygulamak.

## Owned surface

- `src/Modules/Inventory/PortionReservations/Lifecycle/**`, `tests/Modules/Inventory/PortionReservations/Lifecycle/**`,
  `database/migrations/V11/V11-RSV-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Doğrudan stok bakiyesi/konum kimliği, zaman damgaları, geçiş sürümü ve idempotency anahtarı.

## Out of scope

- Son bölüm kilitleme stratejisi ve order kabul düzenlemesi.

## Dependencies

- V11-INV-001
- V11-INV-002
- V1-ORD-001
- V0-DOM-001

## Deliverables

- `src/Modules/Inventory/PortionReservations/Lifecycle/**` altında Goal kapsamını uygulayan production code ve
  task-specific automated test assets.
- Aynı davranışın otomatik başarı, ret ve concurrency/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Consume/release race tam olarak bir terminal geçişine izin verir; yinelenen komut orijinal sonucu yeniden oynatır.

## Handoff

- V11-RSV-002
- V11-RSV-003
