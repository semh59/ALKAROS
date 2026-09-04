# V1-IAM-017 - Permission catalog split and waiter role

- Task ID: V1-IAM-017
- Status: Done
- Assignee: claude-session-01XKRazppo9sW452rdbCZsgy
- Work type: implementation
- Surface state: Existing

## Goal

`docs/domain/authorization-model.md` ikinci ve üçüncü bölüm uyarınca
`pos.cashier.mutate` iznini granüler kodlara böler, `waiter` ile `supervisor`
rollerini ekler ve rol ile izin eşleşme tohumunu yeniden kurar. Eski izin yalnız
geçiş takma adı olarak kalır; kaldırma işi `V1-IAM-024` görevine aittir.

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-017-permission-catalog-split-and-waiter-role.md`
- `database/migrations/V1/V1-IAM-017/**`
- `evidence/V1-IAM-017/**`
- Yüzey devri (çıkış): src/Modules/Identity/Authorization/Catalog/** ve tests/Modules/Identity/Authorization/Catalog/** yüzeyleri, pos.cashier.mutate takma adının ve sabitinin kaldırılması için V1-IAM-024'e devredildi (PO:2026-09-05); bu historical task closed kalır.
- Yüzey devri (giriş): database/MigrationComposition/order.json ve tests/Host/MigrationComposition/Manifest/ManifestTests.cs, migration 043 için V1-RMD-089'dan bu göreve devredildi (PO:2026-09-04); ardından migration 044 için V1-IAM-018'e devredildi (PO:2026-09-04).
- src/Host/Composition/Migrations/MigrationManifest.cs içindeki PhaseBMax sabiti V1-FND-004 sahipliğinde kalır; bu görevde yalnızca faz üst sınırı 043 değerine güncellendi (V1-RMD-089/9. dalga deseni).
- `AuthorizationService` davranışı değişmez; izin kodu yalnız string olarak akar.
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## In scope

- Yeni `identity.permissions` satırları: `orders.create`, `orders.send`,
  `tables.status`, `tables.reserve`, `tables.transfer`, `tables.merge`,
  `floorplan.manage`, `bills.split`, `bills.void`, `bills.comp`,
  `bills.discount`, `cash.drawer`, `reports.view`.
- Yeni `identity.roles` olarak `waiter` ile `supervisor`; `role_permissions`
  tohumu karar dokümanının üçüncü bölümündeki matrise göre yeniden kurulur.
- Eski `pos.cashier.mutate` iznini yeni kodların birleşimine eşitleyen geçiş
  takma adı; her iki kod da aynı endpoint sonucunu üretir.
- İleri ile geri migration; geri alma tohumu `V1-RMD-097` durumuna döner.

## Out of scope

- Politika motoru, yetki isteği akışı ile endpoint yeniden eşleme; bunlar sonraki
  görevlere aittir.
- Eski izin kodunun tamamen kaldırılması; bu iş `V1-IAM-024` içindedir.

## Dependencies

- V1-IAM-016
- V1-IAM-002

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release --no-restore`: 0 uyarı / 0 hata.
- `dotnet test` (yerel Postgres 18, `alkaros-test-pg`):
  `ALKAROS.Identity.Authorization.Tests` 49/49 (31'i yeni `Catalog/**` —
  `ApplicationPermissionsTests`, `PermissionSplitMigrationTests` metin
  seviyesi, `PermissionSplitDatabaseTests` gerçek 005→008→042→043 zinciriyle);
  `ALKAROS.Host.Tests` `Manifest.ManifestTests` 16/16 (`PhaseBMax` 043,
  pozisyon sayısı 42).
- Migration ileri: tüm 001..043 zinciri boş `alkaros_fullmig` veritabanına
  sırayla uygulandı (`docker exec alkaros-test-pg psql -f`), `waiter` = tam 3
  izin (`orders.create`, `orders.send`, `tables.status`), `pos.cashier.mutate`
  içermez. Geri: `043-*.down.sql` uygulandı; 13 kod ve `waiter` rolü kalktı,
  migration 042 satırları (`pos.cashier.mutate`, `catalog.manage`,
  `kitchen.reprint`) yerinde kaldı.
- Semih için gerçek senaryo: `waiter` rolünde bir kullanıcı oluşturulur, kasa
  terminaline girer ve salon planında rezervasyon eyleminin görünmediğini
  (`allowedCommands` boş), `cashier` rolünde ise göründüğünü doğrular.

## Handoff

- V1-IAM-018
- V1-IAM-024
