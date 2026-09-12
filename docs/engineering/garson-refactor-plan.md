# Garson modülü refactor planı — `OrderManagementStore.cs` ve `waiter-app.js`

- Tarih: 2026-09-12
- Yazan: Claude Sonnet 5
- Bağlam: Semih'in talimatı ("refactor için detaylı ve derin plan yap",
  garson modülü işi bitmek üzereyken yapılan performans/teknik-borç
  değerlendirmesinin doğrudan devamı).
- Durum: **Her iki bölüm de tamamlandı.** Bölüm 1 (`OrderManagementStore.cs`,
  7 adım) — V1-WTR-031..036, 2026-09-12, "Başla" talimatıyla.
  `OrderManagementStore.cs` silindi; yerine `OrderDtoAssembler`,
  `TableDraft/TableDraftService`, `OrderSubmissionCoordinator`,
  `CashierHandoffStore`, `OrderReadStore`, `ShiftSummaryStore` var.
  Bölüm 2 (`waiter-app.js`, 17 adım) — V1-WTR-037..053, 2026-09-12,
  "Devam et" talimatlarıyla. `waiter-app.js` 3295 satırdan 776 satıra
  düştü (%76 azalma); geri kalanı 22 native ES modülüne (`js/util.js`,
  `js/state.js`, `js/auth.js`, `js/api.js`, `js/toast.js`,
  `js/options-sheet.js`, `js/kiosk-lock.js`, `js/features.js`,
  `js/push.js`, `js/offline-queue.js`, `js/screens/tables.js`,
  `js/screens/menu.js`, `js/sheets/party-size.js`, `js/sheets/bill.js`,
  `js/sheets/product-sheet.js`, `js/sheets/void-comp.js`,
  `js/sheets/help-request.js`, `js/sheets/transfer.js`,
  `js/sheets/pending-orders.js`, `js/sheets/failed-orders.js`,
  `js/sheets/profile.js`) taşındı; `waiter-app.js` artık yalnız giriş
  noktası. Her adım kendi build+E2E kanıtıyla, kendi commit'iyle
  kapandı; en riskli modülün (`offline-queue.js`) gerçek çevrimdışı
  davranışı geçici bir smoke testle doğrudan doğrulandı. Refactor'un
  tamamı ayrıca bağımsız bir incelemeden geçti (Semih'in isteğiyle,
  V1-WTR-037..049 arası) — bulunan iki kozmetik sorun (kullanılmayan
  import'lar) düzeltildi, hiçbir davranış regresyonu bulunmadı.
  Regresyon yok.
- Kapsam: yalnız bu iki dosya. Diğer teknik borç maddeleri (E2E'nin CI'a
  bağlanmaması, `IPrePolicyGate` yarışı, V1-WTR-024'ün advisory-lock
  maliyeti) bu planın dışında — ayrı görevler.

## Neden şimdi, neden bu ikisi

Her ikisi de "çalışıyor, testleri yeşil, ama büyüyor" durumunda —
acil bir hata değil, birikmiş bir SRP (single responsibility) erozyonu.
Refactor'un gerekçesi performans değil (ikisi de ölçülebilir bir yavaşlığa
yol açmıyor) — **değişiklik maliyeti**: bu iki dosyaya dokunan HER yeni
görev (yeni bir sipariş kuralı, yeni bir ekran, yeni bir sepet
davranışı), önce 1000+ satırlık tek bir dosyanın tamamını okuyup hangi
bölümün etkilendiğini bulmak zorunda kalıyor, ve testi olmayan (JS
tarafında sıfır otomatik test) bir dosyada bir değişikliğin başka bir
ekranı bozup bozmadığını yalnız elle/E2E ile anlayabiliyoruz.

## Bölüm 1 — `OrderManagementStore.cs` (1285 satır)

### 1.1 Gerçek satır haritası (koddan çıkarıldı, tahmin değil)

