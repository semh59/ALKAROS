# V1-RMD-423 - Ortalama maliyetin İstanbul tarihine göre hesaplanması ve koddaki düşünme notlarının temizlenmesi

- Task ID: V1-RMD-423
- Status: InProgress
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

- Kapanışta doldurulacak.

## Handoff

- None
