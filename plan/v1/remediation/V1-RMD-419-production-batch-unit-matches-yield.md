# V1-RMD-419 - Üretim partisi biriminin reçete verim birimine çevrilmesi ya da reddedilmesi

- Task ID: V1-RMD-419
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-398 stok/reçete denetimi G-04 (Orta): üretim partisinin tamamlanmasında malzeme ölçeği
`gerçekleşen miktar / reçete verimi` olarak hesaplanıyor; partinin birimi (varsayılan `portion`) ile reçetenin verim
birimi (ör. `kg`) hiç karşılaştırılmıyordu. "4 porsiyon" parti, "2 kg" verimli bir reçeteden iki katı malzeme
tüketiyordu. `yield_unit_code` okunuyor ama kullanılmıyordu.

Bu görev: tamamlamada partinin gerçekleşen miktarı reçetenin verim birimine çevrilir (aynı birimse olduğu gibi);
çevrilemiyorsa (ör. `portion` → `kg`) tamamlama Türkçe 409 ile reddedilir ve hiçbir stok hareketi yazılmaz.

## Owned surface

- `plan/v1/remediation/V1-RMD-419-production-batch-unit-matches-yield.md`
- `evidence/V1-RMD-419/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Production/StockEffects/ProductionStockEffectService.cs ve
  src/Modules/Production/StockEffects/Exceptions.cs (V11-PRD-002 sahipliğinde) — yalnız birim çevirisi/reddi ve yeni
  istisna
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Production/ProductionManagementEndpoints.cs
  (V1-RMD-133 sahipliğinde) — yalnız yeni istisnanın Türkçe 409 eşlemesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Production/StockEffects/ProductionStockEffectsDatabaseTests.cs
  (V11-PRD-002 sahipliğinde) — yeni testler

## In scope

- Parti birimi = verim birimi: davranış değişmez.
- Çevrilebilir birim (ör. `g` → `kg`): miktar çevrilerek ölçeklenir.
- Çevrilemeyen birim: 409 `BATCH_UNIT_MISMATCH`, stok değişmez.

## Out of scope

- Parti açılışında birim doğrulaması (tamamlama stok etkisinin tek yoludur; hatalı birimli parti iptal edilebilir).

## Dependencies

- V1-RMD-418

## Acceptance evidence

- `ALKAROS.Production.StockEffects.Tests` 9/9 ve `ALKAROS.Host.Experience.Production.Tests` 4/4 (gerçek PostgreSQL 18,
  Release, 0 uyarı / 0 hata; `evidence/V1-RMD-419/tests.log`). Parti ve verim birimi aynı olan mevcut testler
  değişmeden geçer.
- Yeni testler: `ABatchWhoseUnitCannotBeConvertedToTheRecipeYieldUnitIsRefusedAndConsumesNothing` (`kg` parti,
  `portion` verim → reddedilir, stok 50 kalır) ve `ABatchInAConvertibleUnitIsScaledInTheRecipeYieldUnit` (4000 g parti,
  2 kg verim, 1 kg domates → 2 kg tüketim). Üretim değişikliği geri alınınca ikisi de kırmızı
  (`evidence/V1-RMD-419/red-without-fix.log`).
- V1-RMD-398 probe'u S04 düzeltilmiş kopyada geçer (`evidence/V1-RMD-419/stock-flow-probe-s04.log`).
- Semih'in elle deneyebileceği senaryo: verimi kilogram olan bir reçeteyle porsiyon cinsinden açılmış bir partiyi
  tamamlamayı deneyin; "Partinin birimi reçetenin verim birimine çevrilemiyor; partiyi reçetenin verim biriminde
  açın." uyarısı çıkar ve stok düşmez.

## Handoff

- None