```
  76-336   CreateOrUpdateTableDraftAsync   (261 satır) — kamuya açık gövde
 337-374   SendCheckToCashierAsync          (38 satır)
 375-411   GetChecksAwaitingPaymentAsync    (37 satır)
 412-433   GetOrderByIdAsync                (22 satır)
 434-503   WithAvailableStockAsync          (70 satır)  — private, paylaşılan
 504-530   GetActiveOrderByTableIdAsync     (27 satır)
 531-553   TransferServingUserAsync         (23 satır)
 554-584   SubmitOrderAsync                 (31 satır)
 585-647   FireCourseAsync                  (63 satır)
 648-677   GetPendingOrdersAsync            (30 satır)
 683-699   MapModifiers                     (17 satır)  — private, paylaşılan
 700-1054  [CreateOrUpdateTableDraftAsync'in ÖZEL yardımcıları]  (355 satır)
             ResolveModifiersAsync, ResolveValidSeatIdsAsync,
             ResolveApplicableModifierGroupsAsync,
             ValidateModifierGroupSelections, ValidateRequestBounds,
             ItemContentUnchanged, BuildModifiers, ResolveCatalogProductsAsync
1055-1147  [Ortak okuma yardımcıları]        (93 satır)
             GetActiveOrderByTableIdInternalAsync, FindOrderIdBySubmissionAsync
             (2 aşırı yükleme), LoadOrderDtoAsync, GetTableNumberAsync
1148-1198  MapToDto                          (51 satır)  — private, paylaşılan
1199-1285  GetMyShiftSummaryAsync            (87 satır)
```

