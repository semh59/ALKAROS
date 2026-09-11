# V1-WTR-015 - Kuver (kişi) sayısı takibi

- Task ID: V1-WTR-015
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`garson-karsilastirma` karşılaştırma dokümanının rakip karşılaştırma
matrisinde açık bırakılmış bir "eksik" — "Kişi/kuver sayısı takibi": her
rakip POS (Toast, Square, Lightspeed, Adisyo) kaç misafirin masada
olduğunu takip ediyordu, ALKAROS hiçbirini. Semih'in talimatı ("eksiklere
başla") üzerine eksikler listesinden ele alınan ilk madde — en küçük ve
en temelde faydalı olanı (ileride kurs/koltuk gibi daha büyük eksiklerin
de doğal bir ön koşulu).

Kapsam bilinçli olarak dar tutuldu: kuver sayısı yalnız masanın İLK turu
gönderilmeden önce ayarlanabilir (garson masaya oturttuğunda zaten "kaç
kişi?" diye sorar — doğal an tam da budur). Sonradan düzeltmek (misafir
sayısı değişirse) bu görevin kapsamı dışında, ayrı bir görev olabilir.

`Order.PartySize` (`int?`, 1-50 aralığı) eklendi — `ServingUserId`'nin
V1-RMD-111'de aldığı yolun birebir aynısı: `RebuildWith`'in yeni bir
parametresi, `TransitionTo`'nun kendi elle yeniden kurduğu `new Order(...)`
çağrısına eklendi.

**Kendi işimi kontrol ederken bulduğum, ServingUserId'nin daha önce
(V1-RMD-154) yaşadığı AYNI kusur sınıfı:** `ItemExceptionHandler.cs`'nin
void/comp akışları kendi `new Order(...)`'larını order'ın alanlarından
elle yeniden kuruyor — `PartySize`'ı eklemeseydim her void/ikram, kuver
sayısını sessizce sıfırlardı. `PostgresOrderRepository.GetByIdAsync`'in
okuma yolu da aynı riski taşıyordu. İkisi de düzeltildi; `TransitionTo`
zaten aynı riski taşıyordu, o da düzeltildi.

**Şema değişikliğinin gerçek genişliği:** `orders.orders`'a yeni bir sütun
eklemek, `PostgresOrderRepository`'nin `BindOrder`'ı (her INSERT/UPDATE'te
koşulsuz bağlanıyor) yüzünden, bu tabloyu kullanan HER test projesini
etkiledi — 21 test projesi, migration 102'yi kendi fixture listesine
eklemeden `party_size sütunu yok` hatasıyla kırılırdı. Hepsi tek tek
düzeltildi ve tam bir regresyon turuyla (570 test) doğrulandı.

**Ayrıca düzeltildi:** V1-WTR-014'ün kendi task dosyasındaki bir sahiplik
hatası — `Cashier.help-alerts.test.tsx` (yeni bir dosya) yanlışlıkla
"Sınırlı ek" (başka bir görevin sahipliğinde) altına, "(yeni)" notuyla
birlikte yazılmıştı — çelişkili bir beyan (yeni bir dosya aynı anda başka
birinin sahipliğinde olamaz). `plan_audit_tool.py` bunu UNOWNED_PRODUCTION_
FILE olarak yakaladı; dosya artık V1-WTR-014'ün kendi üst düzey Owned
surface'inde.

## Owned surface

