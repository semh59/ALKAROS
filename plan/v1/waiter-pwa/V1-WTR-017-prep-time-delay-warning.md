# V1-WTR-017 - Hafif gecikme kontrolü (hazırlama süresi uyarısı)

- Task ID: V1-WTR-017
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`garson-karsilastirma` karşılaştırma dokümanının "Yeni fikirler" bölümünden
5. madde (Katman B, ilk kalanı): "ürün başına bir 'tahmini hazırlama süresi'
varsa, bir turda süreler arasında büyük fark olduğunda ('salata 3dk, kavurma
25dk') gönderim öncesi tek satırlık bir uyarı — 'birlikte mi göndersin,
salatayı beklesin mi?' — istersem ayrı iki gönderime böler."

Kodda hiçbir yerde bir "hazırlama süresi" kavramı yoktu (Catalog, Recipes,
Production dahil — `grep` boş döndü). Semih'e iki karar soruldu ve
onaylandı:

1. Süre nereden girilsin? → Yönetici, ürün düzenlerken opsiyonel bir alan
   olarak girer (veritabanı seviyesinde idari bir SQL değil).
2. Garson uyarıyı görünce ne olsun? → Tek satırlık soru: birlikte gönder /
   ayrı gönder. "Ayrı gönder" seçilirse taslak otomatik iki tura bölünür
   (hızlı önce), iki ayrı gönderim yapılır.

**Yeni alan:** `Product.PrepTimeMinutes` (`int?`, 1-180 aralığı), aynı
`WithAvailability`/`Suspend`/`Restore` deseninde bir `WithPrepTimeMinutes`
mutator'ı. `CatalogManagementStore.SetProductPrepTimeAsync` +
`POST /products/{id}/prep-time`, `SetProductAvailabilityAsync`'in birebir
aynı tek-alan-güncelleme şekli. PosTerminal'in Katalog ekranına hem "yeni
ürün" formunda opsiyonel bir alan hem seçili üründe ayrı bir
kaydet/temizle kontrolü eklendi.

**Garson tarafı:** `state.products`'a `prepTimeMinutes` eklendi (`/catalog`
zincirinin sonuna kadar: `DualScreenStore.GetCatalogAsync` →
`CatalogProductDto`). Gönder düğmesine artık doğrudan `sendDraft()` değil
`checkDelayAndSend()` bağlı: taslaktaki ürünlerden en az ikisinin bilinen
bir süresi varsa ve en hızlı ile en yavaş arasında ≥10 dakika fark varsa,
tek satırlık bir uyarı sheet'i açılır. "Birlikte gönder" normal `sendDraft()`
çağırır; "Ayrı gönder" taslağı iki gruba böler (yavaş grup: iki uç
arasındaki orta noktaya eşit veya üstü) ve `sendDraft(items)` iki kez ardı
ardına (paralel değil) çağrılır — önce hızlı grup, sonra yavaş grup.
Süresi hiç girilmemiş ürünler karşılaştırmadan tamamen hariç tutulur, hızlı
sayılmaz.

**Şema değişikliğinin gerçek genişliği bu kez küçük kaldı:**
`PostgresProductRepository`nin sorguları (V1-WTR-015'teki `orders.orders`
gibi) her yerde kullanılmıyor — yalnızca `CatalogManagementStore` üzerinden
(katalog yönetim ekranı) çağrılıyor, sipariş oluşturma yolu
(`OrderManagementStore.ResolveCatalogProductsAsync`) kendi ham SQL'ini
kullanıyor ve `prep_time_minutes`'ı hiç seçmiyor. Etkilenen test projeleri
yalnızca: `tests/Modules/Catalog/ProductCatalog` (repository'yi doğrudan
test ediyor — idempotent `ALTER TABLE` ile, `EnsureAvailabilityColumn`'un
yanına `EnsurePrepTimeColumn` eklendi), `tests/Host/Experience/Catalog`
(katalog yönetim HTTP'sini test ediyor — migration 103 fixture listesine
eklendi) ve `/catalog` uç noktasını gerçekten çağıran üç proje:
`tests/Host/Experience/NfcOrdering` (fixture listesine eklendi),
`tests/Host/MigrationComposition` ve `tests/Host/Experience/Composition`
(ikisi de gerçek `database/migrations` dizinini doğrudan uyguluyor, ayrı
bir fixture listesi yok — yeni migration dosyası otomatik alındı).

