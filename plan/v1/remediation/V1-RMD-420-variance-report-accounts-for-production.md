# V1-RMD-420 - Fark raporunun üretim tüketimini ve çıktısını hesaba katması

- Task ID: V1-RMD-420
- Status: InProgress
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

- Kapanışta doldurulacak.

## Handoff

- None
