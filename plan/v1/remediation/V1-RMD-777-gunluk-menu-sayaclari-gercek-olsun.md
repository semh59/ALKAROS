# V1-RMD-777 - Günlük menü sayaçları gerçek siparişlerle ve üretimle dolsun

- Task ID: V1-RMD-777
- Status: InProgress
- Assignee: claude-code-session_01FMN5WVXi1HJCTEk5argkgZ
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-739 bulgusu (V11-MNU-002, Yüksek), Semih kararıyla: sayaçlar gerçek olacak ve sipariş adedi porsiyon sayılacak (stok miktarı reçeteye bölünmez). Bugün `IDailyMenuCounterProjector` metotlarını üretimde çağıran yok; sayaçlar yalnız menü oluşturulurken sıfırlanıyor. Rebuild, drift ve `PostgresMenuInventoryReportingService` `portion_reservations.metadata->>'daily_menu_item_id'` anahtarından toplar ama `src/` içinde bu anahtarı yazan kod yok; ayrıca stok birimindeki `quantity` toplanıyor (0,15 kg gibi), porsiyon değil. Sonuç: reserved/consumed/waste hep 0 ve "uzlaşma" iki taraf 0 olduğu için yanıltıcı biçimde doğru.

Çözüm:

1. Eşleme: yeni `IDailyMenuItemLocator`, bugünkü iş gününün `Open` günlük menüsünde, ürünü içeren aktif kalemi bulur. `business_date` ve `(daily_menu_id, product_id)` benzersiz olduğundan eşleme belirsiz olamaz; ürün menüde yoksa rezervasyon sayaca girmez.
2. Yazım: `ICrossChannelPortionArbiter` hattı (tek üretim yolu: QR kabulü ve çevrimiçi sipariş) kalemin `DailyMenuItemId` değerini alır. Sipariş kalemi başına yalnız ilk stok rezervasyonu `daily_menu_item_id` ve `portion_count` (= sipariş kalemi adedi) anahtarlarını taşır; birden çok stok eşlemesi olan ürün çift sayılmaz. Rezervasyon miktarı stok biriminde kalır.
3. Olay yolu: Inventory, anahtarı olan rezervasyonun oluşumunu ve her terminal geçişini (tüketim, iptal/bırakma, fire) aynı işlemde outbox'a yazar; Production, günlük menü kalemine bağlı partinin çıktısını aynı işlemde yazar; Menu tüketicisi bunları mevcut `ApplyReservationReserved/Terminal` ve `ApplyProductionOutput` metotlarına uygular. Terminal olay önce gelirse tüketici önce Reserved'ı (idempotent) uygular.
4. Toplama: rebuild, drift ve rapor `portion_count` toplar, stok `quantity` değil. `available_portions` CHECK (>= 0) olduğundan üretimi olmayan kalem ilk siparişte hata verip ölü mektuba düşmesin diye sayaçta 0'a kırpılır.
5. Rebuild ve drift için yönetim uç noktaları açılır; böylece bozulan sayaç onarılabilir ve bu metotlar ölü kod kalmaz.
6. Eski veri: geçmiş rezervasyonlarda anahtar yok, geriye dönük doldurma yapılmaz; yalnız bu görevden sonra oluşan rezervasyonlar sayılır, rebuild yalnız anahtarı olanları sayar.

## Owned surface

