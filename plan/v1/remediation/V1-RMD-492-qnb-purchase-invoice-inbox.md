# V1-RMD-492 - QNB gelen alış faturası çekme

- Task ID: V1-RMD-492
- Status: InProgress
- Assignee: claude-code-session_01XpoF59o3sDPfb7ZADR4BMf
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

QNB gelen kutusundaki alış faturalarını periyodik çekip `V1-RMD-489` taslak hattına beslemek. QNB gelen kutusu sözleşmesi doğrulanmamıştır; istemci sözleşmesi varsayımdır ve QNB'den farklı bir şey çıkarsa revize edilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-492-qnb-purchase-invoice-inbox.md`
- `evidence/V1-RMD-492/**`
- `database/migrations/V1/V1-RMD-492/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json, src/Host/Composition/Migrations/MigrationManifest.cs ve tests/Host/MigrationComposition/Manifest/ManifestTests.cs — yalnız bu görevin migration'ı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Invoicing/Qnb/Client/QnbSoapClient.cs ve src/Modules/Invoicing/Qnb/Client/QnbIncomingDocuments.cs — yalnız gelen belge listeleme ve indirme
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Invoicing/Qnb/Client/QnbSoapClientTests.cs — yalnız gelen belge testleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Purchasing/PurchaseInvoices/IPurchaseInvoiceRepository.cs, src/Modules/Purchasing/PurchaseInvoices/PostgresPurchaseInvoiceRepository.cs ve src/Modules/Purchasing/PurchaseInvoices/PurchaseInvoiceInboxService.cs — yalnız gelen kutusu imleci ve çekme akışı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Purchasing/PurchasingModule.cs ve src/Host/Experience/Purchasing/PurchasingManagementEndpoints.cs — yalnız çekme servisinin kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Purchasing/PurchaseInvoiceEndpoints.cs, src/Host/Experience/Purchasing/QnbPurchaseInvoiceSource.cs ve src/Host/Composition/Errors/ApiErrorCatalog.cs — yalnız çekme ucu
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Purchasing/PurchaseInvoices/PurchaseInvoiceInboxTests.cs ve tests/Modules/Purchasing/PurchaseInvoices/PurchaseInvoiceTestDatabase.cs — yalnız çekme testleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-purchase-invoices/PurchaseInvoicesSection.tsx, src/Clients/PosTerminal/src/features/management-purchase-invoices/PurchaseInvoicesSection.test.tsx, src/Clients/PosTerminal/src/features/management-purchase-invoices/api.ts ve src/Clients/PosTerminal/src/strings.ts — yalnız "QNB'den çek" düğmesi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- QNB gelen kutusu sözleşmesi `evidence/v0/integrations/V0-QNB-001` kod kütüphanesinden alınmıştır (`gelenBelgeleriListeleExt`, `gelenBelgeleriIndirExt`, `connectorService`); canlı sunucuda yalnız `wsLogin` doğrulanmıştır. İstemci bu iki metodu ekler, kimlik bilgisi mevcut QNB kayıt ekranından gelir. Çekme, son alınan sıra numarasından devam eder (imleç `purchasing` şemasında) ve belge bazında hata yalıtılır: iade, bozuk veya yinelenen belge çekmeyi durdurmaz, sonuç özetinde sayılır. Ekrandaki "QNB'den çek" düğmesi ve `POST /purchase-invoices/fetch-qnb` ucu çekmeyi başlatır; gelen fatura `ImportAsync` ile taslak olur (ETTN tekrarı reddedilir). Gerçek QNB'ye karşı denenemez, sahte sunucuyla sınanır.

## Out of scope

- Gerçek QNB hesabıyla doğrulama; fatura onayı otomatikleştirme (onay hep yöneticidedir); zamanlanmış otomatik çekme (sözleşme gerçek hesapla doğrulanana kadar yalnız elle çekilir).

- Çekilen faturalar `V1-RMD-491` ekranında XML ile yüklenenlerle aynı listede görünür.

## Dependencies

- V1-RMD-489

## Acceptance evidence

- Testler, mutasyon kanıtı ve gerçek Host denemesi; çıktılar `evidence/V1-RMD-492/` altındadır.

## Handoff

- None
