# V1-RMD-698 - Göç satır sonu, indeks ve şema düzeltmeleri

- Task ID: V1-RMD-698
- Status: InProgress
- Assignee: claude-code-session_01CEenPfDb6pEWVcDd9W7VdF
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

V1-RMD-599 bulguları B047, B054, B055, B110, B111, B112 kapanır. Göç dosyalarının satır sonu depoda sabitlenir ve sağlama toplamı satır sonundan bağımsız hale gelir (veya .gitattributes ile korunur); ekran koruyucu için boyut ve tür kısıtı ve koşullu istek okuması; makbuz sorgusu için indeks; şema tutarsızlıkları (satır sürümü tipi, IF NOT EXISTS, süre kısıtı, indeks) giderilir; Türkçe sıralama; Done görevlerin eksik kanıt klasörleri için ileri-geri göç testi eklenir.

## Owned surface

- `plan/v1/remediation/V1-RMD-698-migration-line-endings-and-indexes.md`
- `evidence/V1-RMD-698/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): .gitattributes - yeni dosya
- `database/migrations/V1/V1-RMD-698/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/** - yalnız ekran koruyucu okuma ve sıralama
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/CustomerAccounts/** - yalnız indeks
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/** - yalnız sağlama toplamı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json - yalnız yeni göç girdisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs - yalnız PhaseBMax
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs - yalnız yeni göç girdisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/data/migration-rehearsal-profile.md - yalnız yeni göç satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-697-backup-restore-and-deploy-fixes.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Satır sonu politikası, sağlama toplamı, yeni göç (indeks, CHECK, tip), ileri-geri testi, ETag öncesi içerik okumama.

## Out of scope

- Büyük veride göç süresi ölçümü (V20-DRL-001).

## Dependencies

- V1-RMD-697

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-698/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-699
