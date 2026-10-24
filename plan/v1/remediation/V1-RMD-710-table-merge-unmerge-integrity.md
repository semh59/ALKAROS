# V1-RMD-710 - Birleştirme ve ayırmanın doğruluğu

- Task ID: V1-RMD-710
- Status: Done
- Assignee: claude-code-session_01CEenPfDb6pEWVcDd9W7VdF
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-601 bulguları B001, B002, B003, B004, B007, B008 ve N1 kapanır (B030 V1-RMD-711'e devredildi). Boş veya hesapsız masa birleştirilebilir; birleşim her katılımcının tüm açık sipariş ve hesaplarını kaydeder ve ayırmada hepsini geri verir; ayırma işaretçileri geri verilen kayıtları dışlayarak sıralı seçer; ödeme tamamlanınca birleşim kapanır; kapanmış siparişe işaret eden masa kurulmaz; sunucu ana masa için Unmerge komutunu yayınlar ve PosTerminal ayırma düğmesi çalışır. Semih senaryosu (3 ve 4 birleştirilir, ayrılır, ürünler doğru masaya döner) gerçek PostgreSQL testiyle kanıtlanır.

## Owned surface

- `plan/v1/remediation/V1-RMD-710-table-merge-unmerge-integrity.md`
- `evidence/V1-RMD-710/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Tables/** - yalnız ilgili düzeltme
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Tables/** - yalnız ilgili düzeltme
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Tables/** - yalnız yeni ve düzeltilen test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Tables/** - yalnız yeni ve düzeltilen test
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Orders/** - yalnız birleşim kapanışı ve olay tüketicisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/** - yalnız birleşim kapanışı ve olay tüketicisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/tables/** - yalnız Unmerge komutunun ekrana bağlanması
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-601-tables-quality-audit.md - yalnız Out of scope notu
- `database/migrations/V1/V1-RMD-710/**`
- `tests/Host/Experience/Tables/MergeSettlementTests.cs`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json - yalnız yeni migration girdisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs - yalnız PhaseBMax
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs - yalnız yeni migration kimliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/OrderSettlementService.cs - yalnız ödeme sonrası masa ve birleşim kapanışı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/BuildingBlocks/IntegrationContracts/TableIntegrationEvents.cs - yalnız birleşim olaylarına kimlik listeleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Orders/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Billing/** - yalnız yeni test
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Birleşim kaydının çok sipariş ve hesap saklaması, ayırma işaretçi seçimi, ödeme sonrası kapanış, Unmerge komutu ve düğmesi, Semih senaryosu testi.

## Out of scope

- Olay tüketicilerinin olay kimliğiyle tekilleştirilmesi, transfer ve durum ucunun birleşim denetimi (V1-RMD-711).

## Dependencies

- V1-RMD-601

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-710/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-602
