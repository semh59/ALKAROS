# V1-RMD-130 - Independent audit: Kitchen physical print pipeline never dispatched

- Task ID: V1-RMD-130
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız denetimin (2026-09-09) mimari bulgusu: Kitchen'ın fiziksel
yazdırma hattının HER parçası tek tek gerçek ve iyi test edilmişti —
belirlenimci yazıcı yönlendirme (`KitchenPrinterRouter`), kalıcı,
kira-tabanlı bir print kuyruğu (`PrintQueueService`, üstel geri çekilme
ile), çökme-penceresi güvenli teslimat takibi ve operatör onaylı yeniden
yazdırma (`PhysicalPrintRecoveryService`), ESC/POS bilet biçimlendirme
(`EscPosTicketFormatter`) — ama hiçbiri hiçbir zaman gerçekten
ÇALIŞTIRILMIYORDU: hiçbir mutfak bileti hiç bir print job almıyordu
(`IPrintQueueService.EnqueueTicketPrintJobAsync`'i hiçbir üretim kodu
çağırmıyordu), hiçbir print job hiç dispatch edilmiyordu (kuyruğu işleyen
hiçbir hosted service yoktu), ve "yazdıran" tek şey yalnızca testlerde
kullanılan `KitchenPrinterSimulator`'dı — gerçek bir ağ yazıcısına asla
bağlanan hiçbir kod production'da hiç çalışmıyordu. Kullanıcının kararı:
gerçek ağ yazıcısına bağlan (ESC/POS üzerinden TCP, port 9100 — her
termal mutfak yazıcısının (Epson/Star/Bixolon) desteklediği evrensel
"raw"/JetDirect modu).

## Owned surface

- `plan/v1/remediation/V1-RMD-130-kitchen-physical-print-dispatch.md`
  (yeni)
- `database/migrations/V1/V1-RMD-130/**` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik
  iddiası olarak parse etmesin):
  - src/Modules/Kitchen/PrintQueue/IPrinterTransport.cs,
    TcpEscPosPrinterTransport.cs (yeni dosyalar, V1-KIT-003 sahipliğindeki
    dizinin altında) — bu görevin gerçek ağ transportu; başka hiçbir
    dosyaya dokunmadan eklendi.
  - src/Modules/Kitchen/PrintQueue/PrintQueueEnums.cs,
    PrintJob.cs, PrintQueueService.cs (V1-KIT-003 sahipliğinde) — yeni
    `PrintJobStatus.AwaitingOperatorReview`, yeni
    `PrintJob.MarkAwaitingOperatorReview` geçişi, ve
    `ProcessEligibleJobsAsync`'in `PrinterTransmissionUncertainException`'ı
    normal geri-çekilme yerine bu yeni duruma yönlendiren ek `catch` dalı.
    Mevcut hiçbir davranış/durum kodu değişmedi (mevcut testler
    değişmeden geçiyor).
  - src/Host/Experience/KitchenOperations/KitchenPrintDispatchHostedService.cs
    (yeni dosya, V1-RMD-082 sahipliğindeki dizinin altında) — bu görevin
    dispatch döngüsü; başka hiçbir dosyaya dokunmadan eklendi.
  - src/Modules/Kitchen/KitchenModule.cs,
    src/Host/Experience/KitchenOperations/KitchenOperationsEndpoints.cs
    (V1-KIT-003/V1-RMD-082 sahipliğinde) — `IPrinterTransport` kaydı ve
    `KitchenPrintDispatchHostedService`'in `AddHostedService` kaydı
    eklendi.
  - database/MigrationComposition/order.json,
    src/Host/Composition/Migrations/MigrationManifest.cs (`PhaseBMax`),
    tests/Host/MigrationComposition/Manifest/ManifestTests.cs (V1-FND-004
    sahipliğinde) — migration 088 için standart 4 dosyalık desen.
  - tests/Modules/Kitchen/PrintQueue/TcpEscPosPrinterTransportTests.cs
    (yeni dosya, V1-KIT-003 sahipliğindeki dizinin altında),
    PrintQueueUnitTests.cs, PostgresPrintQueueIntegrationTests.cs,
    ALKAROS.Kitchen.PrintQueue.Tests.csproj (V1-KIT-003 sahipliğinde) —
    yeni `AwaitingOperatorReview` geçişini kapsayan testler ve migration
    088 fixture girişleri; mevcut hiçbir test senaryosu değişmedi.
  - tests/Host/Experience/KitchenOperations/KitchenPrintDispatchHostedServiceTests.cs
    (yeni dosya, V1-RMD-082 sahipliğindeki dizinin altında),
    KitchenOperationsTestDatabase.cs (V1-RMD-015 sahipliğinde) — yeni
    `SeedTicketAwaitingPrintJobAsync` yardımcı metodu; mevcut
    `SeedKitchenGraphAsync` hiç değişmedi.

## In scope

