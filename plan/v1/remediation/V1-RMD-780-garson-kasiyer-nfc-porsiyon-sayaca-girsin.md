# V1-RMD-780 - Garson, kasiyer ve NFC siparişleri günlük menü sayaçlarına girsin

- Task ID: V1-RMD-780
- Status: InProgress
- Assignee: claude-code-session_01FMN5WVXi1HJCTEk5argkgZ
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-777 günlük menü sayaçlarını QR kabulü ve çevrimiçi siparişler için gerçek veriyle doldurdu. Garson, kasiyer ve NFC siparişleri rezervasyon üretmeden `OrderStockConsumptionService` ile doğrudan stok düştüğü için sayaca girmiyordu. Semih kararı: "Hepsini say".

Çözüm, 777'nin eşlemesini ve porsiyon kuralını (sipariş adedi = porsiyon, ürünün ilk stok eşlemesi tek satır sayılır) yeniden kullanır; ikinci bir kural yoktur:

1. Stok tüketimi tek yerden geçtiği için (`ConsumeItemsAsync`: garson/kasiyer gönderiminde, NFC ve QR kabulünde, çevrimiçi kabulde) bağlama de orada yapılır. Sipariş kalemi bugünkü açık günlük menüde varsa (`IDailyMenuItemLocator`), ilk stok eşlemesi için tüketimle aynı işlemde `Consumed` durumunda bir `portion_reservations` satırı ve `daily_menu_item_id` + `portion_count` anahtarları yazılır; aynı işlemde mevcut `inventory.daily-menu-portion-status-changed.v1` outbox olayı yayımlanır. Rebuild, drift ve rapor aynı tablodan okuduğu için canlı sayaçla uzlaşır.
2. QR ve çevrimiçi kalemlerin tüketimden önce zaten bir rezervasyon satırı vardır (777 sayar). Kalemde herhangi bir durumda rezervasyon satırı varsa yeni satır yazılmaz; bir sipariş iki kanaldan sayılmaz.
3. Yaşam döngüsü: doğrudan kanalda "ayır" adımı yoktur, tüketim satın alma anıdır; satır doğrudan `Consumed` olur. Tüketimden önce iptal edilen kalem tüketilmediği için sayılmaz. Tüketimden sonraki iptal/iade/fire geçişleri QR ve çevrimiçi ile aynı davranır (satır `Consumed` kalır); bu bütün kanallar için ortak açık nokta olarak RESULT.md'de yazılır.

## Owned surface

- `evidence/V1-RMD-780/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Inventory/CrossChannelReservation/ (V12-STK-001) — doğrudan satış satırı, kanal olarak NFC, tüketim köprüsü yöntemi.
  - src/Host/Experience/Orders/OrderStockConsumption/OrderStockConsumptionService.cs (V1-RMD-143) — kalem eşlemesi ve bağlama.
  - src/Host/Experience/NfcOrdering/NfcOrderingEndpoints.cs (V12-NFC-001) — eşleyici kaydı.
  - docs/architecture/module-dependency-rules.md (V0-ARC-001) — Host kenarları.
  - tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs (V1-FND-008) — onaylı Host kenarları.
  - tests/Host/Experience/Orders/Confirmation/QrOrderExpiryHostedServiceTests.cs (V1-RMD-143) — yeni constructor parametresi.
  - tests/Host/Experience/Orders/TableDraft/ (V1-RMD-106) — garson ve kasiyer gönderimi testleri.
  - tests/Host/Experience/Orders/VoidSent/ (V1-IAM-027) — tüketim şeması: günlük menü ve outbox tabloları fixture listesine eklenir.
  - tests/Host/Experience/NfcOrdering/ (V12-NFC-001) — NFC kabulü testi; fixture listesine günlük menü ve outbox tabloları.
  - tests/Modules/Inventory/CrossChannelReservation/ (V12-STK-001) — doğrudan satış, çift sayım ve tekrar testleri.
  - tests/Modules/Menu/CounterProjection/ (V11-MNU-002) — uçtan uca sayaç, rebuild, drift ve rapor testi.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. `ReservationChannel` değerine `Nfc`; guard arayüzüne doğrudan satış yöntemi (tek satır, `NOT EXISTS` ile kalem başına bir kez, kendi idempotency anahtarı öneki).
2. `OrderStockConsumptionService`: günlük menü eşleyicisi, kalem başına ilk stok eşlemesinde çağrı; iptal edilmiş kalem ve düzenleyici (modifier) stoku sayılmaz.
3. NFC kompozisyonunda eşleyici kaydı ve Host kenarları.
4. Testler: garson gönderimi, kasiyer gönderimi, NFC kabulü sıfırdan farklı sayaç; iki stok eşlemeli ürün tek porsiyon; menüde olmayan ürün sayılmaz; QR kabulü tek kez sayılır; iptal edilmiş kalem sayılmaz; olay tekrarı sayacı değiştirmez; rebuild = canlı = rapor.

## Out of scope

- Tüketimden sonra iptal/iade/fire ile porsiyonun geri verilmesi (QR ve çevrimiçide de yok; ayrı karar).
- Gece yarısı iş günü sınırı, rebuild kilidi, üretim partisi birimi, geçmiş siparişlerin doldurulması, şema/migration.

## Dependencies

- V1-RMD-777

## Acceptance evidence

1. Kırmızı önce: garson, kasiyer ve NFC siparişi sayaçları 0 bırakıyor; düzeltmeden sonra yeşil. Rebuild, drift ve rapor canlı sayaçla eşit.
2. Çift sayım yok (QR kabulü, iki eşlemeli ürün, tekrar); menüde olmayan ve iptal edilen kalem sayılmaz.
3. Menu, Inventory, Orders, Reporting, Host, ApiConventions ve ModuleBoundaries testleri gerçek exit code ile `evidence/V1-RMD-780/` altında.
4. Mutasyon: çağrıyı kaldırmak, `NOT EXISTS`'i kaldırmak, eşleme başına yazmak testleri kırar; kaynak `cmp` ile geri alınır.
5. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
6. qa-backend ve qa-test-kalitesi incelemeleri sırayla yapılır, bulgular giderilir.

## Handoff

- None
