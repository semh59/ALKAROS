# V1-RMD-714 - Masa veri kısıtları, test boşlukları, masa tutarı ve gönderilmiş hesap

- Task ID: V1-RMD-714
- Status: InProgress
- Assignee: claude-code-session_01CEenPfDb6pEWVcDd9W7VdF
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-601 bulguları B023, B027, B029 ve B030 kapanır. Aktif birleşim ve aktif rezervasyon için kısmi benzersiz indeksler eklenir; eşzamanlılık, tekrar teslim, boş masa, siparişsiz, rezerve ve kısmen ödenmiş birleşim testleri eklenir; masa tutarı birleşik ve çok siparişli masada tüm açık siparişleri toplar; kasaya gönderilmiş siparişin masadan ayrılması sonraki transfer ve birleşimde yanlış sürüklenmez.

## Owned surface

- `plan/v1/remediation/V1-RMD-714-table-db-constraints-docs-and-tests.md`
- `evidence/V1-RMD-714/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/migrations/** - yalnız yeni migration
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json - yalnız yeni migration kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs - yalnız yeni migration kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs - yalnız yeni migration kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Tables/** - yalnız ilgili düzeltme
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Tables/** - yalnız ilgili düzeltme
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/CashierHandoffStore.cs - yalnız gönderilmiş hesabın masa kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Tables/** - yalnız yeni ve düzeltilen test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Tables/** - yalnız yeni ve düzeltilen test
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-601-tables-quality-audit.md - yalnız Out of scope notu
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Kısmi benzersiz indeksler (up ve down), test boşlukları, masa tutarı toplamı, gönderilmiş hesabın masadan ayrılması.

## Out of scope

- Mevcut migration dosyalarını değiştirmek.
- V1-RMD-711 kapsamındaki olay yükü, transfer ve durum ucu düzeltmeleri; V1-RMD-712 rezervasyon sunucu kuralları; V1-RMD-713 istemci ekranları.
- Doküman sapmaları (B032) ve sınır/mimari kalemleri S4–S7: V1-RMD-715.
- B025, B026, B031, B033, olay kimliği tekilleştirmesi ve ölü olay onarımı: V1-RMD-716.
- B020 sunucu kısmı (farklı bölge birleşiminin ayrılması, birleştirme için önce masayı açma) V1-RMD-716.

## Dependencies

- V1-RMD-601

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-714/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-715
