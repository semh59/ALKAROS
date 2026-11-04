# V1-RMD-800 - Ürün "yarım porsiyon olur" bayrağı ve kesirli miktar doğrulaması

- Task ID: V1-RMD-800
- Status: Done
- Assignee: claude-code-session_019vXeYXjE3sM6VZKpRMj1L9
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesinin G5 grubunun kesirli miktar kısmı (`7-birlesik-dogrulama.md` satır 13: `DualScreenStore.Orders.cs:224`, `PaymentRefunds.cs:236`). Bugün ürün bazında bayrak yok: sipariş miktarı `0 < q <= 999` aralığındaki herhangi bir ondalık olabiliyor (örneğin 1,3 veya 0,001), iade 3 ondalığa kadar kabul ediyor. PO kararı (2026-11-04): ürün formunda "yarım porsiyon olur" kutusu; yalnız işaretli üründe 1,5 gibi yarım katlı adet kabul edilir, diğer ürünlerde adet tam sayı olmalıdır.

1. Katalog: `catalog.products.allows_half_portion` (varsayılan false). `Product` bayrağı taşır; yönetim uçları oluşturma, listeleme ve ürün ayrıntı güncellemesinde (`POST .../products/{id}/details`, alan boş bırakılırsa değer korunur) bayrağı taşır. Geçiş 216, daha önce kesirli adetle satılmış ürünlerin bayrağını açık başlatır (bayraktan önceki veri satışta kesilmez). Bayrağın düşürülmesi mevcut siparişlere dokunmaz.
2. Kural tek yerde: `OrderQuantityRule.IsAllowed(miktar, bayrak)` (miktar > 0; tam sayı her zaman; bayrak açıksa 0,5'in katı). Sipariş ekleme (kasa hattı `AddItemAsync`, miktar değiştirme `MutateExistingItemAsync`) ve masa taslağı (`TableDraftService`) bu kuralı katalogdan okunan bayrakla uygular; ihlalde 400 ve sabit kod `ORDER_QUANTITY_NOT_ALLOWED` (Türkçe nedenli). QR ve NFC müşteri kanalları miktarı zaten `int` taşır, değişmez.
3. İade: `PaymentRefunds.PriceItemsAsync` aynı kuralı uygular; geriye uyum için bayrağı kapalı üründe satış miktarı kesirli olan kalem (bayraktan önceki veri) ve yarımlarla iade edilmiş kalemin kalan kesri eskisi gibi iade edilebilir; bayrağı açık üründe yalnız 0,5'in katları kabul edilir. Masa taslağında siparişin zaten tuttuğu aynı satır ve miktar, bayrak sonradan kapansa da kaydedilmeye devam eder; kasa hattında birleşen toplam da kurala tabidir. Daha önce kaydedilmiş iade istekleri yeniden doğrulanmaz.
4. Günlük menü sayaçları (`NUMERIC(10,3)`): 1,5 adetlik kalemin rezervasyonu, tüketimi ve iptal/iadede geri alınması 1,5 porsiyon olarak sayılır; bu testle kanıtlanır (kod değişmesi gerekirse düzeltilir).

Kapsam dışı: bayrağı kullanan arayüz (V1-RMD-803), çevrimiçi sağlayıcı siparişleri (miktarı sağlayıcı belirler), müşteri ekranı sözleşmesi (V1-RMD-810 çalışıyor; bayrak ürün listesine arayüz görevinde eklenir).

## Owned surface

- `evidence/V1-RMD-800/**`
- `database/migrations/V1/V1-RMD-800/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Catalog/ProductCatalog/Product.cs (V1-WTR-017) — bayrak alanı.
  - src/Modules/Catalog/ProductCatalog/PostgresProductRepository.cs (V1-RMD-076) — sütun okuma ve yazma.
  - src/Modules/Orders/OrderAggregate/OrderMath.cs (V1-RMD-467) — tek miktar kuralı ve hata türü.
  - src/Host/Experience/Catalog/CatalogManagementContracts.cs (V1-WTR-017) — sözleşme alanı ve tek alan isteği.
  - src/Host/Experience/Catalog/CatalogManagementStore.cs (V1-RMD-119) — oluşturma, liste, tek alan güncelleme.
  - src/Host/DualScreen/DualScreenStore.Orders.cs (V1-RMD-774) — ekleme doğrulaması.
  - src/Host/DualScreen/DualScreenStore.cs (V1-RMD-120) — miktar değiştirme doğrulaması.
  - src/Host/Experience/Orders/TableDraft/TableDraftService.cs (V1-RMD-774) — taslak doğrulaması.
  - src/Host/Experience/Billing/PaymentRefunds.cs (V1-RMD-793) — iade miktarı kuralı.
  - database/MigrationComposition/order.json (V1-FND-004) — yeni migration kaydı.
  - src/Host/Composition/Migrations/MigrationManifest.cs (V1-FND-004) — yalnız PhaseBMax.
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs (V1-GOV-007) — yalnız yeni migration kimliği.
  - tests/Host/MigrationComposition/Migrations/MigrationFileAndSchemaTests.cs (V1-RMD-698) — geri alma ve yeniden uygulama.
  - tests/Modules/Orders/OrderAggregate/OrderDomainTests.cs (V1-RMD-467) — kural birim testleri.
  - tests/Modules/Catalog/ProductCatalog/PostgresRepositoryTests.cs (V1-WTR-017) — sütun gidiş dönüşü.
  - tests/Host/Experience/Catalog/CatalogManagementHttpTests.cs (V1-RMD-426) — uç testleri.
  - tests/Host/Experience/Catalog/ALKAROS.Host.Experience.Catalog.Tests.csproj (V1-RMD-063) — migration fixture.
  - tests/Host/Experience/Billing/RefundHalfPortionHttpTests.cs (V1-RMD-508) — yeni dosya: iade miktarı testleri.
  - tests/Host/Experience/Orders/TableDraft/TableDraftHalfPortionHttpTests.cs (V1-RMD-146) — yeni dosya: taslak testleri.
  - tests/Host/MigrationComposition/DualScreen/DualScreenHalfPortionTests.cs (V1-RMD-120) — yeni dosya: kasa hattı testleri.
  - tests/Modules/Menu/CounterProjection/DailyMenuCounterEndToEndTests.cs (V1-RMD-783) — kesirli sayaç testi.
  - tests/Host/Experience/Orders/TableDraft/OrderManagementTableDraftHttpTests.cs (V1-RMD-146) — yarım porsiyon kullanan iki mevcut test bayraklı ürüne geçer.
  - tests/Host/Experience/Orders/TableDraft/ALKAROS.Host.Experience.Orders.TableDraft.Tests.csproj (V1-RMD-486) — migration fixture.
  - tests/Host/Experience/Orders/Comp/ALKAROS.Host.Experience.Orders.Comp.Tests.csproj (V1-RMD-184) — migration fixture.
  - tests/Host/Experience/Orders/Confirmation/ALKAROS.Host.Experience.Orders.Confirmation.Tests.csproj (V1-RMD-184) — migration fixture.
  - tests/Host/Experience/Orders/Void/ALKAROS.Host.Experience.Orders.Void.Tests.csproj (V1-RMD-184) — migration fixture.
  - tests/Host/Experience/Orders/VoidSent/ALKAROS.Host.Experience.Orders.VoidSent.Tests.csproj (V1-RMD-184) — migration fixture.
  - tests/Host/Experience/OnlineOrdering/ALKAROS.Host.Experience.OnlineOrdering.Tests.csproj (V1-RMD-472) — migration fixture.
  - tests/Modules/OnlineOrdering/AvailabilityPublishing/ALKAROS.OnlineOrdering.AvailabilityPublishing.Tests.csproj (V12-ONL-004) — migration fixture.
  - tests/Modules/OnlineOrdering/AvailabilityPublishing/AvailabilityPublicationTests.cs (V12-ONL-004) — fixture migration listesi.
  - tests/Modules/OnlineOrdering/CatalogPublishing/ALKAROS.OnlineOrdering.CatalogPublishing.Tests.csproj (V12-ONL-003) — migration fixture.
  - tests/Modules/OnlineOrdering/CatalogPublishing/CatalogPublicationTests.cs (V1-RMD-477) — fixture migration listesi.
  - tests/Modules/OnlineOrdering/Yemeksepeti/OrderNormalization/ALKAROS.OnlineOrdering.Yemeksepeti.OrderNormalization.Tests.csproj (V12-ONL-007) — migration fixture.
  - tests/Modules/OnlineOrdering/Yemeksepeti/OrderNormalization/YemeksepetiOrderNormalizerTests.cs (V12-ONL-007) — fixture migration listesi.
  - tests/Modules/OnlineOrdering/Yemeksepeti/ProductMapping/ALKAROS.OnlineOrdering.Yemeksepeti.ProductMapping.Tests.csproj (V12-MAP-001) — migration fixture.
  - tests/Modules/OnlineOrdering/Yemeksepeti/ProductMapping/ProductMappingTestDatabase.cs (V12-MAP-001) — fixture migration listesi.
  - src/Host/Composition/Errors/ApiErrorCatalog.cs (V1-RMD-431) — yeni hata kodu.
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs (V1-RMD-127) — masa taslağı hata eşlemesi.
  - tests/Host/Errors/ApiErrorCatalogTests.cs (V1-RMD-431) — yeni hata kodu satırı.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Her kabul koşulu için önce kırmızı test (gerçek Postgres), sonra düzeltme, sonra yeşil; mutasyon turu.
2. Migration 216 (ileri, geri, tekrar ileri) ve kayıt dosyaları; `catalog.products` kullanan test fixture'larının migration listelerine yeni dosya.
3. Gerçek Postgres testleri: bayraksız üründe 1,5 kasa hattı, masa taslağı ve miktar değişiminde 400; bayraklı üründe 1,5 kabul, 1,3 ret; tam sayı her üründe kabul; iade: bayraksız tam satırda kesirli ret, bayraklı kabul, kesirli satış kaleminde eski serbestlik; günlük menü sayacı 1,5 porsiyon rezerve eder, tüketir ve iptalde geri alır.
4. İngilizce kod ve test adları; kullanıcıya dönen hata metinleri Türkçe.

## Out of scope

- Arayüz (V1-RMD-803), müşteri ekranı sözleşmesi, çevrimiçi sağlayıcı siparişleri, bayrak kapatılınca eski siparişlerin yeniden yazılması.

## Dependencies

- V1-RMD-783
- V1-RMD-793

## Acceptance evidence

1. Kırmızı sonra yeşil: yeni test dosyaları ile `PostgresRepositoryTests`, `CatalogManagementHttpTests`, `OrderDomainTests`, TableDraft, Billing (iade), Menu CounterProjection, ManifestTests, ApiConventions ve ModuleBoundaries, gerçek exit code ile `evidence/V1-RMD-800/` altında.
2. Migration 216 ileri/geri/ileri kanıtı.
3. Mutasyon: kural koşulu, her çağrı noktası (ekleme, değiştirme, taslak, iade) ve geriye uyum dalı bozulunca en az bir test kırılır; kaynak `cmp` ile geri alınır.
4. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
5. qa-backend ve qa-test-kalitesi incelemeleri sırayla, bulgular giderilir, düzeltmelerden sonra doğrulama turu.
6. Semih'in elle deneyeceği senaryo: bir ürünü "yarım porsiyon olur" olarak işaretle; satışta 1,5 adet gir (kabul); işaretsiz üründe 1,5 gir (reddedilir, nedeni yazar); işaretli üründen satılmış kalemde yarım porsiyon iade iste (kabul).

## Handoff

- None
