# V1-RMD-128 - Independent audit: order-item is_available check missing

- Task ID: V1-RMD-128
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız denetimin (2026-09-09) bir başka kritik bulgusu:
`catalog.products` iki ayrı, birbirinden bağımsız bayrak taşıyor —
`active` (kalıcı, ana-veri var/yok bayrağı) ve `is_available`
(`CatalogManagementStore.SetProductAvailabilityV1` ile bir yöneticinin
anlık olarak açıp kapatabildiği, "bugün tükendi/86'landı" bayrağı,
`Product.Suspend()`/`Restore()`). Terminal-çapında hızlı satış yolu
(`DualScreenStore.Orders.cs`) her ikisini de doğru kontrol ediyordu, ama
üç ayrı sipariş-kalemi çözümleme sorgusu yalnız `active`'i kontrol edip
`is_available`'ı hiç sormuyordu:
`OrderManagementStore.ResolveCatalogProductsAsync` (garson masa taslağı),
`NfcOrderingStore`'un eşdeğeri (müşterinin NFC ile kendi kendine
sipariş verdiği, hiçbir personelin araya giremediği yol) ve
`QrPendingOrderStore.ResolveCatalogProductsAsync` (QR müşteri
sipariş yolu, aynı şekilde personelsiz). Sonuç: bir yönetici bir ürünü
"tükendi" işaretlese bile, o ürün bu üç yoldan mutfağa kadar sorunsuzca
ilerleyebiliyordu — özellikle NFC/QR yollarında bunu durduracak hiçbir
insan denetimi yok.

## Owned surface

- `plan/v1/remediation/V1-RMD-128-order-item-availability-check.md` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır (yollar
  geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası olarak
  parse etmesin):
  - src/Host/Experience/Orders/OrderManagementStore.cs (V1-RMD-113
    sahipliğinde) — `ResolveCatalogProductsAsync`'in `WHERE` koşuluna
    `AND p.is_available` eklendi; ürün-bulunamadı istisna mesajı
    güncellendi.
  - src/Host/Experience/NfcOrdering/NfcOrderingStore.cs (V12-NFC-001/
    V12-NFC-002 sahipliğinde) — aynı düzeltme, aynı istisna mesajı
    güncellemesi.
  - src/Modules/QrOrdering/PendingOrders/QrPendingOrderStore.cs
    (V12-QRO-001 sahipliğinde) — aynı düzeltme; `QrOrderInvalidProductException`
    mesajı ve özet belgesi güncellendi.
  - tests/Host/Experience/Orders/TableDraft/OrderManagementTableDraftTestDatabase.cs,
    OrderManagementTableDraftHttpTests.cs,
    ALKAROS.Host.Experience.Orders.TableDraft.Tests.csproj (aynı görev
    sahipliğinde) — `SeedProductAsync`'e opsiyonel `isAvailable` parametresi
    (varsayılan `true`, mevcut hiçbir çağrı değişmedi), yeni
    `DraftingAnUnavailableProductIsRejected` testi, eksik migration 040
    fixture'ı eklendi (bu tabloya `is_available` sütununu ekleyen migration
    bu projenin fixture listesinde hiç yoktu — bağımsız bir eksiklik,
    şimdiye kadar hiçbir test bu sütuna dokunmadığı için fark edilmemiş).
  - tests/Host/Experience/NfcOrdering/NfcOrderingTestDatabase.cs,
    NfcOrderingHttpTests.cs (aynı görev sahipliğinde) — aynı desen; bu
    projenin fixture listesinde migration 040 zaten vardı.
  - tests/Modules/QrOrdering/PendingOrders/Fixtures/QrOrderingPendingOrdersTestDatabase.cs,
    QrPendingOrderStoreTests.cs,
    ALKAROS.QrOrdering.PendingOrders.Tests.csproj (aynı görev sahipliğinde)
    — aynı desen; eksik migration 040 fixture'ı eklendi (TableDraft ile
    aynı, bağımsız eksiklik).

## In scope

1. Üç `ResolveCatalogProductsAsync` sorgusuna (`OrderManagementStore`,
   `NfcOrderingStore`, `QrPendingOrderStore`) `AND p.is_available` eklendi
   — artık üçü de terminal-çapında hızlı satış yoluyla (`DualScreenStore
   .Orders.cs`) birebir aynı, doğru deseni izliyor.
2. Her üç "ürün bulunamadı" istisna mesajı, artık is_available'ın da
   nedenlerden biri olabileceğini yansıtacak şekilde güncellendi (yalnız
   iç günlük/hata ayıklama metni — kullanıcıya dönen mesaj zaten ayrı bir
   Türkçe çeviri katmanından geçiyor, değişmedi).
3. Her üç yol için de bir regresyon testi eklendi: manager tarafından
   "tükendi" işaretlenmiş bir ürünün draft/sipariş isteğine eklenmesi artık
   reddediliyor (`OrderManagementTableDraftHttpTests
   .DraftingAnUnavailableProductIsRejected`,
   `NfcOrderingHttpTests.AnUnavailableProductIsRejected`,
   `QrPendingOrderStoreTests.AnUnavailableProductFails`).
4. Bağımsız bir keşif: hem TableDraft hem QrOrdering PendingOrders test
   projelerinin migration fixture listesinde `is_available` sütununu
   ekleyen migration (040, V1-GOV-040) hiç yoktu — bu sütuna dokunan ilk
   test bu görevin kendi yeni testleri olduğu için şimdiye kadar fark
   edilmemiş bir eksiklik. İkisine de eklendi (NfcOrdering'de zaten vardı).

## Out of scope

- Audit'in aynı ailedeki diğer bulguları (#11-13 WaiterPwa alan
  uyuşmazlıkları) — ayrı bir remediation görevi.
- `DualScreenStore.Orders.cs`'in kendisi — zaten doğru, değiştirilmedi.

## Dependencies

- V1-RMD-113
- V12-NFC-001
- V12-QRO-001

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 Uyarı, 0 Hata.
- Bu görev sırasında paylaşılan Docker VM belleği başka, ilgisiz
  projelerin konteynerleri tarafından doldurulmuş durumdaydı (V1-RMD-126/
  127 ile aynı ortam koşulu). Etkilenen üç test projesi tek bir konteyner
  çalıştırmasında, aynı Docker imajıyla, gerçek Postgres'e karşı
  çalıştırıldı (boru hattı olmadan, gerçek `$?` yakalanarak):
  `ALKAROS.Host.Experience.Orders.TableDraft.Tests`: 14/14,
  `ALKAROS.Host.Experience.NfcOrdering.Tests`: 13/13,
  `ALKAROS.QrOrdering.PendingOrders.Tests`: 8/8 — gerçek çıkış kodu `0`.
  (İlk deneme, eksik migration 040 fixture'ı yüzünden 11/14 hatayla
  başarısız oldu — fixture eklenip aynı konteynerde tekrar çalıştırılarak
  doğrulandı.)
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 13 önceden var
  olan ihlal (değişmedi), yeni ihlal yok.

## Handoff

- None
