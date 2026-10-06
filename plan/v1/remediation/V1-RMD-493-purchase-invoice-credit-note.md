# V1-RMD-493 - Alış iade faturası (iade mahsubu)

- Task ID: V1-RMD-493
- Status: Done
- Assignee: claude-code-session_01Q1Zb6gZ32qiWwZMu7oArnd
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

Tedarikçiden gelen iade faturasını (UBL-TR `IADE` tipli fatura veya CreditNote) içeri alıp yönetici onayıyla stoktan düşen bir iade kaydına çevirmek. Alış faturası içeri alma hattı iade faturasını şimdilik reddeder; bu görev o boşluğu kapatır.

## Owned surface

- `plan/v1/remediation/V1-RMD-493-purchase-invoice-credit-note.md`
- `evidence/V1-RMD-493/**`
- `database/migrations/V1/V1-RMD-493/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json, src/Host/Composition/Migrations/MigrationManifest.cs ve tests/Host/MigrationComposition/Manifest/ManifestTests.cs — yalnız bu görevin migration'ı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Purchasing/PurchaseInvoices/UblPurchaseInvoiceParser.cs, src/Modules/Purchasing/PurchaseInvoices/PurchaseInvoiceModels.cs, src/Modules/Purchasing/PurchaseInvoices/PurchaseInvoiceService.cs, src/Modules/Purchasing/PurchaseInvoices/IPurchaseInvoiceRepository.cs ve src/Modules/Purchasing/PurchaseInvoices/PostgresPurchaseInvoiceRepository.cs — yalnız iade belgesi türü, iade onayı ve stoktan düşme
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Purchasing/PurchaseInvoices/UblPurchaseInvoiceParserTests.cs, tests/Modules/Purchasing/PurchaseInvoices/PurchaseInvoiceApprovalTests.cs, tests/Modules/Purchasing/PurchaseInvoices/PurchaseInvoiceInboxTests.cs, tests/Modules/Purchasing/PurchaseInvoices/PurchaseInvoiceTestDatabase.cs ve tests/Modules/Purchasing/PurchaseInvoices/InvoiceXml.cs — yalnız iade testleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-purchase-invoices/models.ts, src/Clients/PosTerminal/src/features/management-purchase-invoices/PurchaseInvoicesSection.tsx, src/Clients/PosTerminal/src/features/management-purchase-invoices/PurchaseInvoicesSection.test.tsx ve src/Clients/PosTerminal/src/strings.ts — yalnız iade rozeti
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-492-qnb-purchase-invoice-inbox.md — yalnız iade faturalarının bu göreve devredildiğini belirten satır
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- İade faturası taslak olarak içeri alınır (belge türü `Return`); QNB gelen kutusundan çekilen iadeler de artık atlanmaz, taslak olur. Satır eşleştirme alış faturasıyla aynıdır (tedarikçi kalemi hatırlanır).
- Onayda aynı işlemde taslak onaylanır ve her satır için stoktan düşülür (iade hareketi); stok yetmiyorsa onay 409 ile reddedilir ve hiçbir şey yazılmaz. İade, orijinal alış faturasının fiyatını değiştirmez: ürün marj raporunun ortalama alış maliyeti alış girişlerinden hesaplanır, iade yalnız stok miktarını düşürür.
- Orijinal faturaya bağlama yalnız belgedeki başvuru numarasını saklar (bilgi amaçlı); zorunlu değildir.

## Out of scope

- Alış faturası içeri alma ve onay hattının alış tarafı (`V1-RMD-489`, `V1-RMD-490`) davranışı değişmez.
- Muhasebe mahsubu, tedarikçi cari bakiyesi ve iade bedelinin ödeme kaydı.

## Dependencies

- V1-RMD-490

## Acceptance evidence

- Testler, mutasyon kanıtı ve gerçek Host denemesi; çıktılar `evidence/V1-RMD-493/` altındadır.

## Handoff

- None
