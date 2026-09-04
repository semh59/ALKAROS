# V1-IAM-024 - Endpoint re-pointing and alias removal

- Task ID: V1-IAM-024
- Status: Done
- Assignee: claude-session-01XKRazppo9sW452rdbCZsgy
- Work type: implementation
- Surface state: Existing

## Goal

`RequireMutationAsync` ile tek `MutationPermission` çağıran her Experience
endpoint'ini kendi granüler koduna bağlar (Tables, Orders, Billing, Cash);
`AllowedCommands` sonucunu, çağıranın sahip olduğu izinler ile ulaşılabilir
yetki isteği eylemlerinin birleşimi olarak hesaplar; böylece POS istemcisi bir
eylemi gizlemek yerine onay gerektiren biçimde gösterir. Eski `pos.cashier.mutate`
takma adını ve kodunu kaldırır.

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-024-endpoint-repointing-and-alias-removal.md`
- `database/migrations/V1/V1-IAM-024/**`
- `src/Host/Experience/Tables/TableManagementApplication.cs`
- `src/Host/Experience/Tables/TableManagementContracts.cs`
- `src/Host/DualScreen/DualScreenApplication.cs`
- `src/Host/DualScreen/DualScreenApplication.Endpoints.cs`
- `src/Host/Experience/Orders/OrderManagementEndpoints.cs`
- `src/Host/Experience/Billing/BillingSplitApplication.cs`
- `src/Modules/Identity/Authorization/Catalog/**`
- `tests/Modules/Identity/Authorization/Catalog/**`
- `database/MigrationComposition/order.json`
- `tests/Host/MigrationComposition/Manifest/ManifestTests.cs`
- `evidence/V1-IAM-024/**`
- Yüzey devri (giriş): src/Host/Experience/Tables/TableManagementApplication.cs ve src/Host/Experience/Tables/TableManagementContracts.cs V1-RMD-026'dan; src/Host/DualScreen/DualScreenApplication.cs V1-RMD-098'den; src/Host/DualScreen/DualScreenApplication.Endpoints.cs V1-RMD-097'den; src/Host/Experience/Orders/OrderManagementEndpoints.cs V1-RMD-066'dan; src/Host/Experience/Billing/BillingSplitApplication.cs V1-RMD-054'ten; src/Modules/Identity/Authorization/Catalog/** ile tests/Modules/Identity/Authorization/Catalog/** V1-IAM-017'den; database/MigrationComposition/order.json ile tests/Host/MigrationComposition/Manifest/ManifestTests.cs V1-IAM-023'ten bu göreve devredildi (PO:2026-09-05).
- src/Host/Experience/KitchenOperations/KitchenOperationsEndpoints.cs V1-RMD-082 sahipliğinde kalır; bu görevde yalnızca TicketMutationPermission sabiti granüler koda (orders.send) eşlenir (V1-RMD-089/MigrationManifest.cs deseni).
- src/Host/Program.cs V1-RMD-094 sahipliğinde kalır; bu görevde yalnızca ManagerPermissions listesinden pos.cashier.mutate satırı çıkarılır.
- src/Host/Composition/Migrations/MigrationManifest.cs içindeki PhaseBMax sabiti V1-FND-004 sahipliğinde kalır; bu görevde yalnızca faz üst sınırı alias-kaldırma migration'ının numarasına güncellenir (V1-RMD-089/9. dalga deseni).
- src/Clients/PosTerminal/src/features/tables/** V1-RMD-071 sahipliğinde kalır; sunucu artık süzülmüş AllowedCommands gönderdiği için istemci süzgeci değişmez.
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## In scope

- Endpoint başına granüler izin kodu eşlemesi (kilitli kararlar,
  `docs/engineering/authz-wave-remediation-plan.md`): Tables zones/floor-plan/
  table create-edit -> `floorplan.manage`, status -> `tables.status`,
  reservations -> `tables.reserve`, transfers -> `tables.transfer`, merges ->
  `tables.merge`; Billing split -> `bills.split`; DualScreen order create/item
  -> `orders.create`, submit -> `orders.send`; Kitchen ticket transition ->
  `orders.send`; Orders Experience modülüne `orders.create`/`orders.send`
  kontrolü eklenir (bugün yalnız oturum); customer-display pairing ile
  display-session revoke yalnız oturuma (`RequireCashierAsync`) düşürülür; Cash
  Experience endpoint'i yoktur (no-op).
- `DualScreenApplication.cs` `CashierMutationPermission` sabiti,
  `ApplicationPermissions.PosCashierMutateAlias` ile `RolesWithMutateAlias`
  sabitleri, `Program.cs` ManagerPermissions satırı ve `pos.cashier.mutate`
  izin satırı (alias-kaldırma migration'ı) kaldırılır.
- `AllowedCommands`: `TableManagementPrincipal(Guid, bool CanMutate)` ->
  `(Guid, IReadOnlySet<string> Permissions)`; `TableContractMapper.AllowedCommands`
  her komutu tutulan izne göre süzer (Tables komutlarının hiçbiri grant-erişilebilir
  değil, model §3).

## Out of scope

- Politika, yetki isteği, devir, çevrimdışı bütçe ile davranışsal sıkılaştırma
  davranışı; bunlar `V1-IAM-018` ile `V1-IAM-023` arasındadır.
- Yeni iş davranışı; yalnız izin kodu eşleme ile takma ad kaldırma yapılır.

## Dependencies

- V1-IAM-017
- V1-IAM-018
- V1-IAM-019
- V1-IAM-020
- V1-IAM-021
- V1-IAM-022
- V1-IAM-023

## Acceptance evidence

- İki commit ile teslim edildi: (A) her Experience endpoint'ini granüler koda
  bağlar, alias grant'i yerinde bırakır; (B) migration 049 ile alias'ı düşürür.
  Master her iki commit sonrasında da tutarlı ve güvenli kalır.
- `dotnet build ALKAROS.slnx -c Release`: 0 uyarı / 0 hata (her iki commit).
- `dotnet test` (yerel Postgres 18):
  - `ALKAROS.Identity.Authorization.Tests` 179/179 — `Catalog/**` split
    testleri artık 005->008->042->043->049 zincirini uygular ve alias'ın
    cashier/supervisor/manager rollerinden düştüğünü, 13 granüler kodun ve
    `catalog.manage`'in kaldığını, 049-down + 043-down'un alias'ı geri getirip
    granüler seti düşürdüğünü doğrular. Silinen tek test alias için ayrı bir
    `RolesWithMutateAlias` iddiasıydı (sabit kaldırıldı).
  - `ALKAROS.Host.Experience.Tables.Tests` 8/8 — mutasyon seed'i 5 granüler
    masa kodunu verir, ret testi `denial_events.permission_code =
    'floorplan.manage'` bekler.
  - `ALKAROS.Host.Experience.Billing.Tests` 3/3 (Release) — seed `bills.split`
    verir.
  - `ALKAROS.Host.Experience.Orders.Tests` geçer (store düzeyi, HTTP yok).
  - `ALKAROS.Host.Tests` `Manifest.ManifestTests` 16/16 — `PhaseBMax` 049,
    48 pozisyon, son giriş tabloları `["permissions","role_permissions"]`;
    `HostConstructabilityTests` 4/4 — `AddOrderManagementExperience` içindeki
    yeni `IAuthorizationService` kaydıyla DI grafiği doğrulanır.
- Migration: 001..049 ileri boş veritabanına uygulandı; alias satırı 0, granüler
  10 satır, rol grant sayıları waiter 3 / cashier 8 / supervisor 14 / manager 17.
  049-down alias'ı yeniden yaratıp cashier/supervisor/manager'a bağlar (waiter'a
  değil); 049 re-up alias'ı yeniden düşürür.
- `pos.cashier.mutate` dizgisi: aktif çalışma zamanı `.cs` kontrolü yok, gran't
  grant veren test seed'i yok. Kalan atıflar tarihsel yorum (`ApplicationPermissions`
  sınıf dokümanı, `Program.cs`), 042/043 migration metni (değişmez) ve 049
  migration metni (kaldırma + down geri alma).
- Gate'ler: `plan-audit validate` / `verify-manifest` / `validate-coverage` 0
  hata; `project-manifest` VALID. `AUDIT_MANIFEST.json` + `AUDIT_REPORT.md`
  yeniden üretildi; `GATE-V1-EXIT` reseal ayrı bir governance görevine bırakılır.
- Ortam istisnaları: Kitchen HTTP ve Experience Composition testleri yerelde
  koşmadı — WDAC (`FileLoadException 0x800711C7`) yeni derlenen modül DLL'lerini
  engelliyor (G1). Kitchen değişikliği sabit değeri takasıdır (`pos.cashier.mutate`
  -> `orders.send`) + eşleşen ret iddiası; Composition, `HostConstructabilityTests`
  ile kapsanır. `MigrationExecutionTests` yerelde `psql` PATH'te olmadığı için
  atlanır (CI Postgres bacağı koşar).
- Semih için gerçek senaryo: `waiter` rolünde giriş yapılır ve salon planında
  void düğmesi onay gerektiren etiketiyle görünür ama doğrudan çalışmaz;
  `cashier` rolünde rezervasyon yapılabilir; `supervisor` rolünde void ile ikram
  doğrudan çalışır.

## Handoff

- V14-QRO-003
