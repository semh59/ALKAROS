# V1-RMD-490 - Alış faturası onayı ve stok girişi

- Task ID: V1-RMD-490
- Status: Done
- Assignee: claude-code-session_01XpoF59o3sDPfb7ZADR4BMf
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

Tüm satırları eşleşmiş taslak alış faturasını yönetici onayıyla mal kabul kaydına ve stok girişine çevirmek; maliyet raporu bu girişten beslenir.

## Owned surface

- `plan/v1/remediation/V1-RMD-490-purchase-invoice-approval.md`
- `evidence/V1-RMD-490/**`
- `database/migrations/V1/V1-RMD-490/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json, src/Host/Composition/Migrations/MigrationManifest.cs ve tests/Host/MigrationComposition/Manifest/ManifestTests.cs — yalnız bu görevin migration'ı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Purchasing/PurchaseInvoices/PurchaseInvoiceService.cs, src/Modules/Purchasing/PurchaseInvoices/IPurchaseInvoiceRepository.cs, src/Modules/Purchasing/PurchaseInvoices/PostgresPurchaseInvoiceRepository.cs ve src/Modules/Purchasing/PurchaseInvoices/Exceptions.cs — yalnız onay ve red
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Purchasing/PurchasingModule.cs ve src/Host/Experience/Purchasing/PurchasingManagementEndpoints.cs — yalnız onay servisinin kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Purchasing/OrdersAndReceipts/IGoodsReceiptRepository.cs — yalnız siparişsiz mal kabulün okunması
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Purchasing/PurchaseInvoiceEndpoints.cs ve src/Host/Composition/Errors/ApiErrorCatalog.cs — yalnız onay ve red uçları
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Purchasing/PurchaseInvoices/PurchaseInvoiceApprovalTests.cs ve tests/Modules/Purchasing/PurchaseInvoices/PurchaseInvoiceTestDatabase.cs — yalnız onay testleri
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Kesin yollar görev başlatılırken yazılır. Siparişsiz mal kabul için `goods_receipts.order_id` ve `goods_receipt_items.order_line_id` boş olabilir hale gelir, kayıt faturaya bağlanır (migration 176); onayda miktar çevrim katsayısıyla stok birimine, birim fiyat KDV hariç stok birimi fiyatına çevrilir; stok hareketi ve bakiye mevcut mal kabul yoluyla aynı işlemde yazılır; çift onay ve eşleşmemiş satırla onay reddedilir; fatura reddi.

## Out of scope

- Yönetim ekranı (`V1-RMD-491`), QNB gelen kutusundan çekme (`V1-RMD-492`) ve fatura iptali ile iade mahsubu bu görevin dışındadır.

## Dependencies

- V1-RMD-489

## Acceptance evidence

- Testler, mutasyon kanıtı ve gerçek Host denemesi; çıktılar `evidence/V1-RMD-490/` altındadır.

## Handoff

- V1-RMD-491
