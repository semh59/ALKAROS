# V1-RMD-143 - Order Accept hiçbir kanalda gerçek stoğu düşürmüyordu

- Task ID: V1-RMD-143
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in sorusu (2026-09-09): "Müşteri siparişi girdi qr dan, sonra garson
onayladı diyelim stok eksilecek mi?" — cevap hayırdı. Cashier/Waiter/NFC/QR
kanallarının hepsinde bir sipariş `Accepted` durumuna geçtiğinde hiçbir yerde
gerçek stok/porsiyon düşmüyordu. V11-RSV-001/002/003'ün
`PortionReservationLifecycleService`'i bile "Consumed" geçişinde yalnız
`reserved_quantity`'i serbest bırakıyor, `on_hand_quantity`'e hiç dokunmuyordu
— ve bu alt sistemin sıfır çağıranı vardı. `inventory.product_stock_mappings`
şeması (V11-INV-004) ve `StockMasterService` V1.1'den beri vardı ama hiçbir
HTTP yüzeyi olmadan duruyordu — Menu/Purchasing/Production'ın kendi
remediation'larıyla aynı "yazıldı ama hiç bağlanmadı" deseni. Kullanıcının
kararları: tüm kanallar, Accept anında, kataloğdaki tüm satılabilir ürünler
için gerçek düşüm; eşlenmemiş ürün veya yetersiz stok Accept'i tamamen
reddeder (sessiz atlama yok); garson bekleyen bir siparişi görürken kalan
stoğu da görsün ("Kalan stok bilgisi ver garsona").

## Owned surface

- `plan/v1/remediation/V1-RMD-143-order-accept-real-stock-consumption.md`
  (yeni)
- `src/Host/Experience/Orders/OrderStockConsumption/**` (yeni) —
  `OrderStockConsumptionService` ve ilgili exception'lar.
- `src/Host/Experience/Inventory/**` (yeni) — `StockMasterEndpoints`,
  `StockMasterContracts` (manager-only `inventory.manage` HTTP yüzeyi).
- `database/migrations/V1/V1-RMD-143/**` (yeni) — migration 093
  (`inventory.manage` izni).
