# V1-RMD-802 - Asılı sipariş temizliği, boş sipariş süpürücüsü, ödenmiş hesaba bağlı canlı siparişin onarımı ve G5 artıkları

- Task ID: V1-RMD-802
- Status: InProgress
- Assignee: claude-code-session_019vXeYXjE3sM6VZKpRMj1L9
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesinin G5 grubunun kalan sunucu işleri (`7-birlesik-dogrulama.md` satır 5, 13, 22; `6-sinir-olay.md` B2, B3, B11; `9-1020-kasa-eslesme.md` T05b, T20, T22, T26). V1-RMD-801 siparişi iptal eden uçları verdi; bu görev siparişin yaşam döngüsündeki çıkışsız kalan durumları kapatır ve 801'in açık bıraktığı iki sözü yerine getirir.

1. Boş sipariş süpürücüsü: hiç ürünü olmayan (Draft/Active kalemi bulunmayan) `Draft` siparişlerden 2 saatten uzun süredir dokunulmamış olanları sistem kullanıcısıyla, `CancelInTransactionAsync` ile (masa serbest, denetim kaydı `order.cancelled`, aktör türü `System`) iptal eder. Her sipariş kendi işleminde, satır kilidi altında yeniden kontrol edilir: bu arada ürün eklenmişse, hesap açılmışsa veya sipariş güncellenmişse dokunulmaz. Tekrar deneme güvenlidir (iptal edilmiş sipariş aday değildir). Süre sabittir (ayar için geçiş gerekir).
2. Yönetici "asılı sipariş temizle": mevcut yönetici siparişler aracına (`OrderBacklogAdministration`) iki uç. Asılı sipariş = Cashier/Waiter kaynaklı, canlı (Submitted/Accepted/Preparing/Ready) ve verilen süreden eski, iptal edilmemiş hesabı olmayan sipariş (kasa kuyruğunda hesapsız kalanlar ve canlı yığındaki eski hayaletler). `GET /orders/hanging?olderThanHours=` listeler (sipariş no, durum, yaş, masa, stoğa dönecek ve fire olacak kalem sayısı); `POST /orders/clear-hanging` gerekçe (1-500) ve `dryRun` (varsayılan true) ile temizler: her sipariş kendi işleminde yeniden kontrol edilip 801'in `CancelInTransactionAsync`ine verilir, denetim kaydı önce yazılır, siparişin sonucu tek tek döner (`Cleared`, `Changed`, `HasPayment`, `HasServedItems`, `NotCancellable`); sessiz `failed++` yok.
3. Hesap kapanışı ile sipariş tamamlama ayrı işlem (B3, satır 22, F0410 sınıfı): `BillClosureSweeper` ikinci aday sorgusuyla Paid hesabı olan, kardeş hesapların hepsi kapalı, siparişi hâlâ canlı olanları (hesap `closed_at` bekleme süresinden eski) `CompleteForPaidBillAsync` ile onarır; işlem idempotenttir. `TryCloseBillAsync`teki "elle close-settled" yorumu doğrulanır.
4. 801'in açık bıraktığı üç test boşluğu: birleşik masada iptal, çok hesaplı sipariş, iki kilit turu arasında açılan hesap (kilit tutarak belirleyici test).
5. G5 kayıtları, kaynaktan okunarak: F0058 (`current_bill_id` ayrı işlemde yazılıyor): hesap ekleme `IBillRepository.AddAsync` işlem aşırı yüklemesi ve Tables portunun işaretçi yazan yöntemiyle aynı işlemde yazılır, yutulan hata kalkar; F0925 (hesap kapanış projeksiyonu üç ayrı okuma, iade yarışı): kapanış hesabın `bill-settlement` kilidi altında okur ve yazar; F0396 (kasa kuyruğu olayı `Clients.All`): hub bağlantıda `orders.create` izni olan oturumu kasa kuyruğu grubuna alır, duyuru yalnız o gruba gider; F0451 (`Initiated` listede yok): dört Payment üreticisi (`CashTenderHandler.cs:136`, `AccountChargeHandler.cs:122`, `CardSettlementOrchestrator.cs:102`, `EftTenderHandler.cs:135`) `AddAsync`ten önce `Tender()` çağırır, `Initiated` kalıcı olmaz; durum kümesi her ödeme durumu için testle kilitlenir.
6. 801'in müşteri ekranı bağlantı noktası (810 RESULT.md): hesap iptal ucu ve sipariş iptal ucu `NotifyCustomerDisplayAsync` ile ekrana haber verir; sipariş iptal ucu ve temizleme kasa kuyruğu duyurusunu `Cancelled` değişikliğiyle yapar.
7. Arayüzün okuyacağı sözleşme: `CatalogProductDto.AllowsHalfPortion` (garson ve kasa menü uçları bayrağı taşır; 803 WaiterPwa'da kullanır).

Kapsam dışı: arayüz (V1-RMD-803).

## Owned surface

- `evidence/V1-RMD-802/**`
- `src/Host/Experience/Orders/OrderCleanup/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/SecurityAdministration/OrderBacklogAdministration.cs (V1-RMD-284) — asılı sipariş listesi ve temizleme uçları.
  - src/Host/Experience/SecurityAdministration/SecurityAdministrationEndpoints.cs (V1-RMD-266) — temizleme servislerinin kaydı.
  - src/Host/Experience/Orders/OrderCancellation/OrderCancellationStore.cs (V1-RMD-801) — aktör türü parametresi.
  - src/Host/Experience/Orders/OrderCancellation/OrderCancellationRegistration.cs (V1-RMD-801) — temizleme servisinin kaydı.
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs (V1-BIL-005) — sipariş iptalinde ekran ve kuyruk bildirimi.
  - src/Host/Experience/Orders/CashierQueueAnnouncer.cs (V1-RMD-287) — masasız sipariş için isteğe bağlı masa kimliği.
  - src/Host/Experience/WaiterNotifications/SignalRCashierQueueAnnouncer.cs (V1-WTR-009) — yalnız kasa kuyruğu grubuna duyuru.
  - src/Host/Experience/WaiterNotifications/WaiterOrderStatusHub.cs (V1-WTR-009) — izinli oturumu kasa kuyruğu grubuna alma ve olay yükü.
  - src/Host/Experience/Billing/BillClosureSweeper.cs (V1-RMD-736) — Paid hesaba bağlı canlı siparişin onarımı.
  - src/Host/Experience/Billing/BillCancellation.cs (V1-RMD-506) — iptal sonucu siparişin kimliğini taşır.
  - src/Host/Experience/Billing/BillingSplitApplication.cs (V1-IAM-024) — hesap iptalinde ekran bildirimi ve süpürücü kaydı.
  - src/Host/Experience/Billing/BillingSplitStore.cs (V1-RMD-057) — hesap ve masa işaretçisi tek işlemde.
  - src/Host/DualScreen/DualScreenApplication.Payments.cs (V13-PUI-001) — ortak bildirim yardımcısı, kapanış yorumu.
  - src/Host/DualScreen/DualScreenContracts.cs (V1-RMD-380) — menü ürününde yarım porsiyon bayrağı.
  - src/Host/DualScreen/DualScreenStore.cs (V1-RMD-090) — menü sorgusunda bayrak.
  - src/Modules/Billing/BillFoundation/IBillRepository.cs (V1-RMD-002) — işlem içi ekleme ve kapanış kilidi.
  - src/Modules/Billing/BillFoundation/PostgresBillRepository.cs (V1-RMD-002) — aynı yöntemler.
  - src/Modules/Billing/PaymentClosure/BillClosureService.cs (V13-ALC-002) — kapanışı kilit altında yapma.
  - src/Modules/Tables/TableRelease/ITableReleasePort.cs (V1-RMD-801) — hesap işaretçisi yazan yöntem.
  - src/Modules/Tables/TableRelease/PostgresTableReleasePort.cs (V1-RMD-801) — aynı yöntem.
  - tests/Host/Experience/Billing/OrderCancellationHttpTests.cs (V1-RMD-040) — üç test boşluğu.
  - tests/Host/Experience/Billing/BillCancellationHttpTests.cs (V1-RMD-506) — hesap iptalinde ekran bildirimi testi.
  - tests/Host/Experience/Billing/BillingSplitHttpTests.cs (V1-RMD-103) — hesap ve işaretçi tek işlemde testi.
  - tests/Host/Experience/Orders/TableDraft/CheckLifecycleHttpTests.cs (V1-RMD-773) — duyuru sahte sınıfı yeni imzayı taşır.
  - tests/Host/Experience/WaiterNotifications/WaiterOrderStatusHubGroupTests.cs (V1-RMD-226) — kasa kuyruğu grubu testleri.
  - tests/Host/Experience/WaiterNotifications/ALKAROS.Host.Experience.WaiterNotifications.Tests.csproj (V1-RMD-201) — rol tablolarının geçişi test veritabanına girer.
  - tests/Host/Experience/PaymentTender/BillClosureLoggingTests.cs (V1-RMD-785) — bildirim yardımcısı mesajı.
  - tests/Host/MigrationComposition/DualScreen/CustomerDisplayContractTests.cs (V1-RMD-008) — menü ürünü izin listesi.
  - tests/Modules/Billing/PaymentClosure/BillClosureServiceTests.cs (V1-RMD-298) — sahte depo yeni yöntemleri taşır.
  - tests/Modules/Tables/PaymentTopology/PaymentAwareTableTopologyPolicyTests.cs (V13-TBL-001) — durum kümesi testi.
  - tests/Architecture/ModuleBoundaries/HostForeignWriteLockTests.cs (V1-RMD-002) — BillingSplitStore masa yazımı kalkar, sayım küçülür.
  - tests/Architecture/ApiConventions/api_convention_allowlist.json (V1-RMD-429) — iki yeni yönetici ucu.
  - tests/Architecture/ApiConventions/idempotency_allowlist.json (V1-RMD-429) — temizleme ucu anahtarsız, tekrarı güvenli (temizlenen sipariş aday olmaktan çıkar).
  - tests/Host/Experience/Billing/OrderCleanupTests.cs (V1-RMD-040) — yeni dosya: süpürücü testleri.
  - tests/Host/Experience/Billing/PaidOrderRepairTests.cs (V1-RMD-040) — yeni dosya: onarım testleri.
  - tests/Host/Experience/Billing/ClosureSettlementLockTests.cs (V1-RMD-040) — yeni dosya: kapanış kilidi testleri.
  - tests/Host/Experience/SecurityAdministration/HangingOrdersHttpTests.cs (V1-RMD-266) — yeni dosya: yönetici uç testleri.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Her kabul koşulu için önce kırmızı test (gerçek Postgres), sonra düzeltme, sonra yeşil; mutasyon turu.
2. Süpürücü: tanım sınırında kalan durumlar (kalemleri sonradan iptal edilmiş sipariş, hesabı olan sipariş, yakın zamanda güncellenmiş sipariş, arada ürün eklenmesi) testlidir.
3. Yönetici uçları: yetkisiz, gerekçesiz, dryRun, para var, servis edilmiş kalem var, durum değişmiş, denetim yazılamıyor yolları.
4. İngilizce kod ve test adları; kullanıcıya dönen metinler Türkçe.
5. Migration yoktur (şema değişmez).

## Out of scope

- Arayüz (V1-RMD-803); açık hesaplı siparişin yönetici temizliği (iş kararı: yalnız hesapsız asılı siparişler); ayar ekranı olan zaman aşımı.

## Dependencies

- V1-RMD-801
- V1-RMD-810

## Acceptance evidence

1. Kırmızı sonra yeşil, gerçek exit code ile `evidence/V1-RMD-802/`: OrderCleanupTests, PaidOrderRepairTests, ClosureSettlementLockTests, HangingOrdersHttpTests, OrderCancellationHttpTests, BillCancellationHttpTests, BillingSplitHttpTests, WaiterOrderStatusHubGroupTests, PaymentAwareTableTopologyPolicyTests, BillClosureServiceTests, PaymentTender, SecurityAdministration, ModuleBoundaries, ApiConventions, CustomerDisplayContractTests.
2. Mutasyon: süpürücü yeniden kontrolü, hesap/ürün/yaş koşulu, sistem aktör türü, temizleme denetimi ve izni, para engeli, onarım adayı ve bekleme süresi, kapanış kilidi, işaretçinin aynı işlemde yazılması, kasa kuyruğu grubu, ekran bildirimi bozulunca en az bir test kırılır; kaynak `cmp` ile geri alınır.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. qa-backend ve qa-test-kalitesi incelemeleri sırayla, bulgular giderilir, düzeltmelerden sonra doğrulama turu.
5. F0058, F0925, F0396, F0451 ve 801'in üç test boşluğu `evidence/V1-RMD-802/RESULT.md` içinde tek tek kapatılır (kapanmıyorsa nedeni ve kapanacağı grup yazılır).
6. Semih'in elle deneyeceği senaryo: bir masada ürünsüz sipariş aç, 2 saat bekle (ya da testte zamanı ilerlet): masa boşalır; kasa kuyruğunda eski hesapsız sipariş varsa yönetim ekranından gerekçeyle temizle: kuyruktan kaybolur; hesabı ödenmiş ama masası dolu kalmış sipariş bir dakika içinde kendiliğinden kapanır.

## Handoff

- V1-RMD-803