- `evidence/V1-RMD-777/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/BuildingBlocks/IntegrationContracts/DailyMenuCounterIntegrationEvents.cs (V1-RMD-663) — iki olay sözleşmesi.
  - src/Modules/Inventory/ALKAROS.Inventory.csproj (V11-INV-004) — Messaging ve IntegrationContracts başvurusu.
  - src/Modules/Inventory/CrossChannelReservation/ (V12-STK-001) — satır modeli, anahtar yazımı, tüketim köprüsü, olay yazıcı.
  - src/Modules/Inventory/PortionReservations/Lifecycle/PostgresPortionReservationRepository.cs (V11-RSV-001) — terminal geçişte olay.
  - src/Modules/Production/ALKAROS.Production.csproj (V11-PRD-001) — Messaging ve IntegrationContracts başvurusu.
  - src/Modules/Production/StockEffects/ProductionStockEffectService.cs (V11-PRD-002) — çıktı olayı.
  - src/Modules/Menu/ALKAROS.Menu.csproj (V11-MNU-003) — IntegrationContracts başvurusu.
  - src/Modules/Menu/MenuModule.cs (V11-RMD-002) — tüketici ve eşleyici kaydı.
  - src/Modules/Menu/DailyMenuLifecycle/IDailyMenuItemLocator.cs (V11-MNU-001) — yeni eşleyici sözleşmesi.
  - src/Modules/Menu/DailyMenuLifecycle/PostgresDailyMenuItemLocator.cs (V11-MNU-001) — yeni eşleyici.
  - src/Modules/Menu/CounterProjection/ (V11-MNU-002) — tüketici, porsiyon toplama, kırpma.
  - src/Modules/Reporting/MenuInventory/PostgresMenuInventoryReportingService.cs (V11-RPT-001) — porsiyon toplama.
  - src/Host/Experience/Orders/PendingOrderConfirmation/PendingOrderConfirmationStore.cs (V1-RMD-137) — QR kabulünde kalem eşlemesi.
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs (V1-RMD-137) — eşleyici kaydı.
  - src/Host/Experience/OnlineOrdering/YemeksepetiOrderIntakeService.cs (V12-ONL-002) — çevrimiçi sipariş kalem eşlemesi.
  - src/Host/Experience/OnlineOrdering/YemeksepetiWebhookEndpoints.cs (V12-ONL-001) — eşleyici kaydı.
  - src/Host/Experience/Menu/MenuManagementEndpoints.cs (V1-RMD-131) — rebuild ve drift uç noktaları.
  - src/Host/Experience/Menu/MenuManagementContracts.cs (V1-RMD-131) — rebuild ve drift sözleşmeleri.
  - docs/architecture/module-dependency-rules.md (V0-ARC-001) — yeni olay kenarları ve Host kenarları.
  - tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs (V1-FND-008) — onaylı Host kenarları.
  - tests/Modules/Menu/CounterProjection/ (V11-MNU-002) — tüketici, uçtan uca ve toplama testleri.
  - tests/Modules/Menu/DailyMenuLifecycle/ (V11-MNU-001) — eşleyici testleri.
  - tests/Modules/Inventory/CrossChannelReservation/ (V12-STK-001) — anahtar, olay ve çift sayım testleri.
  - tests/Modules/Production/StockEffects/ (V11-PRD-002) — çıktı olayı testi.
  - tests/Modules/Reporting/MenuInventory/ (V11-RPT-001) — porsiyon toplama testi.
  - tests/Host/Experience/Menu/ (V1-RMD-131) — rebuild ve drift uç nokta testleri.
  - tests/Host/Experience/Orders/Confirmation/ (V1-RMD-137) — QR kabulü anahtar testi.
  - tests/Host/Experience/OnlineOrdering/ (V12-ONL-001) — çevrimiçi sipariş anahtar testi.
  - src/Host/packages.lock.json (V1-FND-001) — kilit dosyası.
  - src/Modules/*/packages.lock.json (V1-FND-001) — kilit dosyaları.
  - tests/Architecture/*/packages.lock.json (V1-FND-001) — kilit dosyaları.
  - tests/Host/**/packages.lock.json (V1-FND-001) — kilit dosyaları.
  - tests/Modules/*/*/packages.lock.json (V1-FND-001) — kilit dosyaları.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. `IDailyMenuItemLocator`: bugünkü iş günü (`IBusinessDateProvider`) için `Open` menüde aktif kalem eşlemesi.
2. Arbiter satırına `DailyMenuItemId`; ilk stok rezervasyonuna `daily_menu_item_id` ve `portion_count` yazımı.
3. Rezervasyon oluşumu ve tüm terminal geçişleri (tüketim köprüsü, repository geçişi) için outbox olayı; üretim çıktısı için outbox olayı; Menu tüketicisi.
4. Rebuild, drift ve rapor için `portion_count` toplaması; `available_portions` kırpması.
5. Rebuild ve drift uç noktaları (menü yöneticisi yetkisi).
6. Host kenarları ve olay kenarları dokümanı.

## Out of scope

- Garson, kasiyer ve NFC siparişleri rezervasyon üretmediği için bu kanallar sayılmaz (ayrı karar, RESULT.md'de seçenekler).
- Şema/migration, geçmiş rezervasyonların geriye dönük doldurulması, menü ekranı, Apply metotlarının eşzamanlılık yeniden tasarımı.

## Dependencies

- V11-MNU-002
- V12-STK-001
- V11-PRD-002

## Acceptance evidence

1. Gerçek PostgreSQL ile uçtan uca test: açık günlük menü, gerçek arbiter rezervasyonu, outbox'tan tüketiciye teslim; sayaçlar sıfırdan farklı, rebuild ve rapor değerleriyle eşit, drift yok. Tüketim, iptal/bırakma ve fire geçişleri ayrı doğrulanır. Düzeltmeden önce kırmızı, sonra yeşil.
2. İki stok eşlemeli ürün tek porsiyon sayılır; üretimi olmayan kalem siparişte hata vermez; olaylar ters sırada ve tekrar gelse sayaç aynı kalır.
3. Menu, Inventory, Production, Orders, Reporting, Host ve ApiConventions testleri gerçek exit code ile `evidence/V1-RMD-777/` altında.
4. Mutasyon: anahtar yazımı kaldırılınca ve tüketici kaydı kaldırılınca testler kırılır; kaynak `cmp` ile geri alınır.
5. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.

## Handoff

- None
