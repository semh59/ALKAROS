# V1-RMD-489 - Alış faturası içeri alma ve ürün eşleştirme

- Task ID: V1-RMD-489
- Status: InProgress
- Assignee: claude-code-session_01XpoF59o3sDPfb7ZADR4BMf
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

Tedarikçiden gelen UBL-TR e-fatura XML'ini yükleyip taslak alış faturası olarak saklamak; fatura satırlarını tedarikçi bazında hammaddeye eşleştirmek ve eşleştirmeyi hatırlamak. Stok girişi bu görevde yoktur.

## Owned surface

- `plan/v1/remediation/V1-RMD-489-purchase-invoice-intake.md`
- `evidence/V1-RMD-489/**`
- `database/migrations/V1/V1-RMD-489/**`
- `src/Modules/Purchasing/PurchaseInvoices/**`
- `tests/Modules/Purchasing/PurchaseInvoices/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Purchasing/PurchaseInvoiceEndpoints.cs — yeni dosya, yalnız alış faturası uç noktaları
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json, src/Host/Composition/Migrations/MigrationManifest.cs ve tests/Host/MigrationComposition/Manifest/ManifestTests.cs — yalnız bu görevin migration'ı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Purchasing/PurchasingModule.cs — yalnız servis kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Purchasing/PurchasingManagementEndpoints.cs — yalnız yeni servis kaydı ve uç nokta eşlemesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Errors/ApiErrorCatalog.cs — yalnız alış faturası hataları
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx — yalnız yeni test projesi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Kesin yollar görev başlatılırken yazılır. UBL-TR ayrıştırıcı (fatura no, ETTN, tarih, tedarikçi VKN/unvan, satır kodu/adı/miktar/birim/KDV hariç birim fiyat); `purchasing.purchase_invoices`, `purchase_invoice_lines`, `supplier_item_mappings` tabloları (migration 175); XML yükleme ve eşleştirme uç noktaları (`purchasing.manage`); aynı ETTN ikinci kez reddedilir; tedarikçi VKN ile mevcut tedarikçiye bağlanır, yoksa eşleşmemiş kalır.

## Out of scope

- Stok girişi (`V1-RMD-490`), yönetim ekranı (`V1-RMD-491`) ve QNB gelen kutusundan çekme (`V1-RMD-492`) bu görevin dışındadır.

## Dependencies

- V1-RMD-488

## Acceptance evidence

- Testler, mutasyon kanıtı ve gerçek Host denemesi; çıktılar `evidence/V1-RMD-489/` altındadır.

## Handoff

- V1-RMD-490
