# V1-RMD-787 - Kasa incelemesi: küçük backend kritikleri (ölü kod, gövde sınırı, işlem anahtarı, izin, tutar denetimleri)

- Task ID: V1-RMD-787
- Status: Done
- Assignee: claude-code-session_019vXeYXjE3sM6VZKpRMj1L9
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesinin 33., 8., 7., 11. ve 12. satırlarındaki, koddan doğrulanmış dokuz küçük kusur:

1. `OperationalReportService.CalculateServiceWindow` ve `ServiceWindowFilter`: üretim çağıranı yok (yalnız kendi testi); silinir.
2. Telemetri uçlarında (tarayıcı hataları, çevrimdışı kuyruk, etkileşimler) gövde sınırı yalnız `Content-Length` başlığına bakar (etkileşimler) ya da hiç yoktur; parçalı (chunked) gönderim sınırı aşar. Uç meta verisine istek gövdesi sınırı konur (sunucu okurken uygular).
3. PosTerminal `submitOrder` her çağrıda yeni `operationId` üretir; ağ hatasından sonra yeniden denemede sunucu tekrarı tanıyamaz. Aynı sipariş ve revizyon için aynı anahtar kullanılır, başarıdan sonra silinir.
4. `pairings/approve` ve `display-sessions/revoke` yalnız terminal oturumu ister; garson oturumu da aynı tarayıcıda giriş yapabildiği için müşteri ekranını bağlayabilir/koparabilir. Ekranı hesaba bağlayan uçla aynı izin (`payments.take`) istenir.
5. Kart yer tutucusunun neden metninde görev kodu (`V13-HUG-001`) API yanıtına ve ödeme geçmişine yazılır; görev kodsuz Türkçe metne çevrilir. Geçmişte yazılmış kayıtlar değişmez, hiçbir kod bu metni karşılaştırmaz.
6. Kart ödemesi: kalandan büyük tutar `Unknown` olarak kaydedilir; kayıttan önce kalan tutar (indirim ve bahşiş dahil, dağıtılmış ödemeler düşülmüş) denetlenir ve aşım `OverAllocationException` (409) olur.
7. EFT: aynı işlem anahtarı farklı tutarla gelirse eski tutarla `Approved` döner; tutar karşılaştırılır, uyuşmazlık 409 `TENDER_IDEMPOTENCY_KEY_REUSED`.
8. Tutar ölçeği: nakit/tahsilat istekleri ve kasa hareketi tutarı en çok iki ondalık ve DB sütunu `NUMERIC(18,2)` sınırını (9.999.999.999.999.999,99) aşmaz; bellekteki yuvarlama ile DB yuvarlaması ayrışmaz. Kasa hareketi tutar hatası bugün eşlenmemiş (500); 400 olur.
9. İade `CompleteManual`: fiş numarası doğrulaması durum denetiminden önce yapıldığı için `Requested` durumdaki nakit talebe yanlış "fiş numarası" hatası verir; durum denetimi öne alınır.

Kapsam dışı (ayrı karar): HSTS/ters vekil konumu ve Google Fonts dış isteği (`PosTerminal/index.html`).

## Owned surface

