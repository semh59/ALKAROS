# V1-KIT-008 - Mutfak ekranından ürün tükendi bildirimi (86'lama köprüsü)

- Task ID: V1-KIT-008
- Status: Planned
- Assignee: Unassigned
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
  `CatalogManagementStore` DI enjeksiyonu.
- authorization_grants yazma yolu (Sınırlı ek, paylaşılan — mevcut
  grant-akışını yöneten dosya(lar), `docs/domain/authorization-model.md`
  §4'te tarif edilen mekanizma) — yeni bir grant türü/`policy_path` (ör.
  `kitchen.availability.suspend.plan-conflict`).
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

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `dotnet test` → yeni testler dahil ilgili projeler yeşil, gerçek
  Postgres'e karşı; en az bir test aynı ürünü eşzamanlı iki çağrının
  birini 409 ile reddettiğini kanıtlar.
- Semih'in elle deneyebileceği senaryo: `kitchen.availability.suspend`
  izinli bir oturumla aktif bir ürünü mutfak ucundan 86'la, Catalog
  Management ekranında ürünün gerçekten pasif göründüğünü ve
  `authorization_grants` tablosunda yeni bir kaydın oluştuğunu doğrula.

## Handoff

- None
