# V1-RMD-420 - Fark raporunun üretim tüketimini ve çıktısını hesaba katması

- Task ID: V1-RMD-420
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-398 stok/reçete denetimi G-05 (Orta): gerçek-teorik fark raporunun formülü `açılış + teslim − kapanış` idi.
Tamamlanmış bir üretim partisinin tükettiği malzeme (ör. hamur için 5 kg un) raporda açıklanamayan sapma olarak
görünüyordu; üretimin stoğa kattığı çıktı da formülde yoktu, bu yüzden üretilen kalemde sahte "negatif kullanım"
çıkıyordu. V11-RPT-003 formülü üretimi hiç anmıyor.

Bu görev: rapor satırına dönemdeki üretim tüketimi ve üretim çıktısı eklenir. Gerçek kullanım
`açılış + teslim + üretim çıktısı − kapanış`; açıklanan kullanım `teorik (satış) + üretim tüketimi`; fark
`gerçek − açıklanan`, yüzde açıklanan kullanıma göre.

## Owned surface

- `plan/v1/remediation/V1-RMD-420-variance-report-accounts-for-production.md`
- `evidence/V1-RMD-420/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Reporting/MenuInventory/PostgresMenuInventoryReportingService.cs
  ve src/Modules/Reporting/MenuInventory/ReportingModels.cs (V11-RPT-001 sahipliğinde) — yalnız fark raporunun iki
  yeni sütunu ve formülü
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Reporting/MenuInventory/MenuInventoryReportingDatabaseTests.cs
  (V11-RPT-001 sahipliğinde) — yeni test

## In scope

- `ProductionOrder` kaynaklı `Consumption` (çıkış) ve `ProductionOutput` (giriş) hareketleri, kalem ve lokasyon
  bazında.

## Out of scope

- Fire, transfer ve elle düzeltme hareketleri (ayrı karar; bulgu yalnız üretimi kapsıyor).
- Teorik kullanımın lokasyona dağıtılması (G-03, belgelenmiş sadeleştirme).

## Dependencies

- V1-RMD-419

## Acceptance evidence

- `ALKAROS.Reporting.MenuInventory.Tests` 10/10 ve `ALKAROS.Host.Experience.InventoryReporting.Tests` 8/8 (gerçek
  PostgreSQL 18, Release, 0 uyarı / 0 hata; `evidence/V1-RMD-420/tests.log`). Mevcut fark raporu testleri (üretim
  hareketi olmayan kalemler) değişmeden geçer.
- Yeni test `ActualVsTheoreticalReportCountsProductionConsumptionAndOutput`: üretimin 5 kg tükettiği un (20 → 15) ve
  10 kg ürettiği hamur (0 → 10) için ikisinde de fark 0. Sorgu ve formül değişikliği geri alınınca kırmızı
  (`evidence/V1-RMD-420/red-without-fix.log`).
- V1-RMD-398 probe'u S05 (ve S04) düzeltilmiş kopyada geçer (`evidence/V1-RMD-420/stock-flow-probes.log`).
- API yanıtındaki fark raporu satırına `productionOutput` ve `productionConsumption` alanları eklendi; bu raporu
  gösteren bir istemci ekranı yok.
- Semih'in elle deneyebileceği senaryo: dönem içinde un kullanan bir üretim partisi tamamlayın ve açılış/kapanış
  sayımını yapın; gerçek-teorik fark raporunda un satırı üretim tüketimini gösterir ve sahte sapma çıkmaz.

## Handoff

- None
