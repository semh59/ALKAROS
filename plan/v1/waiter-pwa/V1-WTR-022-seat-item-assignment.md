# V1-WTR-022 - Koltuk bazında ürün ataması

- Task ID: V1-WTR-022
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`garson-karsilastirma` karşılaştırma dokümanının "büyük eksikler" listesinden
"koltuk bazlı atama + hesap bölme" maddesinin gerçekten eksik olan yarısı.

**Önce kapsamın gerçek büyüklüğü netleştirildi (2026-09-11):** bu listedeki
diğer iki madde — görsel kat planı editörü ve rezervasyon yönetimi — kod
tabanında zaten TAMAMEN vardı (`FloorPlanWorkspace.tsx`,
`src/Modules/Tables/Reservations/**`, `ReservationStation.tsx`).
Karşılaştırma dokümanı bu ikisinde yanlıştı; ayrı bir görev açılmadı,
bellek düzeltildi. "Koltuk" maddesinin de yarısı zaten vardı: hesap
bölme'nin `AllocationOwnerKind.Seat` ile koltuk bazlı paylaştırması ve
`table_mgmt.table_seats` doğrulaması (`PostgresSplitDesignRepository`)
V1-BIL-004'ten beri gerçek ve çalışır durumda. Eksik olan tek şey: sipariş
verirken bir kalemi belirli bir koltuğa atamak — `OrderItem`'ın hiç
`SeatId`'si yoktu.

**Yeni sütun, FK yok:** `orders.order_items.seat_id` eklendi ama
`table_mgmt.table_seats`'e FK YOK — bilinçli karar (migrasyon dosyasının
kendi yorumunda gerekçelendirildi): bir masanın kat planında hiç koltuk
tanımlı olmayabilir, böyle bir masa yine de sipariş almaya devam etmeli.
Doğrulama uygulama katmanında (`OrderManagementStore.
ResolveValidSeatIdsAsync`) — geçersiz/başka masaya ait bir koltuk id'si
hata değil, yalnızca o satırın atanmamış kalması (aynı tolerans,
tanınmayan bir modifikatörün zaten aldığı davranış).