**Bu haritanın gösterdiği gerçek şey, önceki değerlendirmenin
söylediğinden daha keskin:** dosya "9 eşit ağırlıklı sorumluluk" değil —
**TEK bir sorumluluk (`CreateOrUpdateTableDraftAsync` + kendi 355 satırlık
özel doğrulama/çözümleme yardımcıları = ~615 satır, dosyanın %48'i) dosyanın
neredeyse yarısını kaplıyor**, geri kalanı 8 küçük-orta boy sorumluluğun
bir araya toplanması. Bölme kararı bunu yansıtmalı: en büyük, en izole
edilebilir parça önce çıkmalı.

### 1.2 Hedef mimari

```
src/Host/Experience/Orders/
├── OrderManagementEndpoints.cs         (değişmez — HTTP yüzeyi aynı kalır)
├── OrderManagementContracts.cs         (değişmez)
├── OrderDtoAssembler.cs                [YENİ]
│     WithAvailableStockAsync, LoadOrderDtoAsync,
│     GetActiveOrderByTableIdInternalAsync, FindOrderIdBySubmissionAsync
│     (iki aşırı yükleme), GetTableNumberAsync, MapToDto, MapModifiers
│     — "bir Order'ı OrderDto'ya nasıl çeviririz + mevcut stok bilgisini
│     nasıl ekleriz" sorusunun TEK sahibi. Aşağıdaki üç servisin hepsi
│     buna bağımlı (constructor injection, DI'da singleton/scoped aynı
│     ömürde kalır).
├── TableDraft/
│   └── TableDraftService.cs            [YENİ]
│         CreateOrUpdateTableDraftAsync + kendi 8 private yardımcısı
│         (ResolveModifiersAsync...ResolveCatalogProductsAsync).
│         Bağımlılık: OrderDtoAssembler (LoadOrderDtoAsync,
│         WithAvailableStockAsync için).
│         — Bu, dosyanın en büyük ve en karmaşık parçası; kendi
│         klasörüne çıkması "burada modifier/koltuk/kategori doğrulama
│         mantığı var" sinyalini de veriyor.
├── OrderSubmissionCoordinator.cs       [YENİ]
│         SubmitOrderAsync, FireCourseAsync — ikisi de SubmitOrderHandler/
│         kitchen dispatch'e giden "bir siparişi ileri götür" ailesi.
│         Bağımlılık: OrderDtoAssembler (GetTableNumberAsync için).
├── CashierHandoffStore.cs              [YENİ]
│         SendCheckToCashierAsync, GetChecksAwaitingPaymentAsync —
│         "hesabı kasiyere devret" ailesi, TableDraft'tan tamamen bağımsız.
├── OrderReadStore.cs                   [YENİ]
│         GetOrderByIdAsync, GetActiveOrderByTableIdAsync,
│         TransferServingUserAsync, GetPendingOrdersAsync —
│         basit okuma/tekil-yazma uçları, aralarında ortak durumu yok,
│         hepsi ayrı ayrı endpoint'e bağlı; tek dosyada toplanmaları
│         yalnız "küçük ve az değişen" oldukları için, mantıksal bir
│         birliktelikleri yok — bu grup ilerde büyürse ilk ayrışacak grup.
└── ShiftSummaryStore.cs                [YENİ]
          GetMyShiftSummaryAsync — zaten kendi başına duran, bağımsız bir
          okuma; taşınması en düşük riskli adım.
```

`OrderManagementStore` sınıfının kendisi TAMAMEN kalkar — `OrderManagementEndpoints.cs`
her endpoint'i artık ilgili küçük servise bağlar (`app.MapPost(...).WithMetadata(...)`
satırları aynı kalır, yalnız DI'dan çekilen tip değişir).

### 1.3 Neden bu bölünme, başka türlü değil

- **"Bir metodu değiştirmek için kaç dosya/metot okumam gerekiyor"** sorusunu
  küçültmek asıl hedef. Bugün bir waiter'ın koltuk seçimini değiştirmek
  isteyen biri 1285 satırlık dosyanın tamamını (en azından üst kısmını)
  taramak zorunda; `TableDraftService.cs` sonrası yalnız o dosyayı (yine
  ~650 satır ama TEK sorumluluğa ait) okuması yeter.
- `OrderDtoAssembler` ayrı bir sınıf olmasının nedeni: üç farklı servis
  (TableDraft, Submission, Read) aynı "Order → OrderDto + stok bilgisi"
  dönüşümüne muhtaç — bunu her birine kopyalamak yerine paylaşılan TEK
  bağımlılık olarak kalması, "iki yerde birbirinden sapan DTO eşleme"
  sınıfının hiç oluşmamasını garanti ediyor (bu codebase'te zaten olmuş
  bir hata sınıfı — V1-RMD-147'nin kendi yorumu `MapModifiers`'ı tam bu
  yüzden paylaşılan yaptığını söylüyor).
- `OrderReadStore`'un "mantıksal birlikteliği yok" diye işaretlenmesi
  bilinçli: bunu "geçici" bir grup olarak bırakıyoruz, zorla isim bulup
  yapay bir soyutlama uydurmuyoruz. Gerçek bir ortak nokta ortaya
  çıkmadan (ör. hepsi aynı yetkilendirme desenini paylaşıyor diye) daha
  fazla bölmek YANLIŞ soyutlama riski taşır.

### 1.4 Göç stratejisi — sırayla, her adımda yeşil test

Tek seferde "büyük patlama" refactor YAPILMAYACAK — AGENTS.md'nin "kapsam
dışına çıkma yasağı" ilkesiyle de uyumlu değil, ve bu sınıfı çağıran 7
dosya (`OrderManagementEndpoints.cs`, `OrderManagementContracts.cs`,
`DualScreenStore.cs`, `DualScreenStore.Orders.cs`,
`OrderSubmissionStockDispatcher.cs`, `NfcOrderingStore.cs`, kendisi) göz
önüne alınırsa riskli. Bunun yerine, HER adım kendi görev ID'sini alır,
kendi build+test+gate kanıtını üretir, bir öncekini bozmadan:

1. **Adım 1 — `OrderDtoAssembler` çıkar.** En düşük risk: yalnız private
   yardımcıları taşımak, davranış değişmez. `OrderManagementStore` bu
   yeni sınıfa constructor'dan bağımlı olur, kendi private metotlarını
   sil, delegasyona çevir. Regresyon kanıtı: TÜM mevcut
   `OrderManagementStore` testleri değişmeden geçmeli (public API aynı).
2. **Adım 2 — `ShiftSummaryStore` çıkar.** En bağımsız, en kolay ikinci
   adım — momentum ve desen kanıtlamak için.
3. **Adım 3 — `CashierHandoffStore` çıkar.** İkinci en bağımsız grup.
4. **Adım 4 — `TableDraftService` çıkar.** En büyük ve en riskli adım —
   diğer üçü bittikten, deseni doğruladıktan SONRA yapılmalı. Kendi
   klasörüne (`TableDraft/`) taşınması da bu adımda.
5. **Adım 5 — `OrderSubmissionCoordinator` çıkar.**
6. **Adım 6 — kalan `OrderReadStore`'u ayrı dosyaya taşı, eski
   `OrderManagementStore.cs`'i sil.**
7. **Adım 7 — `OrderManagementEndpoints.cs`'i güncelle** (her endpoint
   doğru yeni servise bağlansın) ve DI kaydını (`Program.cs` veya
   composition kökü — nerede olduğu doğrulanmalı) güncelle.

Her adımdan sonra: `dotnet build` (0/0), ilgili test projeleri (`tests/Host/
Experience/Orders/TableDraft`, `SubmitOrder`, vb. — HANGİLERİNİN
etkilendiği adıma göre değişir), `consistency_audit.py`,
`plan_audit_tool.py validate`. Adım 4 ve 7'den sonra ayrıca
`tests/E2E/WaiterPwa` tam paket bir kez çalıştırılmalı (gerçek HTTP
yüzeyi taşındığı için).

