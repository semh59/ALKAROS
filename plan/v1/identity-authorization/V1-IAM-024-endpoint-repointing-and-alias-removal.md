# V1-IAM-024 - Endpoint re-pointing and alias removal

- Task ID: V1-IAM-024
- Status: Planned
- Assignee: claude-session-01XKRazppo9sW452rdbCZsgy
- Work type: implementation
- Surface state: Planned

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

- `dotnet build ALKAROS.slnx -c Release` sıfır uyarı ile sıfır hata; tam
  `dotnet test` paketi geçer; `pos.cashier.mutate` dizgisi `src` ile `tests`
  altında kalmaz.
- Migration ileri ile geri yönde boş veritabanında denenir.
- Semih için gerçek senaryo: `waiter` rolünde giriş yapılır ve salon planında
  void düğmesi onay gerektiren etiketiyle görünür ama doğrudan çalışmaz;
  `cashier` rolünde rezervasyon yapılabilir; `supervisor` rolünde void ile ikram
  doğrudan çalışır.

## Handoff

- V14-QRO-003
