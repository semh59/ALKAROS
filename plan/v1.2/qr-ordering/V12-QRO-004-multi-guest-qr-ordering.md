# V12-QRO-004 - Aynı masada birden fazla telefondan QR siparişi

- Task ID: V12-QRO-004
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in sorusu ve talimatıyla ("Peki iki ayrı kişiyiz tek telefon ile
sipariş vermek doğru değil. Çözüm bulalım", 2026-09-12): aynı masadaki
birden fazla kişinin kendi telefonundan BAĞIMSIZ QR siparişi verebilmesi.

**Bulgu:** `V12-QRO-002`'nin `QrTableReservationPolicy.ReserveForSubmissionAsync`'i,
bir QR gönderimini yalnız masa `Available` (boş) ise kabul ediyordu — ilk
gönderim masayı `Available → Reserved` yapıyor, ve masa `Reserved` (garson
henüz onaylamadı) ya da `Occupied` (garson zaten onayladı, servis
sürüyor) olduğu her durumda **farklı bir telefondan gelen HER YENİ QR
gönderimi koşulsuz reddediliyordu** (`QrTableNotAvailableException`).
Yani bir masadaki 2. kişi kendi telefonundan asla sipariş veremiyordu —
yalnız 1. kişinin siparişi geçiyordu, geri kalan herkes garsonu çağırmak
zorunda kalıyordu.

Bu, `docs/design/modules/qr-nfc-ordering.md` §6'nın kendi yazılı kararıyla
("aynı masaya birden fazla kişinin bağımsız sipariş vermesini kasıtlı
olarak destekliyoruz") ve `NfcOrderingStore`'un (`V12-NFC-001`) zaten
uyguladığı gerçek davranışla (masa `Occupied` iken de yeni, bağımsız bir
NFC siparişi kabul ediliyor) DOĞRUDAN çelişiyordu — QR kanalı, aynı
tasarım kararının kendi kodunda hiç uygulanmamış hâliydi.

**Neden `Occupied`'ı açmak güvenli, `Reserved`'ı açmak değil:**
`QrOrderSubmittedConsumer` her gönderimi kendi `SubmissionId`'siyle ayrı
bir `orders.orders` satırına dönüştürüyor (zaten NFC ile birebir aynı
desen) — birleştirme/çakışma riski yok. `ITableRepository.
LinkCurrentOrderAsync`'in kendi `WHERE current_order_id IS NULL OR
current_order_id = @order_id` koruması, ikinci bir siparişin masanın
`current_order_id` işaretçisini asla çalmayacağını zaten garanti ediyor.
`Reserved` durumu ise farklı: orada HENÜZ onaylanmış hiçbir sipariş yok —
yalnız ilk gönderimin garson onayını beklediği, belirsiz bir ara durum;
bu durumda ikinci bir gönderime izin vermek "hangi sipariş gerçek, hangisi
değil" belirsizliğini masaya taşırdı. `Cleaning`/`OutOfService` zaten hiç
tartışmasız reddedilmesi gereken durumlar.

**Çözüm:** `QrTableReservationPolicy.ReserveForSubmissionAsync`: masa `Occupied` ise
artık HİÇBİR durum değişikliği yapmadan (masa zaten olması gereken yerde)
kabul ediyor — `NfcOrderingStore.PlaceOrderAsync`'in `selfCheckIn`
mantığıyla birebir aynı kural. `Available` hâlâ `Reserved`'a çevriliyor
(ilk gönderim, değişmedi). `Reserved`/`Cleaning`/`OutOfService`/devre
dışı/var olmayan masa hâlâ koşulsuz reddediliyor (değişmedi).

## Owned surface

- Sınırlı ek:
  - src/Modules/QrOrdering/TablePolicy/QrTableReservationPolicy.cs
    (V12-QRO-002 sahipliğinde) — `ReserveForSubmissionAsync`'e Occupied
    erken-çıkışı eklendi, doc comment güncellendi.
  - tests/Modules/QrOrdering/TablePolicy/QrTableReservationPolicyTests.cs,
    tests/Modules/QrOrdering/PendingOrders/QrPendingOrderStoreTests.cs,
    tests/Host/Experience/QrOrdering/QrOrderingHttpTests.cs (ilgili test
    sahipliğinde) — Occupied'ı "reddedilir" varsayan testler "başarılı
    olur, masa durumu değişmez" olarak güncellendi; Reserved'ın hâlâ
    reddedildiğini doğrulayan ayrı testler eklendi (unit seviyede zaten
    vardı, HTTP seviyesinde yeni eklendi).

## Out of scope

- `Reserved` durumundaki bir masaya ikinci bir QR gönderimine izin vermek
  — bilinçli olarak yapılmadı, Goal'ün "neden Occupied güvenli, Reserved
  değil" bölümünde gerekçelendirildi.
- `TableTokenService`'in kendi rotasyon/süre yönetimi — bir masanın QR
  token'ı bugün yalnız personel elle `RotateAsync` çağırırsa veya kendi
  4 saatlik ömrü dolarsa geçersiz oluyor; masa `Occupied → Available`
  döndüğünde (hesap kapandığında) token'ın OTOMATİK rotasyonu bu görevin
  kapsamında ele alınmadı. Bugünkü davranışla bu görev arasında yeni bir
  risk YOK (aynı token'ın süresi boyunca kimin sipariş verebileceği kuralı
  zaten hep buydu) — yalnız gerçek bir iyileştirme fırsatı, ayrı bir karar
  gerektirir (bkz. Handoff).
- Aynı anda İKİ FARKLI telefonun tam olarak aynı anda ilk gönderimi
  yapmaya çalışması (masa hâlâ Available iken) — bu zaten `V12-QRO-002`'nin
  `FOR UPDATE` satır kilidiyle çözülmüş bir yarış, bu görev dokunmadı.
- Masa `Occupied → Available` döndüğünde (hesap kapanıp masa
  temizlendiğinde) o masanın QR token'ının OTOMATİK rotasyonu — bugün
  yalnız elle (`TableTokenService.RotateAsync`) yapılabiliyor. Bu, bir
  sonraki gerçek misafir grubunun oturduğu masada, ÖNCEKİ grubun hâlâ
  süresi dolmamış (4 saate kadar) eski QR linkinin/oturumunun teorik
  olarak hâlâ çalışabiliyor olması riskini kapatır — bu görevin ortaya
  çıkardığı yeni bir risk değil (davranış hep böyleydi), ama masa devri
  sık olan bir işletmede gerçek değeri olan, ayrı bir iyileştirme, ayrı
  bir görev olarak ele alınmalı.

## Dependencies

- V12-QRO-002

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres dotnet test`:
  - `tests/Modules/QrOrdering/TablePolicy` → 7/7 (Occupied artık kabul
    testi; Reserved/Cleaning/OutOfService hâlâ ret testi).
  - `tests/Modules/QrOrdering/PendingOrders` → 13/13 (gerçek Postgres —
    Occupied masaya ikinci gönderim artık başarılı, masa durumu
    değişmiyor).
  - `tests/Host/Experience/QrOrdering` → 23/23 (gerçek HTTP uçtan uca —
    ikinci misafirin gönderimi 202 Accepted dönüyor, outbox'a event
    kuyruklanıyor, masa `Occupied` kalıyor; Reserved masa hâlâ 409
    "garsonu çağırın" ile reddediliyor).
  - `tests/Host/Experience/NfcOrdering` → 17/17 (regresyon — dokunulmadı,
    tam paket koşumunda tek seferlik bir eşzamanlılık flake'i gözlendi,
    izole ve ikinci tam koşumda 17/17 temiz; bu görevle ilgisiz).
  - `tests/Modules/Tables/TableLifecycle` → 59/59, `tests/Modules/Tables/
    CurrentPointers` → 19/19 (regresyon).
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.

## Handoff

- None
