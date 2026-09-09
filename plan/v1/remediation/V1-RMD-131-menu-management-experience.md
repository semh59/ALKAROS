# V1-RMD-131 - Independent audit: Menu module had zero HTTP surface

- Task ID: V1-RMD-131
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız denetimin (2026-09-09) mimari bulgusu: Menu modülü (kalıcı,
adlandırılmış menüler — `StaticMenu` — ve bir servis gününün özel menüsü
lifecycle'ı — `DailyMenuLifecycle`, porsiyon takibiyle) tamamen gerçek,
Postgres destekli, zaten test edilmiş servisler/repository'lerle
oluşturulmuştu ve `MenuModule` zaten `ModuleRegistry.DefaultCatalog`'un
bir parçasıydı (yani `IStaticMenuService`/`IDailyMenuService` gerçek
"serve" DI konteynerinde her zaman kayıtlıydı) — ama Host katmanında
hiçbir HTTP uç noktası bunları hiç haritalamıyordu. Hiçbir istemci bu
modüle hiç ulaşamıyordu. Ayrıca Cashier client'ında `MenuRecipeAdminEngine`
diye ayrı, tamamen bellek-içi bir simülasyon vardı — hiçbir sayfaya
bağlı değildi (`OrderEntryEngine`/`WaiterOfflineQueueEngine` ile aynı
sınıf ölü kod, `docs/engineering/v1-independent-audit.md`'de zaten
belgelenmiş emsal). Kullanıcının kararı: gerçek bir yönetim API'si kur
(Catalog/Kitchen yönetim uç noktaları örüntüsünde) ve ölü motoru sil.

## Owned surface

- `plan/v1/remediation/V1-RMD-131-menu-management-experience.md` (yeni)
- `src/Host/Experience/Menu/**` (yeni)
- `database/migrations/V1/V1-RMD-131/**` (yeni)
- `tests/Host/Experience/Menu/**` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik
  iddiası olarak parse etmesin):
  - src/Host/DualScreen/DualScreenApplication.cs (V1-ORD-005/ana Host
    kompozisyonu sahipliğinde) — `AddMenuManagementExperience()` ve
    `MapMenuManagement()` çağrıları, Catalog'un kendi çağrılarının hemen
    yanına eklendi.
  - database/MigrationComposition/order.json,
    src/Host/Composition/Migrations/MigrationManifest.cs (`PhaseBMax`),
    tests/Host/MigrationComposition/Manifest/ManifestTests.cs (V1-FND-004
    sahipliğinde) — migration 089 için standart 4 dosyalık desen.
  - ALKAROS.slnx (çözüm dosyası, paylaşılan) — yeni
    `ALKAROS.Host.Experience.Menu.Tests` projesi eklendi; silinen
    `ALKAROS.Cashier.MenuRecipeAdmin.Tests` girişi kaldırıldı.
  - src/Clients/Cashier/MenuRecipeAdmin/**,
    tests/Clients/Cashier/MenuRecipeAdmin/** (bu görevin kendi silme
    kararı) — tamamen silindi: `MenuRecipeAdminEngine`/
    `MenuRecipeAdminModels` yalnız kendi dosyalarından referans
    alınıyordu (grep ile doğrulandı, hiçbir sayfa/UI onları hiç
    çağırmıyordu) — `OrderEntryEngine`/`WaiterOfflineQueueEngine` ile
    aynı sınıf ölü kod, aynı önceki karar (bkz.
    `docs/engineering/v1-independent-audit.md` H4).

## In scope

1. **Kalıcı menü yönetimi** (`POST/PUT /menus`, `POST /menus/{id}/items`,
   `PUT /menus/{id}/items/{itemId}`, `POST /menus/{id}/items/reorder`,
   `GET /menus`, `GET /menus/{id}`) — `IStaticMenuService`'in ince bir
   HTTP sarmalayıcısı, mevcut hiçbir iş kuralı yeniden yazılmadı.
2. **Günlük özel menü yaşam döngüsü** (`POST /daily-menus`,
   `POST /daily-menus/{id}/open`, `POST /daily-menus/{id}/close`,
   `POST /daily-menus/{id}/items`, `PUT .../items/{itemId}/price`,
   `PUT .../items/{itemId}/planned-portions`,
   `PUT .../items/{itemId}/status`, `GET /daily-menus/{id}`,
   `GET /daily-menus/by-date/{date}`) — aynı desen, `IDailyMenuService`'in
   ince sarmalayıcısı. `ClosedBy`/`ChangedBy`/`CreatedBy` alanları artık
   her zaman kimliği doğrulanmış yönetici kimliğinden geliyor (önceden
   servis imzalarında `Guid?` olarak vardı ama hiçbir çağıran yoktu).
3. **Yetkilendirme.** `menu.manage` (yeni, yalnız yönetici — migration
   089), Catalog'un kendi `catalog.manage`/`CatalogManagerEndpointFilter`
   deseniyle birebir aynı: `alkaros.manager` çerezi +
   `ManagementSessionLookup.ResolveActorAsync` (V1-RMD-121'in paylaşılan
   yardımcı fonksiyonu — kod tekrarı yaratmadan).
4. **Hata eşlemesi Türkçe.** Catalog'un kendi filtre sınıfı İngilizce
   mesajlar döndürüyor (`docs/UI_STYLE_GUIDE.md` ihlali — ayrı,
   bu görevde bulunan ama düzeltilmeyen bir bulgu, bkz. Out of scope);
   bu görevin kendi `MenuManagerEndpointFilter`'ı Catalog'un desenini
   kasıtlı olarak KOPYALAMADI ve tüm mesajları doğrudan Türkçe yazdı.
5. **Ölü kod temizliği.** `MenuRecipeAdminEngine`/`MenuRecipeAdminModels`
   (Cashier client, bellek-içi simülasyon, hiçbir sayfaya bağlı değil) ve
   onların test projesi tamamen silindi.

## Out of scope

- **Yeni bulgu, düzeltilmedi:** `CatalogManagementEndpoints
  .CatalogManagerEndpointFilter`'ın hata mesajları İngilizce
  ("Authentication is required.", "The catalog manager permission is
  required." vb.) — `docs/UI_STYLE_GUIDE.md` ihlali. Bu görev yalnız
  KENDİ yeni filtresini doğru (Türkçe) yazdı, mevcut Catalog dosyasına
  dokunmadı (ayrı sahiplik, ayrı görev gerektiriyor).
- Öğe seviyesinde yazıcı yönlendirmesi vb. Menu'nün DIŞINDAKİ diğer
  mimari bulgular (#6 Production/Purchasing, #7 BuildingBlocks ölü
  kütüphaneleri) — ayrı, kullanıcıyla görüşülecek kararlar.
- Bu yeni API'ye gerçek bir istemci arayüzü (PosTerminal/WaiterPwa'da bir
  "menü yönetimi" ekranı) inşa etmek — kullanıcı yalnız backend API'sini
  ve ölü motorun silinmesini istedi; bir UI ayrı bir karar/görev.

## Dependencies

- V1-RMD-121
- V12-QRT-003

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 Uyarı, 0 Hata.
- Üç test grubu ayrı ayrı, gerçek Postgres'e karşı, aynı Docker imajıyla
  çalıştırıldı (boru hattı olmadan, gerçek `$?` yakalanarak):
  yeni `ALKAROS.Host.Experience.Menu.Tests` (401/403/mutasyon-yok,
  kalıcı menü oluşturma/kompozisyon/yeniden sıralama, yinelenen kod
  reddi, tam günlük menü yaşam döngüsü, kapalı menüye ekleme reddi,
  bilinmeyen tarih 404): 5/5;
  `ALKAROS.Host.Tests` (Manifest + Reachability + gerçek "serve"
  konteynerinin `AddMenuManagementExperience`'ı dahil her modül
  servisini çözdüğünü doğrulayan `ProductionExperienceCompositionTests`):
  121/121; Menu modülünün kendi mevcut testleri (bu görev modülün
  kaynak koduna hiç dokunmadı, regresyon olmadığını doğrulamak için):
  `ALKAROS.Menu.StaticMenu.Tests` 18/18, `ALKAROS.Menu.DailyMenuLifecycle
  .Tests` 14/14, `ALKAROS.Menu.CounterProjection.Tests` 6/6 — hepsi gerçek
  çıkış kodu `0`. (İlk deneme, `Menu.Create`'in kodu büyük harfe
  normalize ettiğini hesaba katmayan bir test doğrulamasıyla 4/5
  başarısız oldu — düzeltilip aynı konteynerde tekrar doğrulandı.)
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 13 → 9 ihlal
  (`MenuRecipeAdminEngine.cs`'in silinmesiyle kendi 4 mojibake ihlali de
  gitti — yeni ihlal yok, beklenen düşüş).

## Handoff

- None
