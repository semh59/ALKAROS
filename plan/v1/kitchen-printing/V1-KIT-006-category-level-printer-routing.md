# V1-KIT-006 - Kategori bazlı yazıcı yönlendirmesini gerçek hale getirme

- Task ID: V1-KIT-006
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in talimatıyla ("siparişleri yazıcıya iletirken ayar yapmam lazım,
örneğin ızgara grubu o yazıcıya gitsin", 2026-09-12): bir ürün grubunun
(kategorinin) tamamını tek bir yazıcıya/istasyona yönlendirebilme —
uçtan uca gerçek hale getirildi.

**Önce mevcut altyapı incelendi:** `RouteLevel.Category` ve
`RoutingEvaluationRequest.CategoryId`, `KitchenPrinterRouter`'ın kendi
önceliklendirme zincirinde (Item > Product > DailySpecial > Category >
Default) TAMAMEN doğru uygulanmıştı — ama bunun tek gerçek çağıranı,
`KitchenOrderSubmissionDispatcher.MapItemsToStations`, `CategoryId`'yi HİÇ
doldurmuyordu. Yani bir kategori rotası yapılandırılsa bile, gerçek bir
sipariş gönderiminde asla eşleşmiyordu — kendi kod yorumunda zaten
"deep-analysis finding B-3" olarak işaretlenmiş ama hiç kapatılmamış bir
boşluktu. İkinci boşluk: yönetim uç noktası (`PUT /kitchen-operations/
routes/{routeId}`) yalnız VAR OLAN bir rotayı düzenleyebiliyordu — hiçbir
şey yeni bir rota YARATAMIYORDU, ve PosTerminal'in mutfak ekranı rotaları
yalnız salt-okunur listeliyordu, hiçbir form yoktu. Üç boşluk birlikte,
özelliği baştan sona kullanılamaz kılıyordu.

**Çözüm — üç parça:**

1. `KitchenOrderSubmissionDispatcher`: sevkiyat işleminin kendi
   transaction'ında (`catalog.products.category_id`, yaş sınırı
   çözümlemesiyle aynı desen) her kalemin ürün kategorisini çözüp
   `RoutingEvaluationRequest.CategoryId`'yi dolduruyor artık. Bir kategori
   rotası şimdi gerçekten eşleşiyor.
2. `KitchenOperationsStore.CreateRouteAsync` + yeni
   `POST /kitchen-operations/routes/{routeId}` (aynı `kitchen.routing
   .manage` yetkisi, `routeId` çağıran tarafından üretilir — bu kod
   tabanındaki müşteri-taraflı-id kuralıyla aynı). Repository'nin kendi
   `SaveRouteAsync`'i zaten bir upsert'ti (`INSERT ... ON CONFLICT (id) DO
   UPDATE`); eksik olan tek şey, var-olma zorunluluğu olmayan bir
   store/uç nokta çiftiydi.
3. PosTerminal mutfak ekranına gerçek bir form: bir kategori seç, bir
   yazıcı seç, kaydet — zaten var olan (ama hiçbir istemcinin çağırmadığı)
   uç noktayı çağırıyor. Zaten aktif kategori rotalarını da adlarıyla
   listeliyor.

## Owned surface

- Sınırlı ek:
  - src/Modules/Kitchen/TicketLifecycle/KitchenOrderSubmissionDispatcher.cs
    (Kitchen sahipliğinde) — `ResolveProductCategoriesAsync`,
    `MapItemsToStations`'a `categoryByProductId` parametresi.
  - src/Host/Experience/KitchenOperations/KitchenOperationsStore.cs,
    KitchenOperationsEndpoints.cs (Host sahipliğinde) — `CreateRouteAsync`,
    `POST /routes/{routeId}`.
  - src/Clients/PosTerminal/src/features/kitchen-operations/models.ts,
    kitchenApi.ts, KitchenOperationsWorkspace.tsx,
    KitchenOperationsWorkspace.test.tsx, kitchen-operations.css
    (PosTerminal sahipliğinde) — `KitchenCategory` tipi, kategori listesi
    getirme (Catalog'un kendi `/management/catalog/categories` uç noktası,
    CatalogWorkspace'in zaten yaptığı gibi), `createCategoryRoute`,
    rota ekleme formu.
  - src/Clients/PosTerminal/src/routes/workspace.tsx (PosTerminal
    sahipliğinde) — `onCreateCategoryRoute` bağlantısı, `emptyKitchenData`
    güncellemesi.
  - tests/Modules/Kitchen/TicketLifecycle/KitchenOrderSubmissionDispatcherTests.cs,
    tests/Host/Experience/KitchenOperations/KitchenOperationsHttpTests.cs
    (ilgili test sahipliğinde) — yeni testler.

## Out of scope

- Item/Product/DailySpecial seviyesi rotalar için oluşturma formu — yalnız
  Category seviyesi istendi ve eklendi; diğer seviyeler zaten mevcut PUT
  ile düzenlenebiliyordu (yalnız oluşturma eksikti, o da yalnız Category
  formunun arkasındaki genel uç nokta üzerinden kapatıldı — PUT hâlâ
  Item/Product/DailySpecial rotalarını düzenleyebilir, yalnız PosTerminal'de
  onlar için bir oluşturma FORMU yok).
- Rota silme/pasifleştirme formu — mevcut PUT (`IsActive: false`) zaten bunu
  yapabiliyor ama PosTerminal'de buna da bir buton eklenmedi, yalnız
  oluşturma formu.
- DailySpecial seviyesi (tarihe özel kategori rotası) hiç ele alınmadı — bu
  görevin kapsamı "bir grup hep bu yazıcıya gitsin", tarihe bağlı bir
  istisna değil.

## Dependencies

- V1-KIT-002

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres dotnet
  test`:
  - `tests/Modules/Kitchen/TicketLifecycle` → 21/21 (1 yeni test: aynı
    kategorideki iki farklı ürün aynı yazıcıya gidiyor, kategorisiz bir
    ürün varsayılana düşüyor).
  - `tests/Modules/Kitchen/Routing` → 23/23 (regresyon — router'ın kendi
    önceliklendirme mantığı zaten doğruydu, dokunulmadı).
  - `tests/Host/Experience/KitchenOperations` → 8/8 (1 yeni test: salt-okuma
    yetkisi POST'u 403 ile reddediyor; doğru yetkiyle oluşturma başarılı,
    `/routes` listesinde görünüyor, aynı uç nokta PUT ile sonradan
    düzenlenebiliyor).
- `cd src/Clients/PosTerminal && npx tsc --noEmit` → 0 hata.
- `cd src/Clients/PosTerminal && npx vitest run` → 138/138 (tüm proje,
  izole değil).
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.

## Handoff

- None
