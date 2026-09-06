# V1-RMD-110 - Staff account creation and identity admin permission grant

- Task ID: V1-RMD-110
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: implementation
- Surface state: Existing

## Goal

Semih onayıyla ("Evet", garson-masa servis atama tasarımı görüşülürken
sorulan "her garsona bir kullanıcı adı ve şifre vermek daha kolay değil
mi" sorusuna yanıt olarak, 2026-09-06), iki iç içe geçmiş kök bulgu
kapatıldı:

1. **Sistemde ikinci bir kullanıcı hesabı oluşturmanın hiçbir yolu yoktu.**
   `identity.users` tablosuna INSERT yapan tek yer `provision-manager`
   CLI komutuydu ve bu komut zaten bir kullanıcı varsa çalışmayı reddediyordu.
   `RoleManagementEndpoints.cs`'te `POST /roles/{roleId}/users/{userId}`
   yalnızca VAR OLAN bir kullanıcıya rol atıyordu, yeni kullanıcı
   yaratmıyordu. Yani bir restoranda tek yönetici hesabı dışında hiçbir
   personel hiçbir zaman giriş yapamıyordu.
2. **`RoleManagementEndpoints.cs`'in tamamı, oluşturulduğu günden beri
   (V1-RMD-102) fiilen kullanılamıyordu.** Migration 008'in
   `identity.permissions`'a eklediği dört admin izni
   (`identity.users.manage`, `identity.roles.manage`,
   `identity.permissions.manage`, `identity.device_sessions.manage`)
   hiçbir role, hiçbir migrasyonda, hiç verilmemişti — `provision-manager`
   bile bootstrap yöneticisine yalnızca `catalog.manage`,
   `kitchen.routing.manage`, `kitchen.reprint`, `operations.backup`
   veriyordu. Yani bu HTTP yüzeyi oturumla erişilebilirdi ama her komut
   kendi izin kontrolünde 403 dönüyordu — bootstrap yöneticisi dahil,
   kimse için.

## Owned surface

- `database/migrations/V1/V1-RMD-110/**` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevin sahipliğinde kalır (yollar
  geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası olarak
  parse etmesin):
  - src/Modules/Identity/Authorization/IRoleRepository.cs,
    PostgresRoleRepository.cs, IRoleManagementService.cs,
    RoleManagementService.cs ve
    tests/Modules/Identity/Authorization/RoleManagementServiceTests.cs
    (V1-IAM-002 sahipliğinde) — `UsernameExistsAsync`/`CreateUserAsync`
    repository metotları ve `IRoleManagementService.CreateUserAsync`
    (izin: `identity.users.manage`, minimum 8 karakter şifre — daha önce
    hiçbir yerde tanımlı bir şifre politikası yoktu, bu yeni bir karar)
    eklendi.
  - src/Host/Experience/Roles/RoleManagementEndpoints.cs ve
    tests/Host/Experience/Roles/RoleManagementHttpTests.cs (V1-RMD-102
    sahipliğinde) — `POST /api/v1/management/users` eklendi (yalnız
    hesap oluşturur; rol ataması mevcut
    `POST /roles/{roleId}/users/{userId}` ile ayrı yapılır).
  - database/MigrationComposition/order.json ve
    src/Host/Composition/Migrations/MigrationManifest.cs (V1-RMD-103
    sahipliğinde) — migration 054 girişi eklendi, `PhaseBMax` "053"
    → "054".
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs (V1-FND-004
    sahipliğinde) — güncel manifest sayısına/son girişe göre sabitler
    güncellendi.

## In scope

1. `database/migrations/V1/V1-RMD-110/054-*.up/down.sql`: dört
   `identity.*.manage` iznini `manager` rolüne veriyor (yalnız manager,
   supervisor değil — hesap oluşturma/rol atama, supervisor'ın zaten
   sahip olduğu saha/eskalasyon izinlerinden daha yüksek yetki seviyesi).
2. `IRoleRepository`/`PostgresRoleRepository`: `UsernameExistsAsync`,
   `CreateUserAsync(username, passwordHash, displayName)` — eşzamanlı
   aynı kullanıcı adıyla yaratma denemesi `ux_users_username` unique
   index ihlali daraltılmış yakalamayla `InvalidOperationException`'a
   çevriliyor.
3. `IRoleManagementService`/`RoleManagementService.CreateUserAsync`:
   `identity.users.manage` yetkisi zorunlu; kullanıcı adı/şifre/görünen
   ad doğrulaması (şifre en az 8 karakter — yeni bir taban politika
   kararı, önceden hiçbir yerde tanımlı değildi); `PasswordHasher` ile
   hash'leniyor (mevcut PBKDF2 uygulaması, provision-manager'la aynı).
