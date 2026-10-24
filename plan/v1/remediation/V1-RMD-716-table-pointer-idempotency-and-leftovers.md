# V1-RMD-716 - Masa işaretçisi, kat planı sürüm kuralı ve düşük bulgular

- Task ID: V1-RMD-716
- Status: Done
- Assignee: claude-code-session_01CEenPfDb6pEWVcDd9W7VdF
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-601 bulguları B020 (sunucu kısmı), B025, B026 ve B033 kapanır: işaretçi yeniden kurma çağıranı ve seçim politikası, kat planı kaydının masa satır sürümünü istememesi, düşük bulgular listesi. İdempotency anahtarı (B031), olay kimliğiyle tekilleştirme ve ölü olay uyarısı/yeniden oynatma V1-RMD-717'ye ayrıldı.

## Owned surface

- `plan/v1/remediation/V1-RMD-716-table-pointer-idempotency-and-leftovers.md`
- `evidence/V1-RMD-716/**`
- `src/Modules/Tables/TableLimits.cs`
- `src/Host/Experience/Tables/TablePointerRepairHostedService.cs`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Tables/** - yalnız ilgili düzeltme
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Tables/** - yalnız ilgili düzeltme
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/tables/** - yalnız ilgili düzeltme
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/ReservationStation.tsx - yalnız iki aynı dal
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/ReservationStation.test.tsx - yalnız ulaşılamayan sunucu testi
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/data/projection-ownership.md - yalnız işaretçi onarımı satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Tables/** - yalnız yeni ve düzeltilen test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Tables/** - yalnız yeni ve düzeltilen test
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- İşaretçi onarımı, kat planı sürüm kuralı, düşük bulgular.

## Out of scope

- Doküman sapmaları ve modül sınırı (V1-RMD-715).
- İdempotency anahtarı, olay tekilleştirme, ölü olay uyarısı ve yeniden oynatma (V1-RMD-717).

## Dependencies

- V1-RMD-601

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-716/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-717