- `tests/Host/Experience/Inventory/**` (yeni) — `StockMasterEndpoints`'in
  kendi HTTP testleri (auth/permission reddi, konum/kalem CRUD, ürün-stok
  eşleme atama+listeleme+`AvailableQuantity` hesaplaması).
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik
  iddiası olarak parse etmesin):
  - src/Host/Experience/Orders/PendingOrderConfirmation/PendingOrderConfirmationStore.cs
    (V1-RMD-1xx Confirmation sahipliğinde) — AcceptAsync artık
    OrderStockConsumptionService'i aynı transaction'da çağırıyor.
  - src/Host/Experience/NfcOrdering/NfcOrderingStore.cs,
    src/Host/Experience/NfcOrdering/NfcOrderingEndpoints.cs (V12-NFC-00x
    sahipliğinde) — NFC'nin kendi güvenilir anında-onay yolu da aynı
    servisi çağırıyor; idempotent replay'lerde çifte tüketimi önlemek için
    CanTransitionTo(Accepted) koruması eklendi.
  - src/Host/Experience/Orders/OrderManagementStore.cs,
    src/Host/Experience/Orders/OrderManagementContracts.cs,
    src/Host/Experience/Orders/OrderManagementEndpoints.cs (Orders
    Management sahipliğinde) — GetOrderByIdAsync artık her kalem için
    salt-okunur AvailableStockQuantity hesaplıyor (garsonun kalan stoğu
    görmesi); OrderManagementExceptionFilter yeni stok exception'larını
    Türkçe 409'a haritalıyor.
  - src/Host/DualScreen/DualScreenApplication.cs (ana Host kompozisyonu
    sahipliğinde) — AddStockMasterExperience()/MapStockMasterApi()
    çağrıları eklendi.
  - database/MigrationComposition/order.json,
    src/Host/Composition/Migrations/MigrationManifest.cs (PhaseBMax),
    tests/Host/MigrationComposition/Manifest/ManifestTests.cs (V1-FND-004
    sahipliğinde) — migration 093 için standart 4 dosyalık desen.
  - ALKAROS.slnx (paylaşılan) — yeni
    ALKAROS.Host.Experience.Inventory.Tests eklendi.
  - tests/Host/Experience/NfcOrdering/**,
    tests/Host/Experience/Orders/Confirmation/**,
    tests/Host/Experience/Orders/TableDraft/** (ilgili görevler
    sahipliğinde) — Inventory şema migration'ları (059/060/061/087)
    eklendi, yeni stok tüketimi/görünürlük senaryoları için regresyon
    testleri eklendi, Confirmation'ın kendi önceden var olan
    `ALKAROS_KITCHEN_STATION_ID` ortam değişkeni eksikliği (bu görevden
    önce hiç GET /{orderId} yolu ile tetiklenmemişti) giderildi.
  - src/Host/Experience/Orders/SentItemVoid/SentItemVoidStore.cs,
    src/Host/Experience/Orders/SentItemVoid/SentItemVoidContracts.cs
    (V1-IAM-027 sahipliğinde) — 2026-09-09 derin inceleme sonrası: kalem
    hâlâ `KitchenState.Sent`iken (mutfak hiç başlamamışken) `void-sent`
    ile iptal edilirse, Accept anında düşülen stoğu artık gerçekten geri
    veriyor (`IStockMovementReversalService`, V11-INV-003, önceden sıfır
    çağıranı olan hazır bir modül); `Preparing`/`Ready` değişmedi (stok
    tüketilmiş kalıyor, `docs/domain/void-complimentary-discount-policy.
    md`'nin Waste tanımına uyuyor).
  - docs/domain/void-complimentary-discount-policy.md (V0-DOM-006
    sahipliğinde) — yeni "## Amendment (2026-09-09)" bölümü, yukarıdaki
    kararı Semih'in onaylı karar kaydı olarak dokümante ediyor.
  - tests/Host/Experience/Orders/VoidSent/** (V1-IAM-027 sahipliğinde) —
    stok geri verme senaryoları için 2 yeni test + gerçek tüketim
    hareketi + bakiye tohumlayan yeni fixture yardımcıları.

## In scope

1. **Gerçek, atomik stok düşümü Accept anında.** `OrderStockConsumptionService`
   Production'ın kendi kanıtlanmış deseniyle aynı ilkel'i (
   `IStockBalanceRepository.TryApplyGuardedOnHandDeltaAsync` + gerçek
   `StockMovement` kaydı, `StockMovementSourceType.Order`) kullanıyor —
   tek bir yarış-güvenli SQL UPDATE, yetersiz stokta `null` dönüyor.
   Sipariş `Accepted`'e geçebilen İKİ çağrı noktasının (
   `PendingOrderConfirmationStore.AcceptAsync`,
   `NfcOrderingStore.LoadDtoAfterEnsuringAcceptedAsync`) ikisi de aynı
   transaction içinde bu servisi çağırıyor — Cashier/Waiter/QR/yaş
   sınırlı-NFC birincisinden, güvenilir-anında-kabul NFC yolu ikincisinden
   geçiyor.
2. **Eşlenmemiş ürün veya yetersiz stok Accept'i tamamen reddediyor.**
   `ProductStockNotConfiguredException`/`InsufficientOrderStockException`
   fırlatıldığında transaction hiç commit edilmiyor, sipariş
   `PendingConfirmation`'da kalıyor, garson net Türkçe hata görüyor —
   sessiz atlama veya kısmi düşüm yok.
3. **Manager-only StockMaster HTTP yüzeyi.** `inventory.product_stock_mappings`
   şeması ve `StockMasterService` V1.1'den beri vardı ama hiç HTTP'den
   erişilemiyordu; birinin ürün↔stok kalemi eşlemesini gerçekten
   tanımlayabilmesi için `/api/v1/management/inventory/**` eklendi
   (stok konumu/kalem CRUD-lite, ürün-stok eşleme atama+listeleme).
4. **Garsona kalan stok görünürlüğü.** `OrderManagementStore`'un sipariş
   döndüren HER yolu (`GetOrderByIdAsync`, masaya göre görüntüleme, taslak
   oluşturma/ekleme yanıtı) artık her kalem için gerçek BOM aritmetiğiyle
   (`Math.Min` üzerinden çoklu eşleme sınırlayıcı faktörü)
   `AvailableStockQuantity` döndürüyor; eşlenmemiş ürün için `null`
   (yanıltıcı sıfır değil). Salt bilgilendirme — tek yetkili kapı hâlâ
   Accept anındaki `OrderStockConsumptionService`.
5. **Ürün-stok eşlemesini kaldırma** (2026-09-09 derin inceleme).
   `IProductStockMappingRepository.RemoveAsync` V1.1'den beri vardı ama
   hiç çağrılmıyordu — yanlış yapılandırılmış bir eşlemeyi düzeltmenin
   hiçbir yolu yoktu. `DELETE /api/v1/management/inventory/products/
   {productId}/stock-mappings/{stockItemId}` eklendi; eşleme yoksa 404.
6. **Accept-sonrası "sent" bir kalemin iptalinde stok iadesi** (2026-09-09
   derin inceleme, Semih'in kararı). `SentItemVoidStore`, mutfak henüz
   başlamamışken (`KitchenState.Sent`) iptal edilen bir kalemin Accept
   anında düşülen stoğunu artık gerçekten geri veriyor — hazır,
   önceden hiç çağrılmayan `IStockMovementReversalService`
   (V11-INV-003) üzerinden gerçek bir `Reversal` hareketi kaydederek.
   `Preparing`/`Ready` değişmedi (stok tüketilmiş kalıyor).
   `docs/domain/void-complimentary-discount-policy.md`'ye bu kararı
   belgeleyen "## Amendment (2026-09-09)" eklendi. Bunu doğru
   hedefleyebilmek için `OrderStockConsumptionService`'in kendi
   `StockMovement.sourceReferenceId`'i artık `order.Id` değil
   `item.Id` — bir siparişin başka bir kaleminin tüketimi hiç
   etkilenmiyor.

## Out of scope

- `PortionReservationLifecycleService`'in kendi "Consumed" geçişinin
  `on_hand_quantity`'e hiç dokunmaması — bağımsız, daha derin bir
  bulgu (V11-RSV-001/002/003, hâlâ sıfır çağıranı var); ayrı karar.
- StockMaster HTTP yüzeyinin konum/kalem güncelleme veya pasifleştirme
  uç noktaları — yalnız oluşturma+listeleme+eşleme+eşleme kaldırma, bu
  görevin gerçek ihtiyacı (bir ürünü bir stok kalemine bağlayıp
  düzeltebilmek).
- Docker Compose üzerinden manuel uçtan uca doğrulama — Semih'in kendi
  eliyle deneyebileceği senaryo aşağıda tarif edildi, ayrı bir çalıştırma
  bu görevin kapanışını beklemedi.
- `OrderItemModifier` (bir sipariş kaleminin modifier'ları, ör. "ekstra
  peynir" — kendisi de `catalog.products` (`ProductType.Modifier`)
  içinde ayrı bir ürün) hiç stok tüketmiyor —
  `OrderStockConsumptionService` yalnız `item.ProductId`'ye bakıyor,
  `item.Modifiers`'a hiç bakmıyor. Bulundu (2026-09-09 derin inceleme)
  ama kasıtlı olarak bu görevin kapsamına alınmadı: modifier'ların kendi
  BOM'u olup olmayacağı, eşlenmemiş bir modifier'ın tüm Accept'i
  reddedip reddetmeyeceği gibi sorular Semih'in ayrı kararını gerektirir
  — "kataloğdaki tüm satılabilir ürünler" kapsamı yorumlanırken bu görev
  boyunca hep `OrderItem.ProductId` (ana ürün) anlamında kullanıldı.

## Dependencies

- None

## Acceptance evidence

- `dotnet build ALKAROS.slnx`: 0 Uyarı, 0 Hata.
- Gerçek Postgres'e karşı (`alkaros-test-pg`, ayrı ayrı çalıştırıldı,
  gerçek çıkış kodu yakalanarak):
  - Yeni `ALKAROS.Host.Experience.Inventory.Tests` (auth/permission reddi,
    konum/kalem oluşturma+listeleme, tekrarlı kod çakışması reddi,
    ürün-stok eşleme atama+listeleme+`AvailableQuantity` hesabı,
    eşlenmemiş ürün boş liste, bilinmeyen stok kalemine eşleme reddi):
    7/7.
  - `ALKAROS.Host.Experience.Orders.Confirmation.Tests` (Accept'in gerçek
    stok düşürdüğü, yetersiz stokta reddettiği, eşlenmemiş üründe
    reddettiği, `GetOrderByIdAsync`'in kalan stoğu gösterdiği yeni
    senaryolar dahil): 14/14.
  - `ALKAROS.Host.Experience.NfcOrdering.Tests` (güvenilir anında-kabul
    yolunun da stok tükettiği, eşzamanlı aynı-sipariş tekrar
    gönderimlerinin çifte tüketmediği dahil, 3 ardışık çalıştırmada
    kararlı): 17/17.
  - `ALKAROS.Host.Experience.Orders.TableDraft.Tests` (yeni Inventory
    şema bağımlılığı, submit-draft yanıtının GetOrderByIdAsync üzerinden
    kurulması): 14/14.
  - `ALKAROS.Host.Experience.Production.Tests` (bu görev
    `AddStockMasterExperience()`'a eklediği `IUnitConverter` kaydının
    Production'ın kendi bağımsız kaydını bozmadığını doğrulamak için
    regresyon): 4/4.
  - `ALKAROS.Inventory.StockMaster.Tests` (modül seviyesi, bu görev
    kaynak koduna dokunmadı, regresyon yok): 14/14.
  - `ALKAROS.Host.Tests` (Manifest migration 093 girişi + Reachability +
    Composition, her modül servisini çözdüğünü doğruluyor): 133/133.
- Migration 093: boş veritabanında up (`inventory.manage` izni +
  `manager` rolüne bağlanması) ve down (izin ve rol-izin satırının
  silinmesi) ikisi de `ALKAROS.Host.Experience.Inventory.Tests`'in kendi
  fixture'ı üzerinden (tam migration geçmişini uygulayan
  `StockMasterTestDatabase`) doğrulandı.
- Gerçek Docker Compose uçtan uca doğrulama (`docker compose build
  api migrate provision` + `docker compose up -d --wait --force-recreate
  migrate provision api`, migration 093'ün gerçekten uygulandığı
  (`[093] applied ...` log satırı) doğrulanarak), Semih'in kendi eliyle
  deneyebileceği tam senaryo gerçekten çalıştırıldı:
  1. `admin` ile giriş yapıldı; oturumun `inventory.manage` iznini
     gerçekten taşıdığı doğrulandı.
  2. `POST /api/v1/management/inventory/stock-locations` ve
     `.../stock-items` ile gerçek bir stok konumu ve kalemi oluşturuldu;
     `POST .../products/{productId}/stock-mappings` ile bir kataloğ
     ürünü o kaleme eşlendi (eşleme henüz bakiyesizken liste yanıtı
     `availableQuantity: null` döndü — yanıltıcı sıfır değil).
  3. Kalemin başlangıç bakiyesi 5 birim olarak dolduruldu; eşleme
     listesi artık `availableQuantity: 5` gösterdi.
  4. NFC müşteri uç noktasından (`nfc.<host>` vhost, gerçek Caddy
     origin izolasyonu üzerinden) 3 birimlik gerçek bir sipariş girildi
     — güvenilir anında-kabul yolu siparişi doğrudan `Accepted`'e
     taşıdı ve stok kalemi gerçekten 5'ten 2'ye düştü; ilişkili
     `inventory.stock_movements` satırı gerçek sipariş kimliğine
     (`source_type = Order`, `source_reference_id = <orderId>`)
     bağlı olarak oluştu.
  5. Aynı ürünü kalan 2 birimden fazla (5 birim) isteyen ikinci bir NFC
     siparişi `PendingConfirmation`'da kaldı (güvenilir yol stok
     yetersizliğini yakalayıp reddetti); yönetici/garson ekranının
     kullandığı `GET /api/v1/terminals/{terminalId}/orders/{orderId}`
     bu bekleyen siparişte `availableStockQuantity: 2` gösterdi (kalan
     stok garsona görünür); `POST .../accept` denemesi gerçek stok
     hâlâ 2'de sabitken 409 `INSUFFICIENT_STOCK` ve Türkçe "'E2E Test
     Urun' için yeterli stok yok." hatasıyla reddedildi, sipariş
     `PendingConfirmation`'da ve stok 2'de değişmeden kaldı.
  6. Hiç eşlemesi olmayan yeni bir ürün için NFC siparişi de
     `PendingConfirmation`'da kaldı; `POST .../accept` denemesi 409
     `PRODUCT_STOCK_NOT_CONFIGURED` ve Türkçe "'E2E Unmapped Product'
     için stok tanımlanmamış, lütfen yöneticiye bildirin." hatasıyla
     reddedildi.
  Not: `inventory.stock_movements` kasıtlı olarak değiştirilemez/
  silinemez (append-only denetim izi, `prevent_stock_movement_mutation`
  trigger'ı ile korunuyor) — bu doğrulamanın oluşturduğu test satırları
  bu yüzden ortamda kalıcı kaldı; bu, bu görevin bir yan etkisi değil,
  önceden var olan tasarım kararı.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 1 önceden var
  olan ihlal (`InventoryAdjustmentService.cs:96`, bu görevden bağımsız,
  dokunulmadı), yeni ihlal yok — bu görev sırasında `StockMasterEndpoints.
  cs` ve `OrderStockConsumptionService.cs`'deki Türkçe yorum bloklarının
  ürettiği 22 yeni ihlal İngilizceye çevrilerek giderildi.

### Derin gözden geçirme (Semih'in isteğiyle, ilk commit'ten sonra, 2026-09-09)

İlk commit (`6cddb32e`) sonrası Semih'in "yapılanları detaylı ve derin
kontrol et" isteği üzerine kod tekrar satır satır incelendi. İki gerçek
kusur bulundu ve düzeltildi (üçüncüsü kasıtlı olarak "Out of scope"a
eklendi, dördüncüsü ayrı, bu görevden bağımsız bir kusur olarak
raporlandı):

1. **İptal edilmiş kalem yine de stok tüketiyordu.** Bir garson,
   sipariş hâlâ `PendingConfirmation`'dayken bir kalemi iptal
   edebiliyor (`ItemExceptionHandler.VoidItemAsync` / `SentItemVoidStore`
   — ikisi de `order.Status`'a değil yalnız kalemin kendi
   Status/KitchenState'ine bakıyor; `PendingConfirmation` siparişlerin
   kalemleri genelde zaten `KitchenState.Sent` olduğundan "void-sent"
   yolu gerçekçi bir senaryo). `OrderStockConsumptionService` `order.
  Items`'ı hiç filtrelemeden geziyordu — iptal edilen kalem hâlâ listede
   kalıyor ve Accept'te onun için de stok aranıyordu; ürünün eşlemesi
   yoksa (artık siparişte olmayan bir ürün için) TÜM Accept haksız yere
   reddediliyordu. Düzeltme: `item.Status == OrderItemState.Cancelled`
   olan kalemler atlanıyor (Complimentary kalemler hâlâ tüketiyor —
   onlar gerçekten hazırlanıp servis ediliyor). Yeni regresyon testi
   (`AcceptingAnOrderWithACancelledItemNeverConsumesItsStock`) düzeltme
   olmadan gerçekten kırmızı olduğu doğrulanarak eklendi.
2. **Accept iki ayrı transaction'a bölünmüştü — Production'ın V1-RMD-133
   ile kendi kapattığı aynı hata sınıfı.** `PendingOrderConfirmationStore.
   AcceptAsync` stok tüketimini kendi transaction'ında commit ediyor,
   SONRA siparişin `Accepted` yazısını AYRI bir transaction'da
   kaydediyordu. Aradaki optimistic-concurrency kontrolü paylaşılmadığı
   için, stok tüketimi commit olduktan sonra sipariş yazısı eski row
   version yüzünden başarısız olursa (ör. o sırada başka biri kalemi
   iptal ettiyse), stok kalıcı olarak düşmüş ama sipariş hiç Accepted'e
   geçmemiş oluyordu. Düzeltme: ikisi artık TEK transaction'da,
   `IOrderRepository.SaveAsync`'in connection/transaction taşıyan
   overload'ı üzerinden. Aynı kusur `NfcOrderingStore`'un kendi
   "TryConsumeStockAsync" + ayrı `TryTransitionAsync(Accepted)"
   ikilisinde de vardı — üstelik orada bunu tek transaction'da
   birleştirmek, önceki oturumda "tam olarak teorik kapatılmadı" diye
   not düşülen NFC eşzamanlı çifte-tüketim yarışını da gerçekten
   kapatıyor (kaybeden isteğin row-version korumalı UPDATE'i satır
   kilidine takılıp sıfır satır etkiler ve o transaction'ın kendi stok
   tüketimini de birlikte geri alır).`TryConsumeStockAsync` bu yüzden
   `TryConsumeStockAndAcceptAsync` olarak yeniden yazıldı. Regresyon:
   Confirmation 15/15 (yeni test dahil), NfcOrdering 17/17 (3 ardışık
   koşuda kararlı).
3. Modifier'ların stok tüketmemesi — yukarıdaki "Out of scope"a eklendi.
4. **Bu görevden bağımsız, önceden var olan bir kusur bulundu (bu görev
   sırasında düzeltilmedi — kapsam dışı):**
   `OrderManagementTableDraftHttpTests.
   ConcurrentIdenticalFirstSubmissionsForATableResolveToTheSameOrder`
   bu makinede %100 tekrarlanan bir gerçek yarış koşulu ortaya çıkardı:
   `OrderManagementStore.CreateOrUpdateTableDraftAsync`'in
   `orderNumber = $"TBL-{tableNumber}-{now:HHmmssff}"` üretimi, iki
   gerçekten eşzamanlı (`Task.WhenAll`) istek Windows'un kaba saat
   çözünürlüğü yüzünden aynı `HHmmssff` değerini üretince
   `orders_order_number_key` tekilliğini ihlal ediyor — kod yalnız
   `ux_orders_table_submission` ihlalini yakalıyor, bu farklı kısıtı
   yakalamıyor, 503'e düşüyor. `git stash` ile bu görevin TÜM
   değişiklikleri geri alınıp son commit'teki (`6cddb32e`) hal üzerinde
   3 kez tekrar çalıştırılarak doğrulandı: hata aynı şekilde ORADA DA
   var — bu görevin bir regresyonu değil, `OrderManagementStore`'un
   (V1-RMD-107/123 kökenli) kendi önceden var olan kusuru. Ayrı bir
   Task ID gerektirir (order_number üretimine gerçek bir çakışmasızlık
   garantisi — ör. bir sequence veya rastgele son ek — eklemek);
   Semih'e ayrıca bildirildi.

### "Başka ne eksikler var" turu (Semih'in isteğiyle, 2026-09-09)

Semih'in "başka ne eksikler var" sorusu üzerine dört yeni bulgu
raporlandı; Semih üçünü şimdi kapatmayı seçti (eksik test kanıtları,
eşleme kaldırma uç noktası, Accept-sonrası stok iadesi), dördüncüsü
(modifier'lar) "Out of scope"a eklendi (yukarıda).

- **Eksik test kanıtları kapatıldı**: çoklu-BOM (2 stok kalemli bir
  ürün, biri yetersiz) atomik geri alma testi
  (`AcceptingAnOrderWithATwoIngredientBomAppliesNeitherDeltaWhenOneIsInsufficient`)
  ve Complimentary kalemin stok tüketmeye devam ettiği test
  (`AcceptingAnOrderWithAComplimentaryItemStillConsumesItsStock`) —
  `ALKAROS.Host.Experience.Orders.Confirmation.Tests`: 17/17 (15'ten).
- **Eşleme kaldırma**: `DELETE /api/v1/management/inventory/products/
  {productId}/stock-mappings/{stockItemId}`, gerçek 404 (eşleme yoksa)
  - 204 (varsa, yalnız hedeflenen çift silinir, aynı ürünün diğer
  eşlemeleri dokunulmadan kalır) — `ALKAROS.Host.Experience.Inventory.
  Tests`: 9/9 (7'den).
- **Accept-sonrası stok iadesi**: `docs/domain/void-complimentary-
  discount-policy.md`'ye "## Amendment (2026-09-09)" eklendi (Semih'in
  onayıyla, resmi karar kaydı formatında). `SentItemVoidStore.VoidAsync`
  artık `KitchenState.Sent`ken iptal edilen bir kalemin Accept'te
  düşülen stoğunu `IStockMovementReversalService.ReverseMovementAsync`
  ile gerçekten geri veriyor; `Preparing`/`Ready` değişmedi. İki yeni
  regresyon testi (`VoidingASentItemBeforeTheKitchenStartedRestoresItsStock`,
  `VoidingAPreparingItemDoesNotRestoreItsStock`) — ilki, geri verme
  çağrısı devre dışı bırakılınca gerçekten kırmızı olduğu doğrulanarak
  eklendi — `ALKAROS.Host.Experience.Orders.VoidSent.Tests`: 12/12
  (10'dan).
- Regresyon (gerçek Postgres'e karşı, ayrı ayrı): NfcOrdering 17/17,
  TableDraft 14/14, Production 4/4, Void 5/5, Comp 9/9,
  `ALKAROS.Inventory.StockMaster.Tests` 14/14,
  `ALKAROS.Inventory.MovementReversal.Tests` 16/16,
  `ALKAROS.Inventory.MovementLedger.Tests` 18/18,
  `ALKAROS.Inventory.BalanceProjection.Tests` 12/12,
  `ALKAROS.Host.Tests` (Manifest+Reachability+Composition) 133/133 —
  hepsi değişmeden geçti. `dotnet build ALKAROS.slnx`: 0/0.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata,
  0 uyarı. `python tools/consistency-audit/consistency_audit.py`: bu
  turda `SentItemVoidStore.cs`'nin yorumunda alıntılanan Türkçe politika
  metninin ürettiği 2 yeni ihlal İngilizceye çevrilerek giderildi;
  geriye yalnız önceden var olan, ilgisiz 1 ihlal kaldı.
- Gerçek Docker Compose uçtan uca doğrulama (aynı ortam, `docker compose
  build api` + `up -d --wait --force-recreate api`, yeni migration
  gerekmedi): gerçek bir NFC siparişi kabul edilip stok gerçekten 10'dan
  9'a düştü; kalem `Sent` durumuna zorlanıp `void-sent` çağrıldığında
  `stockRestored: true` döndü ve `inventory.stock_movements`'ta gerçek
  bir `Reversal` satırı (`source_type=StockMovement`,
  `source_reference_id=<orijinal Consumption hareketinin id'si>`)
  oluştu, bakiye gerçekten 10'a döndü; ikinci bir sipariş `Preparing`e
  zorlanıp aynı şekilde iptal edildiğinde `stockRestored: false` döndü
  ve bakiye 9'da değişmeden kaldı; yeni eşleme `DELETE` uç noktası gerçek
  204/404 döndürdü ve yalnız hedeflenen (ürün, stok kalemi) çiftini sildi.

## Handoff

- None