### 1.5 Tahmini efor (bu codebase'in geçmiş görev sürelerine göre kaba sıra)

- Adım 1-3: küçük, birer görev, düşük risk — her biri V1-WTR-021 gibi
  (shift-summary, zaten var) boyutunda bir görev kadar.
- Adım 4: en büyük tek parça — V1-WTR-025 (course management) boyutunda,
  belki biraz daha büyük çünkü kod TAŞINIYOR (yeniden yazılmıyor) ama 8
  yardımcı metodun hepsinin bağımlılıkları teker teker doğrulanmalı.
- Adım 5-7: orta, temizlik + endpoint yeniden bağlama.
- **Toplam: yaklaşık 5-7 ayrı görev ID'si**, art arda, her biri kendi
  kapanış kapısından geçerek. Tek oturumda hepsi bitirilebilir ama TEK
  commit'te değil — her adımın kendi commit'i, kendi kanıtı olmalı (bir
  adım ortada bir hata çıkarırsa geri alınacak birim küçük kalsın diye).

## Bölüm 2 — `waiter-app.js` (3319 satır)

### 2.1 Mevcut durum

Tek bir IIFE (immediately-invoked function expression) içinde ~85
top-level fonksiyon, hepsi aynı closure'ı (modül-seviyesi `let`/`const`
durum değişkenleri: aktif masa, taslak sepet, kullanıcı, vb.) paylaşıyor.
Derleme adımı yok (`<script>` doğrudan tarayıcıya gidiyor) — bu, hem
avantaj (basitlik, build zinciri yok) hem de bugünkü acının kaynağı
(modül sınırı yok, her şey her şeye erişebiliyor).

**Sıfır otomatik test** — bu dosyanın davranışı yalnız
`tests/E2E/WaiterPwa` Playwright paketiyle (gerçek tarayıcı, gerçek Host)
doğrulanıyor. Bu, Bölüm 1'den TAMAMEN FARKLI bir risk profili demek:
C# tarafında bir metodu taşırken "eski testler hâlâ geçiyor mu" diye
bakabiliyoruz; burada TEK doğrulama aracı E2E paketinin kendisi.
**Sonuç: bu modülerleştirme E2E paketinden ÖNCE değil, E2E paketi zaten
var olduğu İÇİN güvenle yapılabilir** — V1-WTR-026 bu refactor'un ön
koşuluydu, şimdi var.

### 2.2 Hedef mimari — native ES modülleri, build adımı yok

Build zincirine (webpack/vite/esbuild) GEÇMEMEK bilinçli bir öneri:
bugünkü "kaydet, F5, gör" döngüsü bu modülün en değerli özelliklerinden
biri (mutfak/kasiyer PC'lerinde npm/node kurulu olması gerekmiyor —
`src/Host` derlenip çalıştırıldığında `wwwroot` olduğu gibi servis
ediliyor). Tarayıcı native `<script type="module">` + `import`/`export`'u
ekstra araç olmadan destekliyor; tek bedel dosya sayısının artması ve
her `<script type="module">`'un kendi scope'unda çalışması (global
değişken sızıntısı otomatik biter — bu aslında bir kazanç).

