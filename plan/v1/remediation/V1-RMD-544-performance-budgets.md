# V1-RMD-544 - Performans bütçeleri ve gerileme alarmı

- Task ID: V1-RMD-544
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Her kritik eylemin süre bütçesi vardır; bütçe aşılınca veya normale göre yavaşlayınca alarm üretilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-544-performance-budgets.md`
- `evidence/V1-RMD-544/**`
- `src/Modules/Observability/PerformanceBudgets/**`
- `tests/Modules/Observability/PerformanceBudgets/**`
- `database/migrations/V1/V1-RMD-544/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs - yalnız bütçe değerlendiricisi kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx - yalnız yeni test projesi girişi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Eylem başına bütçe (ör. p95 800 ms) ve taban çizgisi karşılaştırması (aynı saat dilimi); gerileme: yeni sürümden sonra belirgin yavaşlama.

## Out of scope

- Yok.

## Dependencies

- V1-RMD-543

## Acceptance evidence

1. Bütçe aşımı 3 ardışık dakikada 1 alarm üretir (tekrar bastırma).
2. Yeni sürüm sonrası %50 yavaşlama gerileme olayı olur.
3. Çıktılar `evidence/V1-RMD-544/` altındadır.

## Handoff

- None
