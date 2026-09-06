# V11-PRD-002 - Implement production consumption and output effects

- Task ID: V11-PRD-002
- Status: Done
- Assignee: f2f02a8c-1e37-451a-b2c8-e6543e3fdf89
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.21-I.25
- PDF:II.2.11
- PDF:II.3.8
- PDF:II.5.5
- PDF:III.13
- CORR:C9

## Goal

ProductionBatch transaction'ında IngredientConsumption ve prepared-portion ProductionOutput movement'larını oluşturmak.

## Owned surface

- `src/Modules/Production/StockEffects/**`, `tests/Modules/Production/StockEffects/**`,
  `database/migrations/V11/V11-PRD-002/**`
- `tests/Modules/Production/StockEffects/ALKAROS.Production.StockEffects.Tests.csproj`, `tests/Modules/Production/StockEffects/packages.lock.json`
- Kapsam genişletme onayı (2026-09-06 kullanıcı talimatı): bu task'ın yeni test projesinin `ALKAROS.slnx` ve `build/project-manifest.json` içine kaydı.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Atık faktörü order, birim dönüştürme, hareket referansları, gerçek çıktı ve geri alma güvenli işlem.

## Out of scope

- Production planlama ve satın alma.

## Dependencies

- V11-PRD-001
- V11-RCP-002
- V11-UNT-001
- V11-INV-001
- V11-INV-002
- V0-DOM-010
- V1-FND-005

## Deliverables

- `src/Modules/Production/StockEffects/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Aynı davranışın otomatik başarı, ret ve concurrency/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Tamamlanan parti bir kez dengeli izlenebilir hareketler oluşturur; yinelenen tamamlama hiçbir şey yaratmaz; yetersiz
  ham stok atomik olarak başarısız olur.

## Handoff

- V11-MNU-002