**İki gerçek regresyon bulundu ve düzeltildi:**

1. `CustomerDisplayContractTests.CatalogProductCarriesRealCategoryMetadata`,
   `CatalogProductDto`'nun JSON alanlarını tam bir allowlist ile
   karşılaştırıyor (tıpkı `modifierGroups`'un V1-RMD-148'de eklendiği gibi)
   — `prepTimeMinutes` eklenince bu test haklı olarak kırıldı; allowlist'e
   eklendi. Bu, tam regresyon turunda (135 test) yakalandı, izole
   çalıştırmada değil.
2. **Kendi blast-radius taramamda kaçırdım:** `tests/Host/Experience/QrOrdering`
   projesi de `DualScreenStore.GetCatalogAsync`'i çağırıyor —
   `GET /api/v1/qr/menu` üzerinden — ama rota adı "catalog" değil "menu"
   içerdiği için ilk taramamdaki `grep "catalog"` deseni bunu yakalamadı.
   Commit sonrası bu projeyi ayrıca çalıştırınca 2 test 503/JSON-parse
   hatasıyla kırıldı; migration 103 bu projenin fixture listesine eklenip
   düzeltildi, aynı commit'e amend edildi. Ders: bir SELECT'e yeni bir sütun
   eklerken hangi projelerin etkilendiğini yalnızca literal metin
   aramasıyla (`grep "catalog"`) değil, çağrılan metodun kendisiyle
   (`grep "GetCatalogAsync"`) taramak gerekir — rota adı yanıltıcı olabilir.

## Owned surface

- `plan/v1/waiter-pwa/V1-WTR-017-prep-time-delay-warning.md` (yeni)
- `database/migrations/V1/V1-WTR-017/**` (yeni)
- Sınırlı ek:
  - src/Modules/Catalog/ProductCatalog/Product.cs,
    PostgresProductRepository.cs (Catalog sahipliğinde) —
    `PrepTimeMinutes` alanı, `WithPrepTimeMinutes`, repository sütun
    eşlemesi.
  - src/Host/Experience/Catalog/CatalogManagementContracts.cs,
    CatalogManagementStore.cs, CatalogManagementEndpoints.cs (Host
    sahipliğinde) — `ProductV1.PrepTimeMinutes`,
    `CreateProductV1.PrepTimeMinutes`, `SetProductPrepTimeV1`,
    `SetProductPrepTimeAsync`, `POST /products/{id}/prep-time`.
  - src/Host/DualScreen/DualScreenContracts.cs, DualScreenStore.cs (Host
    sahipliğinde) — `CatalogProductDto.PrepTimeMinutes`,
    `GetCatalogAsync`'in SELECT'ine eklendi.
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js, waiter-app.css (V1-WTR-010
    sahipliğinde) — `checkDelayAndSend`, `confirmDelayChoice`,
    `productPrepTimeMinutes`, `sendDraft`/`draftToPayload`'ın kısmi-gönderim
    desteği, gecikme uyarı sheet'i.
  - src/Clients/PosTerminal/src/features/catalog/{models,catalogApi,
    CatalogWorkspace}.{ts,tsx}, catalog.css, src/routes/workspace.tsx
    (PosTerminal sahipliğinde) — `prepTimeMinutes` alanı, `setPrepTime`
    API çağrısı, ürün formunda opsiyonel alan, seçili üründe kaydet/temizle
    kontrolü.
  - database/MigrationComposition/order.json — 103 kaydı.
  - src/Host/Composition/Migrations/MigrationManifest.cs — PhaseBMax.
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs — manifest
    testinin sınır değerleri.
  - tests/Host/MigrationComposition/DualScreen/CustomerDisplayContractTests.cs
    (Host test sahipliğinde) — allowlist'e `prepTimeMinutes` eklendi
    (yukarıda açıklanan gerçek regresyon).
  - tests/Modules/Catalog/ProductCatalog/PostgresRepositoryTests.cs,
    tests/Host/Experience/Catalog/{ALKAROS.Host.Experience.Catalog.Tests.csproj,
    CatalogManagementHttpTests.cs},
    tests/Host/Experience/NfcOrdering/ALKAROS.Host.Experience.NfcOrdering.Tests.csproj,
    tests/Host/Experience/QrOrdering/ALKAROS.Host.Experience.QrOrdering.Tests.csproj
    (ilgili test sahipliklerinde) — yeni testler / fixture eklemeleri.
  - src/Clients/PosTerminal/src/features/catalog/{CatalogWorkspace.test.tsx,
    catalogApi.test.ts} (PosTerminal test sahipliğinde) — yeni testler.