```
src/Clients/WaiterPwa/wwwroot/
├── waiter-app.js                 [KALIR ama küçülür — yalnız giriş noktası:
│                                   import'lar + bindEvents() çağrısı +
│                                   başlangıç (init) akışı]
├── js/
│   ├── state.js                  [YENİ] — modül-seviyesi paylaşılan durum
│   │     (aktif masa, taslak, kullanıcı, izin seti) + üzerinde çalışan
│   │     saf get/set fonksiyonları. BUGÜN kapalı closure değişkenleri
│   │     olan şey burada AÇIK, import edilebilir hale geliyor.
│   ├── api.js                    [YENİ] — apiUrl, api() sarmalayıcısı,
│   │     401 interceptor, deviceTerminalId, describeHttpFailure.
│   ├── auth.js                   [YENİ] — showLogin, applyUser, can(),
│   │     trapBackgroundExcept/releaseTrap, PIN kilidi (openPinSheet,
│   │     resetIdleTimer, lockScreen, renderPinDots/Pad).
│   ├── screens/
│   │   ├── tables.js             [YENİ] — renderZones, renderTables,
│   │   │     tableAgeMinutes/BadgeHtml.
│   │   ├── menu.js               [YENİ] — renderCategories, renderProducts,
│   │   │     addToDraft, draftQuantityOf, activeItems, draftTotal,
│   │   │     afterDraftChange, lastRound, renderQuickSend.
│   │   ├── product-sheet.js      [YENİ] — openProductSheet,
│   │   │     productSheetHtml, chosenModifiers, toggleModifier,
│   │   │     updateProductSheetTotal, onOptionsConfirm.
│   │   └── bill.js               [YENİ] — renderBill, renderDraftLine,
│   │         renderSentLine, openBill/closeBill.
│   ├── sheets/
│   │   ├── void-comp.js          [YENİ] — openVoidSheet, openVoidSentSheet,
│   │   │     openCompSheet.
│   │   ├── transfer.js           [YENİ] — openTransferSheet.
│   │   ├── help-request.js       [YENİ] — openHelpRequestSheet.
│   │   ├── party-size.js         [YENİ] — openPartySizeSheet,
│   │   │     partySizeSheetHtml, confirmPartySize.
│   │   ├── pending-orders.js     [YENİ] — renderPendingBanner,
│   │   │     openPendingSheet, openSendToCashierSheet.
│   │   ├── failed-orders.js      [YENİ] — openFailedOrdersSheet,
│   │   │     queuedOrderRow, dismissFailedOrder, clearAllFailedOrders.
│   │   └── profile.js            [YENİ] — openProfileSheet.
│   ├── offline-queue.js          [YENİ] — loadDraftsByTable,
│   │     persistDraftsByTable, persistQueue, queueOrder,
│   │     scheduleQueueRetry, checkDelayAndSend, confirmDelayChoice,
│   │     removeSentDraftLines, draftToPayload.
│   ├── push.js                   [YENİ] — registerOfflineWorker,
│   │     pushSupported, iosNeedsInstall, base64UrlToBytes, notify,
│   │     connectHub (SignalR/hub bağlantısı).
│   └── util.js                   [YENİ] — escapeHtml, formatMoney,
│         formatQuantity, formatClock, randomUUID, seatLabel,
│         courseLabel, isFullscreen/onFullscreenChange, measureChrome.
└── index.html                    [DEĞİŞİR] — tek `<script src="waiter-app.js">`
      yerine `<script type="module" src="waiter-app.js">` (tek satır
      değişikliği yeterli, modül grafiği geri kalanı çözer).
```

### 2.3 Neden bu bölünme

- Sınırlar EKRAN/SORUMLULUK bazlı, dosya-boyutu bazlı değil — `sheets/`
  altındaki her dosya "kendi başına açılıp kapanan bir bottom-sheet"
  gerçek UI kavramına karşılık geliyor, kod tabanının kendi vokabülerini
  (bindEvents, openXSheet adlandırma deseni zaten bunu ima ediyor) izliyor.
- `state.js`'in ayrı olması en kritik karar: bugün "closure paylaşımı"
  olan gizli bağımlılık YARIN "açık import" oluyor — bir dosyayı okuyan
  biri hangi paylaşılan duruma dokunduğunu `import { draft, activeTable }
  from './state.js'` satırından görüyor, tüm dosyayı taramadan.
- `offline-queue.js` bilinçli olarak TEK dosya kaldı (bölünmedi) —
  V1-WTR-023/024/027/028'in hepsi bu akışın kendi içindeki sıralama/
  yarış koşullarına dokunmuş; bunu daha fazla parçalamak şu an fayda
  yerine bağlam kaybı riski taşıyor (bu, "her şeyi küçük dosyalara böl"
  kuralının kör uygulanmaması gerektiğinin somut örneği).

### 2.4 Göç stratejisi

JS tarafı C#'tan farklı bir sırayı gerektiriyor çünkü **derleme zamanı
tip kontrolü yok** — bir fonksiyonu yanlış dosyaya taşımak veya bir
`import` unutmak yalnız ÇALIŞMA ZAMANINDA (tarayıcı konsolunda) patlar.
Bu yüzden:

1. **Önce `util.js`'i çıkar** — saf fonksiyonlar, hiçbir paylaşılan
   duruma dokunmuyorlar, en düşük risk, deseni kanıtlamak için ilk adım.
   Sonrasında TAM E2E paketi (`node ... test`, 18/18) çalıştırılmalı —
   bu adımdan sonraki HER adımda aynı tam paket şart, kısmi koşum yeterli
   değil (bir importun eksik olması yalnız o ekrana dokunan testte
   patlar, ilgisiz görünen bir testte değil).
