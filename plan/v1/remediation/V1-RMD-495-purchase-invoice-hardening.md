# V1-RMD-495 - Alış faturası hattı sağlamlaştırma (zehirli belge, reddedilen belge, para birimi, indeks)

- Task ID: V1-RMD-495
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

Bağımsız taramanın bulduğu alış faturası hattı risklerini kapatmak: tek bir bozuk belgenin QNB gelen kutusunu kilitlemesi, geçici QNB hatasında belgenin kalıcı atlanması, yabancı para birimli faturanın TRY gibi maliyete girmesi, ürün marj raporunun sorgu indeksi ve satır sınırı hatası.

## Owned surface

- `plan/v1/remediation/V1-RMD-495-purchase-invoice-hardening.md`
- `evidence/V1-RMD-495/**`
- `database/migrations/V1/V1-RMD-495/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json, src/Host/Composition/Migrations/MigrationManifest.cs ve tests/Host/MigrationComposition/Manifest/ManifestTests.cs — yalnız bu görevin migration'ı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Purchasing/PurchaseInvoices/UblPurchaseInvoiceParser.cs, src/Modules/Purchasing/PurchaseInvoices/PurchaseInvoiceInboxService.cs, src/Modules/Purchasing/PurchaseInvoices/PostgresPurchaseInvoiceRepository.cs ve src/Modules/Purchasing/PurchaseInvoices/Exceptions.cs — yalnız belge doğrulama, reddedilen belge ve hata sınıflaması
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Purchasing/QnbPurchaseInvoiceSource.cs ve src/Host/Composition/Errors/ApiErrorCatalog.cs — yalnız reddedilen belge ayrımı ve hata metni
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Reporting/ProductMargin/PostgresProductMarginReportService.cs — yalnız satır sınırı hatasının doğrulama hatası olması
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Purchasing/PurchaseInvoices/UblPurchaseInvoiceParserTests.cs, tests/Modules/Purchasing/PurchaseInvoices/PurchaseInvoiceInboxTests.cs, tests/Modules/Purchasing/PurchaseInvoices/PurchaseInvoiceServiceTests.cs, tests/Modules/Purchasing/PurchaseInvoices/PurchaseInvoiceTestDatabase.cs, tests/Modules/Purchasing/PurchaseInvoices/InvoiceXml.cs ve tests/Modules/Reporting/ProductMargin/ProductMarginReportTests.cs — yalnız bu görevin testleri
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Belge alanları sütun sınırlarını aşarsa veya sayı taşarsa belge 400 ile reddedilir; gelen kutusunda "atlanan" sayılır, imleç ilerler. Sütun sınırı hatası (veri istisnası) 500/503 olmaz.
- QNB'nin teslim etmeyi reddettiği belge, sonraki bir belge başarıyla inene kadar atlanmış sayılmaz; bağlantı/oturum sorunu ihtimaline karşı çekme durur, imleç o belgenin önünde kalır.
- Para birimi TRY dışındaysa fatura reddedilir (kur dönüşümü bu görevde yok).
- `goods_receipt_items(stock_item_id)` indeksi (marj raporu ve ortalama maliyet sorguları için).
- Ürün marj raporunda satır sınırı aşımı 500 yerine doğrulama hatası döner; "iade desteklenmiyor" hata metni güncellenir.

## Out of scope

- Üretim QNB adresinin belirlenmesi (sözleşme gerektirir; adres `ALKAROS_QNB_USER_SERVICE_URL` ile verilir).
- Döviz kuru dönüşümü ve çok para birimli maliyet.

## Dependencies

- V1-RMD-493

## Acceptance evidence

- Testler, mutasyon kanıtı, migration ileri/geri kanıtı ve gerçek Host denemesi; çıktılar `evidence/V1-RMD-495/` altındadır.

## Handoff

- None