## Out of scope

- Reçete/Production modülünden otomatik hazırlama süresi türetme —
  karşılaştırma dokümanı bunu "zaten türetilebilir" olarak not etmişti ama
  gerçek Recipes/Production şemasında böyle bir alan yok; bu görev bilinçli
  olarak yalnızca ürün üzerinde elle girilen bir alan ekliyor.
- Mutfak/kurs sırası entegrasyonu (course management) — ayrı, çok daha
  büyük bir görev (comparison doc'un kendi "gaps" listesinde).

## Dependencies

- V1-WTR-010
- V1-RMD-176

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` (tüm çözüm) → 0 uyarı, 0 hata.
- `node --check waiter-app.js` → temiz; `waiter-app.css` parantez dengesi
  252/252.
- `npx tsc --noEmit` (PosTerminal) → temiz.
- `npx vitest run src/features/catalog src/routes/workspace.test.tsx`
  (PosTerminal) → 27/27 yeşil (18 katalog + 9 workspace).
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres
  ALKAROS_KITCHEN_STATION_ID=test-station dotnet test`:
  - `tests/Modules/Catalog/ProductCatalog` → 81/81.
  - `tests/Host/Experience/Catalog` → 12/12 (2 yeni test: hazırlama
    süresi kaydedilir ve temizlenir; 1-180 aralığı dışı 400
    VALIDATION_FAILED ile reddedilir).
  - `tests/Host/Experience/NfcOrdering` → 17/17.
  - `tests/Host/Experience/QrOrdering` → 17/17 (yukarıdaki 2. regresyonun
    düzeltmesiyle; ilk koşuda 2/17 kırıktı).
  - `tests/Host/MigrationComposition` (135 test, gerçek migration
    dizinini uygulayan tam host composition testleri dahil) → tam koşuda
    ilk seferde 1 gerçek regresyon bulundu ve düzeltildi (yukarıda
    açıklandı — `CustomerDisplayContractTests`); düzeltmeden sonra
    135/135 yeşil. (Ayrı bir koşuda görülen 5 başarısızlık —
    `DualScreenStoreTests` — izole ve tekrar tam koşuda tekrarlanmadı;
    paralel test yükü altında bağlantı kaynaklı gürültü olarak
    değerlendirildi, bu görevden kaynaklanmadığı stash/izole karşılaştırmayla
    doğrulandı.)
  - `tests/Host/Experience/Composition` → 9/10 yeşil; 1 başarısız test
    (`ServeContainerResolvesEveryModuleServiceFromTheModuleCatalog`) bu
    görevden önce de aynı şekilde başarısız (git stash ile doğrulandı) —
    `IEscalationResolver` DI kayıt sırası flake'i, bu görevin kapsamı
    dışında, önceden var olan bir kusur.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.

## Handoff

- None
