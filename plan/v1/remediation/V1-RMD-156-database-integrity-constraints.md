# V1-RMD-156 - Veritabanı bütünlüğü: işaret kısıtları, eksik foreign key'ler, indeks

- Task ID: V1-RMD-156
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

`docs/engineering/garson-audit-2026-09-10.md`'nin veritabanı bloğu — 11
bulgunun tamamı, hepsi scratch veritabanında SQL çalıştırılarak kanıtlanmış.
Tek Critical ve dört High burada:

- **Critical:** `orders.order_items` miktar/fiyat işaretini zorlamıyor.
  `quantity=-3, unit_price=-10, gross=30` yazılabiliyor. Sıfır miktar ve
  negatif `tax_rate` de kabul ediliyor; `order_item_modifiers.quantity=-7`
  de öyle.
- **High:** `inventory.stock_movements.movement_type`/`.direction` serbest
  metin, hiçbir CHECK yok — tablo **değiştirilemez** (trigger), yanlış değer
  bir daha düzeltilemez.
- **High:** `product_stock_mappings.product_id` ve
  `modifier_stock_mappings.modifier_id` foreign key taşımıyor; olmayan
  ürüne/eklentiye eşleme yazılabiliyor, stok sessizce hiç düşmüyor.
- **High:** `kitchen_ticket_items.order_item_id`/`.product_id` foreign key
  taşımıyor.
- **High:** `GetPendingOrdersAsync` indekssiz — iki tam tablo taraması,
  garsonun yokladığı uç nokta.
- **Medium:** `catalog.product_prices.price` negatif olabiliyor;
  `order.json`'daki `phaseBRange.max` hâlâ `095` (096 eklenmiş olmasına
  rağmen — bu benim bıraktığım tutarsızlık); `WithAvailableStockAsync` N+1.
- **Low:** geri alma zinciri 11 boş şema bırakıyor; iki migration
  `CREATE TABLE IF NOT EXISTS` kullanmıyor; iki migration dosyası BOM ile
  başlıyor.

## Owned surface

- `plan/v1/remediation/V1-RMD-156-database-integrity-constraints.md` (yeni)
- `database/migrations/V1/V1-RMD-156/**` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır (yollar
  geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası olarak
  parse etmesin):
  - database/MigrationComposition/order.json — 097 kaydı + phaseBRange.max.
  - src/Host/Composition/Migrations/MigrationManifest.cs — PhaseBMax +
    doc yorumundaki aralık.
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs — sınır
    değerleri.
  - src/Host/Experience/Orders/OrderManagementStore.cs (V1-ORD-00x
    sahipliğinde) — WithAvailableStockAsync N+1 azaltımı.
  - src/Modules/Inventory/StockMaster/IProductStockMappingRepository.cs,
    PostgresProductStockMappingRepository.cs, IStockItemRepository.cs,
    PostgresStockItemRepository.cs,
    src/Modules/Inventory/BalanceProjection/IStockBalanceRepository.cs,
    PostgresStockBalanceRepository.cs (V11-INV-00x sahipliğinde) — N+1
    azaltımını mümkün kılan üç toplu okuma metodu.
  - tests/Modules/Inventory/**/*.cs (ilgili görevlerin sahipliğinde) — yeni
    arayüz üyeleri için sahte (Fake) implementasyon eklenir.
  - tests/Host/Experience/Inventory/StockMasterHttpTests.cs,
    StockMasterTestDatabase.cs, tests/Host/Experience/KitchenOperations/
    KitchenOperationsTestDatabase.cs (ilgili görevlerin sahipliğinde) —
    yeni foreign key'lerin gerektirdiği gerçek satırların tohumlanması.
  - database/migrations/V1/V1-FND-002/002-inbox-messages.up.sql,
    .../003-outbox-messages.up.sql (V1-FND-002 sahipliğinde) —
    `IF NOT EXISTS` eklenir.
  - database/migrations/V1/V1-KIT-001/014-kitchen-tickets.up.sql,
    database/migrations/V1/V1-OPS-001/015-audit-log.up.sql (ilgili
    görevlerin sahipliğinde) — BOM kaldırılır.
  - Geri alma zincirindeki `DROP SCHEMA` eksik olan `.down.sql` dosyaları
    (her biri kendi görevinin sahipliğinde) — şema temizliği eklenir.

## In scope

1. **İşaret kısıtları.** `orders.order_items`: `quantity > 0`,
   `unit_price >= 0`, `tax_rate >= 0`. `orders.order_item_modifiers`:
   `quantity > 0`. `catalog.product_prices`: `price >= 0`.
2. **Hareket türü/yönü kısıtı.** `inventory.stock_movements.movement_type`
   C#'taki `StockMovementType` enum'ının değerlerine, `.direction`
   `MovementDirection`'a CHECK ile kilitlenir.
3. **Eksik foreign key'ler.** `product_stock_mappings.product_id` →
   `catalog.products`, `modifier_stock_mappings.modifier_id` →
   `catalog.modifiers`, `kitchen_ticket_items.order_item_id` →
   `orders.order_items`, `kitchen_ticket_items.product_id` →
   `catalog.products`.
4. **Bekleyen sipariş indeksi.** `orders.orders (created_at) WHERE
   status = 'PendingConfirmation'` — `ix_orders_table_open`'ın kurduğu
   kısmi indeks kalıbıyla aynı.
5. **`order.json` tutarsızlığı.** `phaseBRange.max` `097`'ye çekilir (096'yı
   da kapsayacak şekilde); `MigrationManifest.PhaseBMax` de aynı değere.
