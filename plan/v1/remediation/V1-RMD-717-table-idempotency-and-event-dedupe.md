# V1-RMD-717 - Masa idempotency, olay tekilleştirme ve ölü olay yeniden oynatma

- Task ID: V1-RMD-717
- Status: Planned
- Assignee: unassigned
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-601 bulgusu B031 ve ertelenen olay kalemleri kapanır: tekrar gönderimde ayırma, birleştirme ve transfer için idempotency anahtarı (ilk sonucu tekrar döner), olay kimliğiyle tekilleştirme, ölü olay uyarısı ve yeniden oynatma.

## Owned surface

- `plan/v1/remediation/V1-RMD-717-table-idempotency-and-event-dedupe.md`
- `evidence/V1-RMD-717/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/migrations/** - yalnız yeni migration
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json - yalnız yeni migration kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs - yalnız yeni migration kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs - yalnız yeni migration kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Tables/** - yalnız ilgili düzeltme
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Tables/** - yalnız ilgili düzeltme
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/tables/** - yalnız ilgili düzeltme
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Tables/** - yalnız yeni ve düzeltilen test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Tables/** - yalnız yeni ve düzeltilen test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/ApiConventions/idempotency_allowlist.json - yalnız Tables uçlarının çıkarılması
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Idempotency anahtarı, olay tekilleştirme, ölü olay uyarısı ve yeniden oynatma.

## Out of scope

- İşaretçi onarımı, kat planı sürüm kuralı, düşük bulgular (V1-RMD-716).
- Birleşim ayırmada eksik katılımcı sürümü (V1-RMD-716 kapattı).

## Dependencies

- V1-RMD-716

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-717/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-602
