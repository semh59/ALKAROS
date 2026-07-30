# V11-MNU-002 - Implement DailyMenu counter projection

- Task ID: V11-MNU-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.21-I.25
- PDF:II.2.8-II.2.9
- PDF:II.3.6
- PDF:III.10-III.11

## Goal

Authoritative production/inventory kayıtlarından prepared, reserved, consumed, waste ve available counter
projection'larını üretmek.

## Owned surface

- `src/Modules/Menu/CounterProjection/**`, `tests/Modules/Menu/CounterProjection/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Projeksiyon hesaplaması, atomik güncelleme kancası, rebuild komutu ve sapma tespiti.

## Out of scope

- Yetkili stok hareketi veya rezervasyon mutasyonları.

## Dependencies

- V11-MNU-001
- V0-DAT-004
- V11-INV-001
- V11-INV-007
- V11-PRD-002
- V11-RSV-001
- V11-RSV-003

## Deliverables

- `src/Modules/Menu/CounterProjection/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Aynı davranışın otomatik başarı, ret ve concurrency/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Projeksiyon silinebilir ve aynı değerlere yeniden oluşturulabilir; mevcut hiçbir zaman bağımsız olarak yazılabilir bir
  kaynak haline gelmez.
- Production output ile Reserved/Released/Consumed/Wasted reservation event'lerinin her biri counter'ı tam bir kez
  etkiler; full rebuild live projection ile eşleşir ve drift testi her producer sınıfını ayrı ayrı kapsar.

## Handoff

- None
