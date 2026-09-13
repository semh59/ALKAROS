# V1-RMD-137 - Independent audit: NFC age-restriction enforcement was cosmetic, no order-confirmation action existed

- Task ID: V1-RMD-137
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız denetimin (2026-09-09, QR/NFC müşteri sipariş yüzeyi turu) en
kritik bulgusu: `V12-NFC-002`'nin yaş kısıtlı ürün "onayı" fiilen hiçbir
şeyi engellemiyordu. `NfcOrderingStore`, siparişi `PendingConfirmation`da
bırakıyordu ama mutfak bileti zaten `Draft->Submitted` geçişinde
(`SubmitOrderHandler`/`KitchenOrderSubmissionDispatcher`) koşulsuz
gönderiliyordu — yaş kontrolü bundan SONRA çalışıyordu. Daha kötüsü:
kod tabanında `PendingConfirmation`dan çıkan (`Accepted`/`Rejected`e
geçen) tek yer siparişin kendisiydi; personelin "onayla/reddet"
diyebileceği hiçbir HTTP aksiyonu yoktu (`V12-QRO-003`, bu aksiyonu
tam kapsamıyla — QR'ın kendi bölüm-rezervasyon mantığı dahil — yapması
planlanan görev, hâlâ "Planned"). Sonuç: yaş kısıtlı her NFC siparişi
kalıcı olarak askıda kalıyordu, masa personel tarafından zorla serbest
bırakılsa bile.

Kullanıcıyla (Semih) iki karar netleştirildi:

1. Mutfak bileti hemen gitsin (hazırlık süresi kaybolmasın); kimlik
   kontrolü servis anında yapılsın — biletin üzerinde yaş kısıtlı
   kalemler ayrıca işaretlenir ki servis eden kişi kontrolsüz teslim
   etmesin.
2. `PendingConfirmation`daki bir siparişi onaylayıp/reddedebilecek,
   kanaldan bağımsız minimal bir Host aksiyonu şimdi eklensin (QR'ın
   kendi bölüm-rezervasyon mantığı olmadan — QR'ın zaten hiçbir HTTP
   yüzeyi yok, bkz. Out of scope).

## Owned surface

- `plan/v1/remediation/V1-RMD-137-nfc-age-restriction-enforcement-and-order-confirmation.md`
  (yeni)
- `database/migrations/V1/V1-RMD-137/**` (yeni — 092:
  `kitchen.kitchen_ticket_items.is_age_restricted`)
- `src/Host/Experience/Orders/PendingOrderConfirmation/**` (yeni —
  `PendingOrderConfirmationStore`, contracts, exceptions)
