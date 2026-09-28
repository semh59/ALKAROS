# V1-RMD-423 - Ortalama maliyetin İstanbul tarihine göre hesaplanması ve koddaki düşünme notlarının temizlenmesi

- Task ID: V1-RMD-423
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-398 stok/reçete denetimi:

- G-08 (Düşük): hareketli ortalama maliyet, teslim anını `CAST(received_at AS date)` ile veritabanı oturumunun saat
  diliminde güne çeviriyordu. Dağıtımda oturum saat dilimi ayarlanmadığı için bu UTC'dir; İstanbul'da gece 01:30'da
  (UTC'de önceki gün 22:30) gelen bir teslim, önceki günün maliyetine giriyordu (denetimde 30 yerine 60).
- G-10 (Düşük): aynı dosyada (`IStockCostResolver.cs`) üretim kodunda AI düşünme notları kalmıştı ("Wait, earlier we
  saw…", "Let's inspect…").

Bu görev: teslim tarihi `received_at AT TIME ZONE 'Europe/Istanbul'` ile restoranın yerel tarihine çevrilerek
karşılaştırılır (iş günü raporlarının kullandığı bölge); düşünme notları, sorgunun ne yaptığını anlatan kısa bir
açıklamayla değiştirilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-423-stock-cost-local-date-and-leftover-notes.md`
- `evidence/V1-RMD-423/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Recipes/CostSnapshots/IStockCostResolver.cs (V11-RCP-002
  sahipliğinde) — yalnız tarih karşılaştırması ve yorumlar
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Recipes/CostSnapshots/RecipeCostSnapshotDatabaseTests.cs
  (V11-RCP-002 sahipliğinde) — yeni test

## In scope

- Maliyet tarihinin İstanbul yerel tarihine göre kesilmesi.
- Düşünme notlarının kaldırılması.

## Out of scope

- Maliyet yönteminin kendisi (ağırlıklı ortalama).

## Dependencies

- V1-RMD-422

## Acceptance evidence

- `ALKAROS.Recipes.CostSnapshots.Tests` 14/14 (gerçek PostgreSQL 18, Release, 0 uyarı / 0 hata;
  `evidence/V1-RMD-423/tests.log`). Mevcut maliyet testleri değişmeden geçer.
- Yeni test `MovingAverageCostCountsADeliveryOnTheRestaurantsLocalDate`: 5 Ağustos 12:00 (İstanbul) 30 TL'lik ve
  6 Ağustos 01:30 (İstanbul; UTC'de 5 Ağustos 22:30) 90 TL'lik teslim; 5 Ağustos maliyeti 30, 6 Ağustos maliyeti 60.
  Üretim değişikliği geri alınınca kırmızı (5 Ağustos için 60; `evidence/V1-RMD-423/red-without-fix.log`).
- V1-RMD-398 probe'u S13 düzeltilmiş kopyada geçer (`evidence/V1-RMD-423/stock-flow-probe-s13.log`).
- G-10: `IStockCostResolver.cs` içindeki "Wait…", "Let's inspect…" notları, sorgunun ne yaptığını anlatan kısa bir
  açıklamayla değiştirildi.
- Semih'in elle deneyebileceği senaryo: gece yarısından sonra (ör. 01:30) gelen bir teslimat, reçete maliyetinde
  bir önceki günün değil teslim gününün maliyetine girer.

## Handoff

- None
