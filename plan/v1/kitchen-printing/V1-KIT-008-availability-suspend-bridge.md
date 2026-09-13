# V1-KIT-008 - Mutfak ekranından ürün tükendi bildirimi (86'lama köprüsü)

- Task ID: V1-KIT-008
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in kararı (2026-09-13): bir ürün tükendiğinde, Mutfak Şefi bunu
bulunduğu yerden (Mutfak ekranından) bildirebilsin — bugün bu yalnız
Catalog Management'tan (`POST /api/v1/management/catalog/products/{id}
/availability`, `Product.Suspend()/Restore()`) mümkün. Bağımsız denetimde
(K2) bulunan gerçek engel: Kitchen uçları terminal-bağlı **cashier
session** kullanıyor, Catalog'un mevcut ucu ise ayrı bir **manager
cookie** + `catalog.manage` izni istiyor — iki farklı oturum modeli, "aynı
API'yi çağır" kadar basit değil. Çözüm: Kitchen terminaline ikinci bir
oturum türü eklemek yerine, Kitchen modülüne Catalog'un yazma kapısına
düzgün bir cross-module port verilir (Kitchen zaten kendi
`KitchenOrderSubmissionDispatcher`'ında `catalog.products`'ı aynı
transaction içinde okuyor — V1-RMD-137/V1-KIT-006 emsali) ve Kitchen'ın
KENDİ cashier-session modeli altında yeni bir `kitchen.availability
.suspend` izniyle korunan bir uç açılır. Ürün, planla (ör. bekleyen
stok/tahmin) çelişen bir durumda tükendi işaretlenirse, aynı işlemde
`authorization_grants` üzerinden nöbetteki yönetici/şef garson'a
denetlenebilir bir bildirim kaydı düşülür (bağımsız denetim K3'ün
önerdiği, zaten var olan örüntü — `WaiterOrderStatusHub` broadcast'i
DEĞİL). Mimari basitleştirme (ilk taslaktan sonra, "cross-module port"
icat etmek yerine): Kitchen, `catalog.products`'ı zaten kendi
transaction'ında doğrudan okuyor (`KitchenOrderSubmissionDispatcher`,
V1-RMD-137/V1-KIT-006 emsali) — aynı şekilde Host katmanında
`KitchenOperationsStore`, var olan `CatalogManagementStore`'u (Catalog
modülünün kendi Host-katmanı sınıfı, zaten `SetProductAvailabilityAsync`
taşıyor) DI ile enjekte edip doğrudan çağırır. Domain (`src/Modules/
Catalog/ProductCatalog/**`) hiç değişmez, yeni bir port/arayüz icat
edilmez.

## Owned surface

- src/Host/Experience/KitchenOperations/KitchenOperationsEndpoints.cs,
  KitchenOperationsStore.cs, KitchenOperationsContracts.cs (Sınırlı ek —
  V1-RMD-082 sahipliğinde kalan dosyalar) — yeni `POST
  /kitchen-operations/products/{productId}/suspend` ucu; yerel
  `kitchen.availability.suspend` sabiti (mevcut `TicketMutationPermission`
  emsaliyle aynı desende, bu dosyada tanımlanır); `KitchenOperationsStore`'a
  `CatalogManagementStore` DI enjeksiyonu (bu nedenle yalnız
  `KitchenOperationsStore`'un DI kaydı Singleton'dan Scoped'a indi —
  Catalog'un kendi `CatalogManagementStore`'u zaten Scoped;
  `IKitchenOperationsSessionAuthorizer` Singleton olarak kaldı, Scoped bir
  şeye bağımlı değil).
- authorization_grants yazma yolu (Sınırlı ek, paylaşılan) — doğrudan
  `IAuthorizationGrantRepository.InsertAsync` ile, `AuthorizationGrant.cs`/
  `AuthorizationGrantService.cs`'e HİÇ dokunmadan: derin inceleme (bu görev
  kapsamında) `IAuthorizationGrantService.RequestAsync`'in bir ÖN-onay/
  engelleme mekanizması olduğunu (izni zaten taşımayan bir rol için,
  Pending'de bekleten) doğruladı — zaten `kitchen.availability.suspend`
  taşıyan bir şefin işlemini bloklamak yanlış olurdu. `InsertAsync`'in kendi
  sözleşmesi ("Pending... veya Path'i set edilmiş bir terminal durum")
  doğrudan zaten-çözümlenmiş (`Granted`/`Auto`) bir satır yazmaya izin
  veriyor; tetikleyici sadece INSERT/UPDATE'te çalışıyor, bu yolu
  engellemiyor.
- `database/migrations/V1/V1-KIT-008/**` (yeni) — `kitchen.availability.
  suspend` izin kodunu `identity.permissions`'a ekler ve şimdilik yalnız
  `manager` rolüne bağlar (V1-IAM-029 aynı kodu Mutfak Şefi'ne de ekleyecek,
  additive). Migration olmadan uç hiçbir oturum için hiç açılamazdı — FK
  (`role_permissions.permission_id -> permissions.permission_id`) izin
  kodunun önce katalogda var olmasını zorunlu kılıyor; bu görevin ilk
  taslağında atlanmış gerçek bir kapsam boşluğuydu, uygulama sırasında
  bulunup buraya eklendi.
- database/MigrationComposition/order.json, src/Host/Composition/
  Migrations/MigrationManifest.cs, tests/Host/MigrationComposition/
  Manifest/ManifestTests.cs (Sınırlı ek, paylaşılan — V1-IAM-028 emsaliyle
  aynı desen) — yeni migration pozisyonu 110.
- tests/Host/Experience/KitchenOperations/KitchenOperationsHttpTests.cs
  (Sınırlı ek — V1-RMD-082 sahipliğinde kalan dosya) — yeni testler.

## In scope

1. Yeni Kitchen HTTP ucu, mevcut Kitchen cashier-session modeliyle
   korunur (Catalog'un manager-cookie akışına dokunulmaz); `Product.cs`,
   `CatalogManagementStore.cs` DEĞİŞMEZ, yalnız DI ile çağrılır.
2. Suspend işlemi, `CatalogManagementStore.SetProductAvailabilityAsync`'in
   mevcut row_version/optimistic-concurrency kuralını olduğu gibi kullanır
   (aynı üründe eşzamanlı iki 86-çağrısından biri 409 alır — yeni bir
   concurrency mekanizması icat edilmez).
3. "Plana aykırı" tespiti: bu görev kapsamında somut kural — ürünün
   `catalog.products`'ta zaten `IsAvailable = false` OLMAYAN, yani aktif
   satışta olan bir ürün 86'landığında varsayılan olarak "plana aykırı"
   sayılır (basit, yanlış-pozitifi kabul edilebilir bir ilk kural;
   gerçek stok tahminiyle karşılaştırma sonraki bir görev).
4. Plana aykırı durumda `authorization_grants`'a bir kayıt düşülür;
   aykırı değilse (ör. ürün zaten önceden pasifse — idempotent tekrar
   çağrı) yalnız suspend işlenir, grant oluşmaz.

## Out of scope

- "Mutfak Şefi" rolünün bu izni fiilen taşıması (`V1-IAM-029`).
- Frontend (`V1-KDS-002`).
- Gerçek envanter/tahmin verisiyle karşılaştırılan sofistike "plana
  aykırı" tespiti — bu görev basit bir ilk kural kullanır.

## Dependencies

- None

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata (doğrulandı).
- `dotnet test tests/Host/Experience/KitchenOperations` → gerçek Postgres'e
  karşı **18/18 yeşil** (4 yeni test: izin sınırı + plana-aykırı grant
  kaydı, idempotent tekrar çağrıda ikinci grant OLUŞMAMASI, ürün
  bulunamadı → 404).
  Eşzamanlılık notu (dürüst düzeltme): `SetProductAvailabilityAsync`
  kendi row_version'ını her çağrıda taze okuyup yazdığı için (client'tan
  bir "expected version" almıyor), gerçek 409'u HTTP seviyesinde
  deterministik olarak zorlamak mümkün değil — aynı sınırlama zaten
  `CatalogManagementHttpTests.cs`'in kendi `/availability` ucu için
  yazdığı yorumda da kayıtlı ("an HTTP-level Task.WhenAll race is not
  reliable here"). Bu görev o mekanizmayı DEĞİŞTİRMEDEN aynen yeniden
  kullanıyor; asıl kanıt zaten
  `tests/Modules/Catalog/ProductCatalog/PostgresRepositoryTests.cs::
  UpdateAsyncThrowsWhenTheProductWasConcurrentlyModified`'da var
  (row_version'ı SQL ile elle ileri alıp `InvalidOperationException`
  fırlatıldığını kanıtlıyor). Bu görevin katkısı o exception'ın
  `KitchenOperationsConcurrencyException` → 409 CONCURRENT_MODIFICATION'a
  doğru eşlendiğini göstermek (`catch (InvalidOperationException)` bloğu,
  kod incelemesiyle doğrulanabilir) — flaky bir HTTP testi icat etmek
  yerine bu sınırı olduğu gibi belgelemek tercih edildi.
- `dotnet test tests/Modules/Identity/Authorization/ALKAROS.Identity.
  Authorization.Tests.csproj` → **199/199 yeşil** (migration 110 bu
  projenin PermissionSplitDatabase zincirine dahil edilmedi — 054'ün
  identity.*.manage satırları gibi, ApplicationPermissions.Codes'un bir
  parçası değil, kasıtlı).
- `dotnet test tests/Host/MigrationComposition` → **135/135 yeşil**
  (ManifestTests migration 110'u kapsayacak şekilde güncellendi).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı (doğrulandı).
- `python tools/consistency-audit/consistency_audit.py` → `clean`
  (doğrulandı).
- Semih'in elle deneyebileceği senaryo: `kitchen.availability.suspend`
  izinli bir oturumla (bugün için: manager rolü — migration 110) aktif
  bir ürünü mutfak ucundan 86'la, Catalog Management ekranında ürünün
  gerçekten pasif göründüğünü ve `identity.authorization_grants`
  tablosunda `status='granted', policy_path='auto',
  reason_code='kitchen.availability.suspend.plan-conflict'` olan yeni bir
  kaydın oluştuğunu doğrula.

## Handoff

- None
