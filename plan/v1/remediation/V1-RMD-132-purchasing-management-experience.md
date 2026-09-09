# V1-RMD-132 - Independent audit: Purchasing had zero HTTP surface

- Task ID: V1-RMD-132
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız denetimin (2026-09-09) mimari bulgusu: Purchasing modülü
(tedarikçi ana verisi, satın alma siparişleri, varyans politikalı mal
kabul — Inventory'ye aynı transaction içinde gerçek stok hareketi işleyen
`PurchasingService.ReceiveGoodsAsync` dahil) tamamen gerçek, Postgres
destekli, zaten test edilmiş bir modüldü ve `PurchasingModule` zaten
`ModuleRegistry.DefaultCatalog`'un bir parçasıydı — ama Host katmanında
hiçbir HTTP uç noktası yoktu. Cashier client'ında ayrı, bellek-içi
`InventoryPurchasingEngine` simülasyonu vardı — hiçbir sayfaya bağlı
değildi (`MenuRecipeAdminEngine` ile aynı sınıf ölü kod, V1-RMD-131'de
zaten belgelendi). Kullanıcının kararı: gerçek bir yönetim API'si kur ve
ölü motoru sil.

## Owned surface

- `plan/v1/remediation/V1-RMD-132-purchasing-management-experience.md`
  (yeni)
- `src/Host/Experience/Purchasing/**` (yeni)
- `database/migrations/V1/V1-RMD-132/**` (yeni)
- `tests/Host/Experience/Purchasing/**` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik
  iddiası olarak parse etmesin):
  - src/Clients/Cashier/InventoryPurchasing/**,
    tests/Clients/Cashier/InventoryPurchasing/** (V11-UI-003
    sahipliğinde) — bu görevin kendi silme kararıyla tamamen kaldırıldı
    (`InventoryPurchasingEngine`/`InventoryPurchasingModels`, hiçbir
    sayfaya bağlı olmayan bellek-içi simülasyon, grep ile doğrulandı).
  - src/Host/DualScreen/DualScreenApplication.cs (ana Host kompozisyonu
    sahipliğinde) — `AddPurchasingManagementExperience()` ve
    `MapPurchasingManagement()` çağrıları, Menu'nün kendi çağrılarının
    hemen yanına eklendi.
  - database/MigrationComposition/order.json,
    src/Host/Composition/Migrations/MigrationManifest.cs (`PhaseBMax`),
    tests/Host/MigrationComposition/Manifest/ManifestTests.cs (V1-FND-004
    sahipliğinde) — migration 090 için standart 4 dosyalık desen (V1-RMD-133
    ile birlikte 090+091 art arda eklendi, bkz. o görevin kendi notu).
  - ALKAROS.slnx (paylaşılan) — yeni
    `ALKAROS.Host.Experience.Purchasing.Tests` eklendi; silinen
    `ALKAROS.Cashier.InventoryPurchasing.Tests` girişi kaldırıldı.

## In scope

1. **Tedarikçi yönetimi** (`GET/POST /suppliers`, `GET/PUT /suppliers/{id}`,
   `POST /suppliers/{id}/activate|deactivate`) — `ISupplierService`'in ince
   sarmalayıcısı. KVKK maskeleme (`SupplierAccessPolicy`) korunuyor —
   yalnız bu API'ye zaten `purchasing.manage` ile erişebilen (dolayısıyla
   maskesiz veriye erişim yetkisi olan) çağıranlar için sabit `"Manager"`
   rolü geçiliyor.
2. **Satın alma siparişleri** (`GET/POST /purchase-orders`,
   `GET /purchase-orders/{id}`, `POST .../submit`, `POST .../cancel`) —
   `IPurchasingService`'in ince sarmalayıcısı.
3. **Mal kabul** (`POST /purchase-orders/{id}/receipts`,
   `GET /purchase-orders/{id}/receipts`, `GET /receipts/{id}`) —
   `ReceivedBy`/`ApprovedBy` artık her zaman kimliği doğrulanmış
   yöneticinin gerçek görünen adından geliyor (önceden serviste
   `string`/`string?` parametre olarak vardı ama hiçbir çağıran yoktu).
4. **Doğrulama: gerçek envanter etkisi.** Yeni HTTP testi tam akışı
   çalıştırıyor (tedarikçi → sipariş → gönder → mal kabul) ve mal
   kabulünün `inventory.stock_balances`'ı GERÇEKTEN değiştirdiğini
   doğruluyor — bu görevin var olma nedeninin ta kendisi.

## Out of scope

- Menu'de zaten bulunan, düzeltilmeyen ayrı bulgu (Catalog'un kendi
  İngilizce hata mesajları) — bu görevin kendi filtresi baştan Türkçe
  yazıldı, mevcut Catalog dosyasına dokunulmadı.
- Audit'in diğer mimari bulguları (#7 BuildingBlocks ölü kütüphaneleri) —
  ayrı, kullanıcıyla görüşülecek bir karar.
- Production'ın kendi bağlanması — bkz. V1-RMD-133 (ayrı görev, aynı
  oturumda art arda kapatıldı).

## Dependencies

- V1-RMD-121
- V1-RMD-124

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 Uyarı, 0 Hata.
- Üç test grubu, gerçek Postgres'e karşı, aynı Docker imajıyla ayrı ayrı
  çalıştırıldı (boru hattı olmadan, gerçek `$?` yakalanarak): yeni
  `ALKAROS.Host.Experience.Purchasing.Tests` (401/403, tam satın
  alma→gönder→mal kabul akışı + gerçek envanter değişikliği doğrulaması,
  pasif tedarikçi siparişi reddi): 3/3; Purchasing'in kendi mevcut
  modül testleri (bu görev modülün kaynak koduna hiç dokunmadı, regresyon
  olmadığını doğrulamak için): `ALKAROS.Purchasing.OrdersAndReceipts.Tests`
  23/23, `ALKAROS.Purchasing.Suppliers.Tests` 29/29; `ALKAROS.Host.Tests`
  (Manifest + Reachability + `ProductionExperienceCompositionTests`,
  yeni `AddPurchasingManagementExperience` kaydı dahil her modül
  servisini çözdüğünü doğruluyor) 121/121 — hepsi gerçek çıkış kodu `0`.
  (İlk deneme, aynı siparişe ikinci kez mal kabul denemesinin sipariş
  zaten `Completed` olduğu için `DUPLICATE_RECEIPT_NUMBER` değil
  `INVALID_STATUS` döndürdüğünü hesaba katmayan hatalı bir test
  varsayımıyla başarısız oldu — düzeltilip aynı konteynerde tekrar
  doğrulandı.)
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 9'dan 1'e düştü
  (`InventoryPurchasingEngine.cs`'in silinmesiyle kendi mojibake ihlalleri
  de gitti — yeni ihlal yok).

## Handoff

- None