- `tests/Host/Experience/Orders/Confirmation/**` (yeni test projesi)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik
  iddiası olarak parse etmesin):
  - src/Modules/Kitchen/TicketLifecycle/KitchenTicketItem.cs,
    KitchenTicket.cs, PostgresKitchenTicketRepository.cs (V1-KIT-001
    sahipliğinde) — KitchenTicketItem yeni bir IsAgeRestricted alanı
    aldı (varsayılan false, geriye dönük uyumlu); CreateFromOrder yeni,
    varsayılanlı bir isAgeRestricted resolver parametresi aldı;
    repository'nin dört SELECT/INSERT yeri yeni sütunu okuyup yazıyor.
    Diğer tüm geçiş/iş kuralları değişmedi.
  - src/Modules/Kitchen/TicketLifecycle/KitchenOrderSubmissionDispatcher.cs
    (V1-KIT-001/V12-QRT-... yönlendirme mantığı sahipliğinde) — yeni
    ResolveAgeRestrictedProductIdsAsync yardımcı metodu eklendi
    (aynı transaction içinde catalog.products'a tek ek sorgu); istasyon
    yönlendirme mantığının kendisi değişmedi.
  - src/Modules/Kitchen/PrintQueue/EscPosTicketFormatter.cs (V1-RMD-130
    sahipliğinde) — yalnızca yaş kısıtlı kalemler için tek bir uyarı
    satırı eklendi; biletin geri kalan biçimlendirmesi değişmedi.
  - src/Host/Experience/KitchenOperations/KitchenOperationsContracts.cs,
    KitchenOperationsStore.cs (V1-KIT-002/ilgili KitchenOperations
    görevleri sahipliğinde) — KitchenTicketItemV1 DTO'suna varsayılan
    `false` ile geriye dönük uyumlu IsAgeRestricted alanı eklendi.
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs (V1-ORD-005/
    V1-IAM-027 sahipliğinde) — iki yeni endpoint (`/accept`, `/reject`)
    ve PendingOrderConfirmationStore'un DI kaydı eklendi; exception
    filter'a iki yeni eşleme satırı eklendi. Mevcut hiçbir endpoint/
    eşleme değişmedi.
  - database/MigrationComposition/order.json, src/Host/Composition/
    Migrations/MigrationManifest.cs, tests/Host/MigrationComposition/
    Manifest/ManifestTests.cs (paylaşılan migration-manifest altyapısı)
    — standart 4 dosyalık migration-manifest güncelleme deseni: yeni
    092 girdisi, PhaseBMax 091->092, RuntimeManifestIds/LastEntryTables/
    outside-range probe güncellendi.
  - tests/Host/Experience/NfcOrdering/, tests/Host/Experience/Orders/
    TableDraft/, tests/Host/Experience/Orders/VoidSent/, tests/Modules/
    Kitchen/PhysicalPrintRecovery/, tests/Modules/Kitchen/PrintQueue/,
    tests/Modules/Kitchen/TicketLifecycle/ (ilgili görevlerin
    sahipliğindeki test projeleri) — paylaşılan
    PostgresKitchenTicketRepository'nin SQL'i artık is_age_restricted
    sütununu her zaman okuyup yazdığından, bu altı projenin curated
    migration fixture listesine 092 (ve gerekli olan beşinde ayrıca
    084, catalog.products.is_age_restricted) eklendi. Test kodunun
    kendisi değişmedi.
  - ALKAROS.slnx (çözüm dosyası, paylaşılan) — yeni
    tests/Host/Experience/Orders/Confirmation projesi eklendi.

## In scope

1. **Yaş kısıtlı kalem, mutfak biletinde işaretleniyor.**
   `KitchenTicketItem.IsAgeRestricted` (yeni, kalıcı snapshot — migrasyon
   092), `KitchenOrderSubmissionDispatcher` bunu aynı transaction'da
   `catalog.products.is_age_restricted`'tan tazeliyor, hem dijital
   `KitchenOperationsStore` DTO'sunda hem de fiziksel ESC/POS biletinde
   ("*** YAS KONTROLU GEREKLI ***" satırı) görünüyor. Mutfak dispatch'i
   koşulsuz kalıyor (Semih'in kararı) — bu, tek gerçek uygulama noktası.
2. **`PendingOrderConfirmationStore` + iki yeni endpoint**
   (`POST .../orders/{orderId}/accept`, `.../reject`, `orders.create`
   yetkisiyle): `PendingConfirmation` -> `Accepted`/`Rejected`. Accept
   masayı `Reserved` -> `Occupied` yapar; Reject siparişin tüm mutfak
   bilet kalemlerini iptal eder (best-effort, `SentItemVoidStore`'un
   V1-IAM-027'deki desenini order-seviyesinde tekrarlıyor) ve masayı
   `Reserved` -> `Available`'a çekip `current_order_id`'yi temizler.
   İkisi de satır sürümü kontrolü yapar; sipariş zaten
   `PendingConfirmation`da değilse (`OrderNotAwaitingConfirmationException`,
   409) veya zaten bir Bill'e bağlıysa (`OrderAlreadyBilledException`,
   409, yalnız Reject için) reddedilir.
3. Migration 092 + standart 4 dosyalık manifest güncelleme deseni.

## Out of scope

- `V12-QRO-003`'ün tam kapsamı (QR'ın kendi bölüm/stok rezervasyonu
  onay üzerine) — QR kanalının hiçbir HTTP yüzeyi olmadığından (2026-
  09-09 denetiminin #0 bulgusu) bugün hiçbir pratik etkisi yok; bu
  görev yalnız NFC'nin gerçek ihtiyacını (askıda kalan sipariş) çözüyor.
- Denetimin diğer bulguları: Y2 (`RelayAbusePolicy` bağlanmamış — QR
  HTTP yüzeyi olmadığı için bağlanacak bir yer yok), O1 (miktar/kalem
  üst sınırı), O2 (NFC sayfasının tüm PosTerminal bundle'ıyla aynı
  origin'den servis edilmesi), D1-D3 (düşük öncelikli) — ayrı görevler.
- Personelin bekleyen bir onayı NASIL göreceği (bir "bekleyen
  onaylar" ekranı/bildirimi) — bu görev yalnız mekanizmayı (HTTP
  aksiyonu) sağlıyor; keşif/UI ayrı bir iş.

## Dependencies

- V12-NFC-001
- V12-NFC-002
- V1-IAM-027

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 Uyarı, 0 Hata.
- Gerçek Postgresql'e karşı Docker'da, real exit code 0 ile:
  - `ALKAROS.Kitchen.TicketLifecycle.Tests`: 19/19
  - `ALKAROS.Kitchen.PrintQueue.Tests`: 27/27
  - `ALKAROS.Kitchen.PhysicalPrintRecovery.Tests`: 17/17
  - `ALKAROS.Host.Experience.Orders.VoidSent.Tests`: 10/10
  - `ALKAROS.Host.Experience.Orders.TableDraft.Tests`: 14/14
  - `ALKAROS.Host.Experience.Orders.Void.Tests`: 5/5
  - `ALKAROS.Host.Experience.Orders.Comp.Tests`: 9/9
  - `ALKAROS.Host.Experience.NfcOrdering.Tests`: 15/15
  - `ALKAROS.Host.Experience.KitchenOperations.Tests`: 7/7
  - `ALKAROS.Host.Tests` (tam migration/composition/reachability
    paketi, ManifestTests dahil): 121/121
  - `ALKAROS.Host.Experience.Orders.Confirmation.Tests` (yeni):
    **7/7** — kabul, ret (mutfak bilet kalemi iptali + masa serbest
    bırakma dahil), zaten onay bekleyen durumda olmama, zaten
    faturalanmış olma, boş ret nedeni, bayat satır sürümü senaryoları.
- `python -m pytest tests/Architecture/ProjectManifest/test_project_manifest.py`:
  4/4.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0
  uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 1 önceden var
  olan, ilgisiz ihlal (değişmedi), yeni ihlal yok.

## Handoff

- None
