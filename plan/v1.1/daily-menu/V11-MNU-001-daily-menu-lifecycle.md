# V11-MNU-001 - Implement DailyMenu lifecycle

- Task ID: V11-MNU-001
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

İş tarihi menüsü oluşturma, ürün seçimi, günlük fiyat ve açma/kapama kurallarını uygulayın.

## Owned surface

- `src/Modules/Menu/DailyMenuLifecycle/**`, `tests/Modules/Menu/DailyMenuLifecycle/**`,
  `database/migrations/V11/V11-MNU-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Servis günü başına bir menü, tarif referansı, günlük fiyat ve ürün aktivasyonu.

## Out of scope

- Envanter sayaçları, production ve porsiyon rezervasyonu.

## Dependencies

- V1-CAT-001
- V1-CAT-002
- V11-RCP-001
- V11-MNU-003
- V0-CMP-002

## Deliverables

- `src/Modules/Menu/DailyMenuLifecycle/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Aynı davranışın otomatik başarı, ret ve concurrency/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Hizmet günü benzersizliği, yapılandırılmış saat dilimi/kesim noktasına saygı gösterir; kapalı menü yeni operasyonel
  öğeleri reddeder.

## Handoff

- V11-MNU-002
- V11-PRD-001