- `evidence/V1-RMD-787/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Reporting/V1Operations/OperationalReportService.cs (V1-RMD-421) — ölü hesaplayıcı silinir.
  - src/Modules/Reporting/V1Operations/ReportingModels.cs (V1-RPT-001) — `ServiceWindowFilter` silinir.
  - src/Modules/Reporting/Payments/ReportingModels.cs (V1-RMD-325) — silinen yönteme atıf yapan açıklama.
  - tests/Modules/Reporting/V1Operations/ReportingDomainTests.cs (V1-RMD-421) — ölü yöntemin testi silinir.
  - tests/Modules/Reporting/Payments/PaymentSettlementReportTests.cs (V1-RMD-325) — silinen yönteme atıf yapan açıklama.
  - src/Host/Observability/InteractionTelemetry/InteractionTelemetry.cs (V1-RMD-541) — gövde sınırı.
  - src/Host/Observability/BrowserErrors/BrowserErrors.cs (V1-RMD-537) — gövde sınırı.
  - src/Host/Observability/OfflineQueueReport/OfflineQueueReport.cs (V1-RMD-566) — gövde sınırı.
  - tests/Host/Observability/InteractionTelemetry/InteractionTelemetryTests.cs (V1-RMD-541) — parçalı gönderim testi.
  - tests/Host/Observability/BrowserErrors/BrowserErrorsTests.cs (V1-RMD-537) — parçalı gönderim testi.
  - tests/Host/Observability/OfflineQueueReport/OfflineQueueReportTests.cs (V1-RMD-566) — parçalı gönderim testi.
  - src/Clients/PosTerminal/src/api.ts (V12-NFC-003) — sabit operationId.
  - src/Clients/PosTerminal/src/api.test.ts (V12-QRT-001) — yeniden deneme testi.
  - src/Host/DualScreen/DualScreenApplication.Endpoints.cs (V1-CUI-011) — eşleştirme onayı ve ekran sonlandırma izni.
  - tests/Host/MigrationComposition/DualScreen/DualScreenHostTests.cs (V1-GOV-008) — garson 403, kasiyer geçer.
  - src/Modules/Payments/TenderComposition/PendingBankCardTerminalIntegrationHandler.cs (V13-PAY-003) — görev kodsuz neden.
  - tests/Modules/Payments/TenderComposition/TenderCompositionTests.cs (V13-PAY-005) — neden metni.
  - src/Modules/Payments/CardSettlement/CardSettlementOrchestrator.cs (V1-RMD-258) — kalan tutar denetimi.
  - tests/Modules/Payments/CardSettlement/CardSettlementOrchestratorTests.cs (V1-RMD-298) — aşım testi, yeni bağımlılık.
  - src/Modules/Payments/EftTender/EftTenderHandler.cs (V1-RMD-258) — tekrarda tutar karşılaştırması.
  - src/Modules/Payments/EftTender/EftTenderExceptions.cs (V1-RMD-258) — tutar uyuşmazlığı istisnası.
  - tests/Modules/Payments/EftTender/EftTenderHandlerTests.cs (V1-RMD-298) — tutar uyuşmazlığı testi.
  - src/Host/DualScreen/DualScreenApplication.Payments.cs (V13-PUI-001) — tutar uyuşmazlığı 409 cevabı.
  - tests/Host/Experience/PaymentTender/PaymentTenderHttpTests.cs (V1-RMD-258) — EFT tutar uyuşmazlığı ve kart aşımı HTTP.
  - src/Modules/Cash/TenderHandler/CashTenderContracts.cs (V13-CSH-003) — tutar ölçeği ve üst sınır.
  - src/Modules/Payments/TenderRouting/TenderRequest.cs (V13-PAY-003) — tutar ölçeği ve üst sınır.
  - src/Modules/Cash/TransactionLedger/CashTransaction.cs (V1-RMD-241) — tutar ölçeği ve üst sınır.
  - src/Modules/Cash/TransactionLedger/CashTransactionExceptions.cs (V13-CSH-002) — tutar hatası metni.
  - src/Host/Composition/Errors/ApiErrorCatalog.cs (V1-RMD-431) — kasa hareketi tutar hatası 400, ek alan adları.
  - tests/Host/Errors/ApiErrorCatalogTests.cs (V1-RMD-431) — tutar hatası eşlemesi.
  - tests/Modules/Cash/TransactionLedger/CashTransactionTests.cs (V13-CSH-002) — ölçek ve sınır testleri.
  - tests/Modules/Cash/TenderHandler/CashTenderHandlerTests.cs (V1-RMD-409) — ölçek ve sınır testleri.
  - tests/Modules/Payments/TenderRouting/TenderRoutingTests.cs (V1-RMD-229) — ölçek ve sınır testleri.
  - tests/Host/Experience/CashSession/CashSessionHttpTests.cs (V13-CSH-004) — kasa hareketinde üç ondalıklı ve sınır üstü tutar 400.
  - src/Host/Experience/Billing/PaymentRefunds.cs (V1-RMD-508) — durum denetimi öne alınır.
  - tests/Host/Experience/Billing/PaymentRefundHttpTests.cs (V1-RMD-508) — Requested nakit talep testi.
  - src/Modules/Payments/CardSettlement/CardSettlementRequest.cs (V13-PAY-004) — kart tutarında ölçek ve üst sınır.
  - src/Modules/Cash/Contracts/CashSessionCommands.cs (V1-CSH-001) — açılış, sayım ve kapanış tutarında ölçek ve üst sınır.
  - tests/Modules/Cash/Contracts/CashSessionLifecycleTests.cs (V1-CSH-001) — ölçek ve sınır testleri.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Dokuz kusurun her biri için önce kırmızı test, sonra düzeltme, sonra yeşil.
2. Kart yer tutucu neden metni geçmiş kayıtlara dokunmadan değişir.

## Out of scope

- HSTS ve ters vekil konumu, Google Fonts dış isteği (ayrı karar).
- Kasa oturumu açılış/sayım tutarları (kasa hareketi tutarları değil).

## Dependencies

- V1-RMD-786

## Acceptance evidence

1. Kırmızı sonra yeşil: Host, modül ve PosTerminal testleri; ilgili test projeleri küçük gruplarla koşulur.
2. Mutasyon: her düzeltme bozulunca en az bir test kırılır; kaynak `cmp` ile geri alınır.
3. ApiConventions ve ModuleBoundaries yeşil; plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0; PosTerminal lint/tsc temiz.
4. qa-backend ve qa-test-kalitesi incelemeleri sırayla yapılır, bulgular giderilir.
5. Semih'in elle deneyeceği senaryo: ödeme sayfasında kalandan büyük kart tutarı girince "kalan tutarı aşıyor" mesajı; garson hesabıyla müşteri ekranı eşleştirmeyi denemek ve reddedilmesi.

## Handoff

- None