- `plan/v1/waiter-pwa/V1-WTR-015-party-size.md` (yeni)
- `database/migrations/V1/V1-WTR-015/**` (yeni)
- Sınırlı ek:
  - src/Modules/Orders/OrderAggregate/Order.cs,
    PostgresOrderRepository.cs (Orders sahipliğinde) — `PartySize`
    alanı, `RebuildWith`/`TransitionTo`/`WithPartySize`, tam repository
    sütun eşlemesi.
  - src/Modules/Orders/ItemExceptions/ItemExceptionHandler.cs (Orders
    sahipliğinde) — void/comp'un kendi `new Order(...)` yeniden
    kurulumlarına `order.PartySize` eklendi (ServingUserId'nin V1-RMD-154'te
    yaşadığı aynı sessiz-sıfırlama riski).
  - src/Host/Experience/Orders/OrderManagementContracts.cs,
    OrderManagementStore.cs (Host sahipliğinde) —
    `CreateTableDraftRequest.PartySize`, `OrderDto.PartySize`, ilk tur
    oluşturmada `partySize: request.PartySize`, `MapToDto`'ya eklendi.
  - src/Clients/WaiterPwa/wwwroot/index.html, waiter-app.js, waiter-app.css
    (V1-WTR-010 sahipliğinde) — bilet başlığında "kişi sayısı" düğmesi,
    stepper sheet'i, adisyon alt başlığında gösterim.
  - database/MigrationComposition/order.json — 102 kaydı.
  - src/Host/Composition/Migrations/MigrationManifest.cs — PhaseBMax.
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs — manifest
    testinin sınır değerleri.
  - tests/Host/Experience/Orders/TableDraft/OrderManagementTableDraftHttpTests.cs
    (Host test sahipliğinde) — yeni testler.
  - **21 test projesinin `.csproj` dosyası** — `orders.orders` tablosunu
    kullanan her proje, migration 102'yi kendi fixture listesine eklemek
    zorunda kaldı (tam liste: TableDraft, Comp, Confirmation, Void,
    VoidSent, NfcOrdering, QrOrdering [Host]; Billing/Adjustments,
    Billing/BillFoundation, Billing/SplitDesign; Kitchen/
    PhysicalPrintRecovery, Kitchen/PrintQueue, Kitchen/TicketLifecycle;
    Orders/ItemExceptions, Orders/OrderAggregate, Orders/SubmitOrder;
    QrOrdering/PendingOrders [Modules]; Tables/CurrentPointers,
    Tables/Reservations, Tables/TableMerge, Tables/TableTransfer).
  - `plan/v1/waiter-pwa/V1-WTR-014-help-request.md` — sahiplik beyanı
    düzeltmesi (yukarıda açıklandı).

## Out of scope

Kuver sayısını masanın ilk turundan SONRA düzeltmek — ayrı görev. Masa
ızgarasında rozet olarak göstermek — Tables modülü Orders'ın party_size'ını
bilmiyor, cross-module bir okuma/denormalize gerektirir, bu görevin
kapsamı dışında (V0-ARC-001 sınırlarını korumak için bilinçli).

## Dependencies

- V1-RMD-111
- V1-RMD-123

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` (tüm çözüm) → 0 uyarı, 0 hata.
- `node --check waiter-app.js` → temiz; `waiter-app.css` parantez dengesi
  247/247.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres
  ALKAROS_KITCHEN_STATION_ID=test-station dotnet test` — tam regresyon
  turu, 570 test, hepsi yeşil:
  - `tests/Host/Experience/Orders/TableDraft/*.csproj` → 59/59 (3 yeni
    test: kuver sayısı order'a kaydedilir; ikinci turda korunur —
    ServingUserId'nin V1-RMD-154'te yaşadığı sessiz-kaybolma sınıfının
    aynısı olmadığının kanıtı; aralık dışı değer reddedilir).
  - Comp 13/13, Void 5/5, VoidSent 14/14, Confirmation 19/19.
  - Orders/OrderAggregate 120/120, Orders/ItemExceptions 20/20,
    Orders/SubmitOrder 16/16.
  - Tables/CurrentPointers 19/19, Tables/Reservations 27/27,
    Tables/TableMerge 29/29, Tables/TableTransfer 33/33.
  - Kitchen/PhysicalPrintRecovery 17/17, Kitchen/PrintQueue 27/27,
    Kitchen/TicketLifecycle 20/20.
  - Billing/Adjustments 16/16, Billing/BillFoundation 41/41,
    Billing/SplitDesign 28/28.
  - QrOrdering/PendingOrders 13/13, Host/QrOrdering 17/17,
    Host/NfcOrdering 17/17.
  - `Architecture.Tests` 9/9, `ManifestTests` 16/16.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı
  (V1-WTR-014'ün UNOWNED_PRODUCTION_FILE hatası da bu turda düzeltildi).
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.

## Handoff

- None
