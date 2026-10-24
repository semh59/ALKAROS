# V1-RMD-716 - Masa işaretçisi, idempotency, olay tekilleştirme ve düşük bulgular

- Task ID: V1-RMD-716
- Status: Planned
- Assignee: unassigned
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-601 bulguları B020 (sunucu kısmı), B025, B026, B031, B033 ve ertelenen kalemler kapanır: işaretçi yeniden kurma çağıranı ve seçim politikası, kat planı kaydının yalnız değişen masaların sürümünü istemesi, tekrar gönderimde ayırma, birleştirme ve transfer için idempotency anahtarı, olay kimliğiyle tekilleştirme, ölü olay uyarısı ve yeniden oynatma, düşük bulgular listesi.

## Owned surface

- `plan/v1/remediation/V1-RMD-716-table-pointer-idempotency-and-leftovers.md`
- `evidence/V1-RMD-716/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/migrations/** - yalnız yeni migration
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json - yalnız yeni migration kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs - yalnız yeni migration kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs - yalnız yeni migration kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Tables/** - yalnız ilgili düzeltme
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Tables/** - yalnız ilgili düzeltme
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/tables/** - yalnız ilgili düzeltme
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/ReservationStation.tsx - yalnız iki aynı dal
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Tables/** - yalnız yeni ve düzeltilen test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Tables/** - yalnız yeni ve düzeltilen test
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- İşaretçi onarımı, kat planı sürüm kuralı, idempotency, olay tekilleştirme, düşük bulgular.

## Out of scope

- Doküman sapmaları ve modül sınırı (V1-RMD-715).

## Dependencies

- V1-RMD-601

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-716/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-602