1. **Bilet → print job köprüsü.** `KitchenPrintDispatchHostedService
   .BridgeUnprintedTicketsAsync` (statik, testlenebilir) henüz print
   job'u olmayan biletleri bulur (`kitchen.print_jobs`'ta hiç satırı
   olmayan `kitchen.kitchen_tickets`), biletin istasyonunda kayıtlı her
   aktif yazıcı için bir print job kuyruğa alır. Aynı anda birden çok kez
   veya eşzamanlı çalışsa bile idempotent (`EnqueueTicketPrintJobAsync`'in
   (bilet, yazıcı) çiftinden türeyen idempotency key'i + repository'nin
   `ON CONFLICT DO NOTHING`'i).
   - Bilet zaten her zaman geçerli bir `StationId` taşıyor (yönlendirme
     üst akışta etkin olsun olmasın, `KitchenOrderSubmissionDispatcher`
     zaten varsayılan istasyona düşüyor) — bu görev bunu hiç değiştirmedi;
     yalnız istasyon → yazıcı → print job köprüsünü ekledi.
2. **Gerçek ağ transportu.** `TcpEscPosPrinterTransport` — bir yazıcının
   `IpAddress:Port`'una TCP ile bağlanır, ESC/POS init/kesme
   komutlarıyla sarılmış metni yazar. İki farklı istisna türü, geri kalan
   hattın zaten varsaydığı güvenlik ayrımını kodluyor:
   - `PrinterUnreachableException` — bağlantı hiç kurulamadı (reddedildi/
     zaman aşımı/DNS), hiçbir bayt hiç gönderilmedi → normal üstel
     geri-çekilmeyle güvenle yeniden denenebilir.
   - `PrinterTransmissionUncertainException` — bağlantı kuruldu ama
     yazma sırasında koptu; yazıcının kısmen/tamamen bilet ürettiği
     bilinmiyor → otomatik yeniden deneme YASAK (mutfakta çift fiziksel
     baskı riski), operatör kararı gerekiyor.
3. **Kuyruk → gerçek yazıcı, güvenli.** `KitchenPrintDispatchHostedService`
   her 5 saniyede bir: köprüyü çalıştırır, süresi dolmuş kiraları kurtarır
   (`RecoverExpiredLeasesAsync`), ve uygun print job'ları
   `PrintQueueService.ProcessEligibleJobsAsync` ile işler. Yürütücü
   delege'si (`ExecuteDeliveryAsync`): yazıcıyı bulur, transportu çağırır;
   `PrinterTransmissionUncertainException` durumunda
   `IPhysicalPrintRecoveryService.StartInFlightDeliveryAsync` +
   `ReportCrashWindowUncertaintyAsync` ile bir `Unknown` teslimat kaydı
   oluşturup istisnayı yeniden fırlatır.
4. **Yeni, asla-otomatik-yeniden-denenmeyen terminal durum.**
   `PrintJobStatus.AwaitingOperatorReview` + `PrintJob
   .MarkAwaitingOperatorReview` — `PrintQueueService
   .ProcessEligibleJobsAsync`, `PrinterTransmissionUncertainException`'ı
   normal `RecordFailure` (geri-çekilme/`DeadLetter`) yolundan ayırıp bu
   yeni duruma yönlendiriyor; migration 088 `kitchen.print_jobs.status`
   CHECK kısıtını buna göre genişletiyor.

## Out of scope

- Öğe seviyesinde yazıcı yönlendirmenin (`IKitchenPrinterRouter`,
  `IPrinterRepository`, `IPrinterRouteRepository`) gerçek Host
  kompozisyonuna hiç bağlanmamış olması —
  `KitchenOrderSubmissionDispatcher`'ın opsiyonel yapıcı parametreleri
  hâlâ `null` varsayılanında (her bilet tek varsayılan istasyona
  düşüyor). Bu, audit'in ayrı, daha dar bir bulgusu; bu görev yalnız
  "bilet zaten bir istasyona sahip → o istasyondaki yazıcıya fiziksel
  olarak ulaştır" kısmını kapattı.
- `PhysicalPrintDelivery` kayıtlarının BAŞARILI teslimatlar için de
  tutulması — bugün yalnız belirsiz (crash-window) durumlar için
  oluşturuluyor, çünkü şu an tek gerçek tüketici
  (`KitchenOperationsStore.GetUnknownDeliveriesAsync`/reprint onay akışı)
  yalnız bunları görüntülüyor; başarılı her teslimatı da kaydetmek
  gerçek bir tüketicisi olmayan ek karmaşıklık olurdu.
- `TcpEscPosPrinterTransportTests`'te yazma-sırasında-bağlantı-kopması
  senaryosunun gerçek bir soket yarışıyla test edilmesi — ampirik olarak
  loopback'te güvenilir şekilde yeniden üretilemedi (bkz. test
  dosyasındaki yorum); bu dal kod incelemesi + o istisna türünün
  tüketildiği entegrasyon testiyle doğrulandı (bkz. Acceptance evidence).
- Audit'in diğer mimari bulguları (#2 Menu modülü, #6 Production/
  Purchasing, #7 BuildingBlocks ölü kütüphaneleri) — ayrı, kullanıcıyla
  görüşülecek kararlar.

## Dependencies

- V1-KIT-002
- V1-KIT-003
- V1-KIT-004

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 Uyarı, 0 Hata.
- Etkilenen üç test projesi tek bir konteyner çalıştırmasında, gerçek
  Postgres'e karşı çalıştırıldı (boru hattı olmadan, gerçek `$?`
  yakalanarak):
  `ALKAROS.Kitchen.PrintQueue.Tests` (yeni `TcpEscPosPrinterTransportTests`
  - `AwaitingOperatorReview` domain/entegrasyon testleri dahil): 27/27,
  `ALKAROS.Host.Experience.KitchenOperations.Tests` (yeni
  `KitchenPrintDispatchHostedServiceTests` dahil): 7/7,
  `ALKAROS.Host.Tests` (Manifest + Reachability, migration 088 dahil):
  121/121 — gerçek çıkış kodu `0`. (İlk `dotnet test` denemesi, migration
  088 fixture'ı `ALKAROS.Kitchen.PrintQueue.Tests.csproj`'a eklenmeden
  önce başarısız oldu; eklenip aynı şekilde yeniden doğrulandı.)
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 13 önceden var
  olan ihlal (değişmedi), yeni ihlal yok.

## Handoff

- None