2. **`state.js`'i çıkar** — hâlâ küçük risk (yalnız değişken taşıma) ama
   HERKESİN bağımlı olacağı temel, bu yüzden ikinci.
3. **`api.js`, `auth.js`'i çıkar** — bunlar da geniş çapta paylaşılan
   altyapı, ekran-özel modüllerden önce sağlamlaşmalı.
4. **Ekran modüllerini (`screens/`, `sheets/`) TEK TEK çıkar**, en
   basitten en karmaşığa: `tables.js` → `party-size.js` → `bill.js` →
   `menu.js`/`product-sheet.js` (bunlar birbirine en sıkı bağımlı, aynı
   adımda gitmeleri gerekebilir) → geri kalan sheet'ler.
5. **`offline-queue.js`, `push.js`'i çıkar** — en riskli ikisi, en son
   (V1-WTR-027/028'in dokunduğu akış budur; burada bir hata sessizce
   sipariş kaybına yol açabilir, bu yüzden her adımdan sonra özellikle
   `05-load-and-timing.spec.js` de dahil tam E2E paketi çalıştırılmalı).
6. **`waiter-app.js`'i son haline getir** — yalnız import grafiği +
   `bindEvents()` çağrısı + sayfa yüklenince çalışacak init akışı kalır.

Her adımdan sonra zorunlu kanıt: tam E2E paketi (`tests/E2E/WaiterPwa`,
şu an 18 test) 18/18, VE — Bölüm 1.4'ün C# tarafından farklı olarak —
bu paket JS'in DAVRANIŞINI test ediyor, dosya yapısını değil, bu yüzden
"testler yeşil ama yanlışlıkla dead code bıraktım" riskine karşı her
adımda `grep`'le eski `waiter-app.js`'te o fonksiyonun gerçekten silindiği
(yalnız taşınmadığı, KOPYALANMADIĞI) doğrulanmalı.

### 2.5 Tahmini efor

- Adım 1-3 (util/state/api/auth): tek oturumda bitebilecek küçüklükte,
  toplamı bir orta boy görev (V1-WTR-019 boyutunda).
- Adım 4 (ekranlar): en büyük parça, birkaç ayrı görev ID'si — her ekran
  grubu kendi görev olabilir (tables, menu+product-sheet, bill, geri
  kalan sheet'ler ayrı ayrı).
- Adım 5 (offline-queue/push): küçük dosya sayısı ama en yüksek dikkat
  gerektiren adım — kendi başına bir görev, bol E2E koşumuyla.
- **Toplam: yaklaşık 6-9 ayrı görev ID'si.**

## Bölüm 3 — İki refactor arasındaki sıralama önerisi

**Önce C# (`OrderManagementStore.cs`), sonra JS (`waiter-app.js`).**
Gerekçe: C# tarafının kendi otomatik test ağı var (regresyon anında
görünür); JS tarafının tek doğrulama aracı E2E paketi, ve E2E paketi
HTTP sözleşmesinin (endpoint şekilleri, response alanları) değişmediğini
varsayıyor. C# tarafı bittiğinde HTTP sözleşmesinin hiç değişmediği
(yalnız sunucu tarafı iç yapının değiştiği) kesinleşmiş olur — JS
tarafına geçildiğinde tek değişken JS'in kendisi kalır, iki tarafın aynı
anda değiştiği bir durum hiç oluşmaz.

## Bölüm 4 — Bu planın kapsamadığı, bilinçli olarak ertelenen kararlar

- **TypeScript'e geçiş** — WaiterPwa hâlâ vanilla JS, PosTerminal zaten
  TypeScript+React. Bu plan yalnız modülerleştirme öneriyor, dil/araç
  değişikliği önermiyor — o, Semih'in ayrı bir kararı olmalı (maliyeti çok
  daha yüksek: derleme adımı, tip tanımları, mutfak/kasiyer PC'lerinde
  build zinciri ihtiyacı).
- **`OrderReadStore`'un daha da bölünmesi** — Bölüm 1.3'te açıklandığı
  gibi bilinçli olarak yapılmadı, gerçek bir ortak nokta ortaya çıkana
  kadar beklensin.
- **Her iki dosyanın da otomatik BİRİM testi eklenmesi** — bu ayrı bir
  karar (özellikle JS tarafı için bir test koşucusu/DOM simülasyonu
  gerektirir, bugün hiç yok); refactor'un KENDİSİ mevcut E2E kapsamına
  güveniyor, yeni birim testi eklemiyor. İstenirse ayrı bir görev.