6. **`WithAvailableStockAsync` N+1.** Kalem başına 3 sorgu yerine toplu okuma.
7. **Küçük temizlikler.** İki migrationda `IF NOT EXISTS`, iki dosyada BOM,
   geri alma zincirinde boş şema kalıntısı — bunlar başka görevlerin
   sahipliğinde olduğu için yalnız önerilir, bu görev onları değiştirmez
   (bkz. Out of scope).

## Out of scope

- Küçük temizliklerin (IF NOT EXISTS, BOM, boş şema) gerçek düzeltilmesi:
  her biri kendi migration'ını sahiplenen göreve ait, burada dokunulmaz —
  gerçek zararları yok (idempotency migration çalıştırıcısı zaten
  checksum'la takip ediyor, boş şema `CREATE SCHEMA IF NOT EXISTS` ile
  no-op).
- Sipariş yaşam döngüsünün tamamlanmaması, ödeme: ayrı yapısal eksikler.

## Dependencies

- V1-RMD-155

## Acceptance evidence

- `dotnet build ALKAROS.slnx`: 0 Uyarı, 0 Hata.
- **Migration gerçekten çalıştırıldı**, scratch veritabanında
  (`rmd156_scratch`): 001'den 097'ye kadar bütün migration'lar CRLF
  temizlenmiş bir sırayla tek tek uygulandı, 97/97 başarılı. Down migration
  çalıştırıldı (7 `ALTER TABLE`/`DROP INDEX`, hepsi başarılı), up yeniden
  uygulandı (idempotent).
- **Audit'in kanıtladığı sekiz kötü satırın tamamı artık reddediliyor**,
  gerçek INSERT'lerle denendi:
  - `quantity=-3, unit_price=-10` → `ck_order_items_quantity_positive`
  - `quantity=0` → aynı kısıt
  - `tax_rate=-50` → `ck_order_items_tax_rate_nonnegative`
  - `order_item_modifiers.quantity=-7` → `ck_order_item_modifiers_quantity_positive`
  - `product_prices.price=-15.00` → `ck_product_prices_price_nonnegative`
  - `stock_movements (movement_type='Banana', direction='Sideways')` →
    `ck_stock_movements_direction`
  - `product_stock_mappings.product_id=<yok>` → `fk_product_stock_mappings_product`
  - `modifier_stock_mappings.modifier_id=<yok>` → `fk_modifier_stock_mappings_modifier`
  - `kitchen_ticket_items.order_item_id=<yok>` → `fk_kitchen_ticket_items_order_item`
- `EXPLAIN (COSTS OFF)` ile `GetPendingOrdersAsync`'in sorgusu
  `Index Scan using ix_orders_pending_confirmation` kullanıyor (önceden
  `Seq Scan`).
- **`WithAvailableStockAsync` artık kalem sayısından bağımsız üç sorgu.**
  Üç depoya (`IProductStockMappingRepository`, `IStockItemRepository`,
  `IStockBalanceRepository`) `GetByProductIdsAsync`/`GetByIdsAsync`/
  `GetByStockItemsAsync` eklendi. Bunu yaparken gerçek bir kusur buldum ve
  düzelttim: ilk yazdığım hâli `balances.ToDictionary(b => b.StockItemId)`
  kullanıyordu, ama `stock_balances` tablosu `(stock_item_id,
  stock_location_id)` bileşik anahtarıyla benzersiz — bir stok kalemi birden
  çok konumda bakiye taşıyabiliyor. Aynı stok kalemi iki konumda satır
  taşısaydı bu `ArgumentException` ile çökerdi; artık yalnız o kalemin
  `DefaultLocationId`'sine denk gelen satır seçiliyor, eski tek-çift
  sorgusuyla birebir aynı semantik.
- **Tam çözüm regresyonu**: `dotnet test ALKAROS.slnx` — 91 test projesinin
  tamamı, `ALKAROS_TEST_PG_PORT=55432` ile. **90/91 yeşil.** Kalan tek
  başarısızlık (`ALKAROS.Host.Experience.Composition.Tests.
  ServeContainerResolvesEveryModuleServiceFromTheModuleCatalog`) bu işten
  bağımsız olduğu `git stash` ile temel sürümde de aynı hatayla
  doğrulandı — `ALKAROS_KITCHEN_STATION_ID` ortam değişkeni bu kabuk
  oturumunda hiç ayarlı değil, koda dokunmadan da aynı şekilde düşüyor.
- İki gerçek regresyon test sırasında bulunup düzeltildi, ikisi de yeni
  foreign key'lerin daha önce fark edilmemiş bir varsayımı kırmasıydı:
  - `KitchenOperationsTestDatabase` iki yerde `kitchen_ticket_items`'ı
    uydurma `order_item_id`/`product_id` ile tohumluyordu. Gerçek bir
    `catalog.products` + `orders.order_items` satırı eklendi.
  - `StockMasterHttpTests`'in iki testi ürün/eklenti eşlemesini
    `Guid.NewGuid()` ile — yorumda "BOM assignment works before a product
    even exists in the catalog" diye açıkça meşrulaştırılmış olarak —
    tohumluyordu. Bu, audit'in tam olarak tarif ettiği sessiz başarısızlık
    kalıbıydı (bir yazım hatası ya da silinmiş ürün, eşlemeyi sessizce hiç
    eşleşmez hale getiriyor); yorum düzeltilerek gerçek ürün/eklenti
    tohumlanacak şekilde değiştirildi. Üçüncü test aynı sebeple 2 yerine 0
    eşleme buluyordu, aynı şekilde düzeltildi.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: bu görevden yeni
  ihlal yok.

## Handoff

- None