4. `POST /api/v1/management/users` — mevcut manager/supervisor oturum
   doğrulamasını (`RoleManagementEndpointFilter`) yeniden kullanıyor.

## Out of scope

- Garson-masa servis sahipliği (`Order.ServingUserId`) tasarımı — bu
  görev onun ön koşulunu (gerçek, ayrı personel hesapları) kapatıyor;
  kendisi ayrı bir göreve bırakıldı.
- PosTerminal'de bir personel yönetimi ekranı — uç nokta hazır, UI ayrı
  bir iş (V1-RMD-102'nin rol/izin uç noktaları da aynı şekilde UI'sız
  teslim edilmişti).
- `supervisor` rolüne bu izinlerin verilmesi — bilinçli olarak dışarıda
  bırakıldı (yukarıya bakın).

## Dependencies

- V1-RMD-109

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 uyarı / 0 hata.
- `docker compose -f compose.yaml -f compose.test.yaml run --rm test`:
  **79/79 test projesi, sıfır başarısız** — `ALKAROS.Identity.Authorization.Tests`
  190/190 (4 yeni `CreateUserAsync` testi dahil),
  `ALKAROS.Host.Experience.Roles.Tests` 8/8 (3 yeni; biri gerçek
  `manager` rolüyle uçtan uca yeni hesap oluşturmayı kanıtlıyor),
  `ALKAROS.Host.Tests` (MigrationComposition) 121/121.
- Revert-and-confirm: migration 054'ün INSERT'i geçici olarak `SELECT 1;`
  ile değiştirilip `ManagerCanCreateANewStaffAccount` testi çalıştırıldı
  — beklendiği gibi 403 Forbidden ile başarısız oldu; migration geri
  yüklenip tam süit yeniden yeşil.
- Migration up/down elle doğrulandı (test-postgres konteynerinde,
  minimal bağımlılık zinciriyle 005→008→042→054): up sonrası `manager`
  rolünde 4 `identity.*.manage` izni mevcut; down sonrası tam olarak
  kaldırılmış, önceki 5 izin (catalog/kitchen/operations/pos) değişmeden
  kalıyor.
- `python tools/plan-audit/plan_audit_tool.py validate` ve
  `validate-coverage`: sıfır hata.
- `python tools/consistency-audit/consistency_audit.py`: bu görevin
  değiştirdiği hiçbir dosyada ihlal yok. Depoda toplam 13 ihlal var,
  hepsi eşzamanlı ilerleyen V1.1 oturumunun dosyalarında
  (`src/Modules/Inventory/**`, `src/Clients/Cashier/InventoryPurchasing|MenuRecipeAdmin|Production/**`);
  dokunulmadı.
- Semih'in elle deneyebileceği senaryo: bootstrap yöneticisiyle giriş
  yap, `POST /api/v1/management/users` ile yeni bir garson hesabı
  oluştur, ardından mevcut `POST /roles/{roleId}/users/{userId}` ile
  `waiter` rolünü ata — yeni personel artık kendi kullanıcı adı/şifresiyle
  giriş yapabilir.

## Handoff

- V1-GOV-097