**`OrderItem`'ın kendi "ServingUserId kusur sınıfı" riski bulundu ve
düzeltildi:** `OrderItem.Mutate` (Activate/Cancel/AdvanceKitchenState/
WithRowVersion/ForOrder'ın hepsinin ortak iç noktası) ve
`ChangeQuantity`'nin kendi elle kurduğu `new OrderItem(...)` — SeatId
eklenmeseydi her void/comp/miktar değişikliği koltuk atamasını sessizce
sıfırlardı. `ItemExceptionHandler.cs`'nin ikram (comp) yeniden
kurulumunda da aynı risk vardı, düzeltildi.

**Şema değişikliğinin genişliği V1-WTR-015 (`orders.orders`) ile aynı
büyüklükte oldu:** `PostgresOrderRepository.BindItem` her INSERT/UPDATE'te
koşulsuz bağlandığı için, aynı 21 test projesi migration 104'ü kendi
fixture listesine eklemek zorunda kaldı.

**İstemci tarafı (WaiterPwa):** bir masa açıldığında o masanın kat planı
koltukları arka planda yükleniyor (`GET .../table-management/floor-plans/
{zoneId}`, 404/koltuksuz masa sessizce "koltuk seçici gösterme" demek).
Ürün ekleme sheet'inde, yalnızca masa koltuklu ise, opsiyonel bir "Koltuk"
seçici. Taslak satırı birleştirme mantığı da düzeltildi: aynı ürün, aynı
seçeneksiz iki satır artık yalnızca AYNI koltuğa (veya ikisi de koltuksuz)
atanmışsa birleşiyor — farklı koltuklara giden aynı ürün ayrı satır kalıyor.

## Owned surface

- `plan/v1/waiter-pwa/V1-WTR-022-seat-item-assignment.md` (yeni)
- `database/migrations/V1/V1-WTR-022/**` (yeni)
- Sınırlı ek:
  - src/Modules/Orders/OrderAggregate/OrderItem.cs,
    PostgresOrderRepository.cs (Orders sahipliğinde) — `SeatId` alanı,
    `WithSeat`, `Mutate`/`ChangeQuantity`'nin sessiz-kaybolma düzeltmesi,
    tam repository sütun eşlemesi.
  - src/Modules/Orders/ItemExceptions/ItemExceptionHandler.cs (Orders
    sahipliğinde) — ikram yeniden kurulumuna `seatId` eklendi.
  - src/Host/Experience/Orders/OrderManagementContracts.cs,
    OrderManagementStore.cs (Host sahipliğinde) —
    `OrderItemDraftDto.SeatId`, `OrderItemDto.SeatId`,
    `ResolveValidSeatIdsAsync`, `MapToDto`'ya eklendi.
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js (V1-WTR-010 sahipliğinde)
    — `state.tableSeats`, `loadTableSeats`, `seatLabel`, ürün sheet'inin
    koltuk seçici optgroup'u, `addToDraft`/`draftToPayload`'ın koltuk
    alanı, taslak birleştirme düzeltmesi.
  - database/MigrationComposition/order.json — 104 kaydı.
  - src/Host/Composition/Migrations/MigrationManifest.cs — PhaseBMax.
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs — manifest
    testinin sınır değerleri.
  - tests/Host/Experience/Orders/TableDraft/{OrderManagementTableDraftHttpTests.cs,
    OrderManagementTableDraftTestDatabase.cs,
    ALKAROS.Host.Experience.Orders.TableDraft.Tests.csproj} (Host test
    sahipliğinde) — yeni testler, `SeedSeatAsync`, migration 039 + 104
    fixture eklemeleri.
  - **21 test projesinin `.csproj` dosyası** — `orders.orders` tablosunu
    kullanan (V1-WTR-015'te de aynı liste) her proje, migration 104'ü
    kendi fixture listesine eklemek zorunda kaldı: TableDraft, Comp,
    Confirmation, Void, VoidSent, NfcOrdering, QrOrdering [Host];
    Billing/Adjustments, Billing/BillFoundation, Billing/SplitDesign;
    Kitchen/PhysicalPrintRecovery, Kitchen/PrintQueue,
    Kitchen/TicketLifecycle; Orders/ItemExceptions, Orders/OrderAggregate,
    Orders/SubmitOrder; QrOrdering/PendingOrders [Modules];
    Tables/CurrentPointers, Tables/Reservations, Tables/TableMerge,
    Tables/TableTransfer.

## Out of scope

- Hesap bölme ekranının koltuk bazlı otomatik dağıtımı (sipariş
  kalemlerinin `SeatId`'sinden `billing.bill_allocations`'ın kendi
  koltuk sahiplerini otomatik türetmek) — altyapı (her ikisi de artık
  gerçek) hazır ama bu görevin kapsamı yalnızca atama; otomatik türetme
  ayrı bir görev.
- Cashier/NFC/QR kanallarından koltuk seçimi — yalnızca WaiterPwa; diğer
  kanallar `SeatId`'yi hiç göndermiyor (varsayılan null), bilinçli.

## Dependencies

- V1-BIL-004
- V1-RMD-026

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` (tüm çözüm) → 0 uyarı, 0 hata.
- `node --check waiter-app.js` → temiz.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres
  ALKAROS_KITCHEN_STATION_ID=test-station dotnet test` — 21 test
  projesinin tamamı + tam migration dizinini uygulayan üç proje
  (MigrationComposition 135/135, Billing Experience 17/17, Tables
  Experience 12/12) tek tek çalıştırıldı, hepsi yeşil. TableDraft'ın
  kendi 2 yeni testi: gerçek bir masa koltuğuyla etiketlenen bir kalem
  koltuğu saklar ve döner; başka bir masaya ait bir koltuk id'si
  reddedilmek yerine sessizce yok sayılır.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.

## Handoff

- None
