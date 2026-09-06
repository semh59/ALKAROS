# V1-RMD-111 - Waiter-table serving ownership (garson-masa) and server hand-off

- Task ID: V1-RMD-111
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: implementation
- Surface state: Existing

## Goal

Garson-masa (hangi garson hangi siparişe bakıyor) tasarımının uygulanması
("Başla", 2026-09-06, önceki iki tur derinleştirme ve rakip araştırması
— Toast "Change Server"/"Edit Other Employees' Orders", Lightspeed
Restaurant "Table Ownership" iki-katmanlı izin modeli — ışığında). V1-RMD-
110'un kapattığı ön koşul (gerçek, ayrı personel hesapları) üzerine inşa
edilir.

Kapatılan kök bulgu: `AuthorizationGrantService.RequestAsync`'teki
own-check guard (yetki modeli §3, çözülmüş karar #1 — "bir garson yalnızca
kendi baktığı hesapta void/comp talep edebilir") baştan beri vardı ama
hiçbir zaman tetiklenmiyordu, çünkü hiçbir sipariş kimin baktığını
kaydetmiyordu — her HTTP çağıran `SubjectServingUserId: null` geçiyordu.
Guard'ın kendi unit testleri (`AuthorizationGrantServiceTests`) mantığı
zaten kapsıyordu; eksik olan uçtan uca bağlantıydı.

## Owned surface

- `database/migrations/V1/V1-RMD-111/**` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevin sahipliğinde kalır (yollar
  geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası olarak
  parse etmesin):
  - src/Modules/Orders/OrderAggregate/Order.cs, IOrderRepository.cs,
    PostgresOrderRepository.cs (V1-ORD-001 sahipliğinde) —
    ServingUserId alanı, ReassignServer(toUserId), serving_user_id
    kolonunun okuma/yazma bağlantısı ve ReassignServingUserAsync eklendi.
  - tests/Modules/Orders/OrderAggregate/**,
    tests/Modules/Orders/SubmitOrder/**,
    tests/Modules/Orders/ItemExceptions/** (ilgili görevler
    sahipliğinde) — yeni migration 056'nın csproj fixture listesine
    eklenmesi (şema bağımlılığı; test mantığı değişmedi).
  - src/Host/Experience/Orders/OrderManagementStore.cs,
    OrderManagementEndpoints.cs, OrderManagementContracts.cs (V1-ORD-005/
    V1-BIL-005/V1-IAM-027 sahipliğinde) — actingUserId parametresi yeni
    siparişleri ServingUserId ile damgalıyor, comp/void-sent uç noktaları
    artık gerçek order.ServingUserId'yi grant isteğine taşıyor, yeni
    `POST .../orders/transfer-server` uç noktası eklendi.
  - tests/Host/Experience/Orders/TableDraft/**, Comp/**, VoidSent/**,
    Void/** (ilgili görevler sahipliğinde) — own-check ve devir
    senaryoları için yeni testler/seed yardımcıları; Void ve diğer
    Postgres'e bağlı modül testlerinde (Kitchen/TicketLifecycle,
    Billing/BillFoundation, Billing/SplitDesign, Billing/Adjustments,
    Tables/TableTransfer, Tables/TableMerge) yalnızca migration 056'nın
    csproj fixture listesine eklenmesi (şema bağımlılığı).
  - database/MigrationComposition/order.json,
    src/Host/Composition/Migrations/MigrationManifest.cs (V1-RMD-103
    sahipliğinde) — migration 055/056 girişleri eklendi, `PhaseBMax`
    "054" → "056".
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs (V1-FND-004
    sahipliğinde) — güncel manifest sayısına/son girişe göre sabitler
    güncellendi.
  - src/Modules/Identity/Authorization/Catalog/ApplicationPermissions.cs
    ve tests/Modules/Identity/Authorization/Catalog/** (V1-IAM-017
    sahipliğinde) — `orders.transfer-server`/`orders.transfer-server-any`
    kodları eklendi; `PermissionSplitMigrationTests` migration 043'e
    sabit bir kod listesiyle ayrıştırıldı (büyüyen katalogdan bağımsız).

## In scope

1. `Order.ServingUserId` (nullable Guid): oluşturulduğunda bir kere
   set edilir, yalnızca yeni `Order.ReassignServer(toUserId)` ile
   değişir — başka hiçbir geçiş (AddItem/CancelItem/Submit/...) yan
   etki olarak değiştirmez.
2. `database/migrations/V1/V1-RMD-111/056-*.up/down.sql`:
   `orders.orders.serving_user_id UUID NULL` — bilinçli olarak
   `identity.users`'a FK YOK (bu tablonun kendi `changed_by` sütunuyla
   aynı emsal, V0-ARC-001 modül sınırı: Orders kendi şemasına sahip,
   Identity'ye sabit referans vermez; dar bir test fixture'ı
   identity şemasını hiç yüklemeden orders.orders'ı kullanabilir).
3. `database/migrations/V1/V1-RMD-111/055-*.up/down.sql`: iki-katmanlı
   izin — `orders.transfer-server` (kendi açık siparişini devretme, her
   rol) ve `orders.transfer-server-any` (herhangi birinin siparişini
   devretme, cashier/supervisor/manager) — Toast/Lightspeed emsaline göre.
4. `OrderManagementStore.CreateOrUpdateTableDraftAsync(request,
   actingUserId, ct)`: yeni siparişte `ServingUserId = actingUserId`;
   birleştirme dalında `currentOrder.ServingUserId` korunur (farklı bir
   terminalden gelen ikinci round sessizce sahipliği çalmaz).
5. `OrderManagementStore.TransferServingUserAsync(fromUserId, toUserId,
   ct)`: `fromUserId == toUserId` ve hedefin var/aktif olmadığı
   durumlarda `InvalidTransferTargetException` (400); aksi halde
   `IOrderRepository.ReassignServingUserAsync` ile fromUserId'ye ait
   TÜM bitmemiş siparişleri toplu taşır.
6. `POST .../orders/transfer-server`: `FromUserId == actingUserId` ise
   `orders.transfer-server`, değilse `orders.transfer-server-any`
   gerektirir.
7. `/comp` ve `/void-sent` uç noktaları artık `SubjectServingUserId:
   order.ServingUserId` taşıyor (önceden sabit `null`).

## Out of scope

- PosTerminal/WaiterPwa'da bir "devret" düğmesi — uç nokta hazır, UI
  ayrı bir iş (V1-RMD-102/110'un rol uç noktaları da aynı şekilde
  UI'sız teslim edilmişti).
- Sipariş/masa yerine tekil kalem (item) düzeyinde sahiplik — tasarım
  tartışmasında değerlendirildi, V1 kapsamı için sipariş düzeyi yeterli
  görüldü (bir hesaptaki tüm kalemler aynı garsona ait).
- V1.1 kapsamındaki hiçbir modül/dosya (Inventory/Menu/Production/
  Purchasing/PortionReservations/Reporting) — dokunulmadı.

## Dependencies

- V1-RMD-110

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 uyarı / 0 hata.
- `docker compose -f compose.yaml -f compose.test.yaml run --rm test`:
  **79/79 test projesi, sıfır başarısız** — `ALKAROS.Host.Tests`
  (MigrationComposition) 121/121, `ALKAROS.Host.Experience.Orders.Comp.Tests`
  9/9 (2 yeni own-check testi), `ALKAROS.Host.Experience.Orders.VoidSent.Tests`
  10/10 (2 yeni), `ALKAROS.Host.Experience.Orders.TableDraft.Tests` 10/10
  (6 yeni: atama-oluşturma, çapraz-terminal koruma, kendi-devir, sadece-
  kendi-devir-yetkisiyle-başkasınınkini-taşıyamama, cashier-herhangi-
  devir, geçersiz-hedef-reddi).
- `dotnet test tests/Modules/Identity/Authorization/...`: 194/194 (2 yeni
  izin kodu + migration 055 metin testleri dahil).
- Revert-and-confirm: `/comp` uç noktasında `SubjectServingUserId:
  order.ServingUserId` geçici olarak `null`'a döndürülüp
  `AWaiterCompingAnotherServersCheckIsRefusedByTheOwnCheckGuard` testi
  çalıştırıldı — beklendiği gibi 403 yerine 202 ile başarısız oldu; kod
  geri yüklenip tam süit yeniden yeşil.
- Migration 055/056 elle doğrulanmadı (054'ün aksine salt-katalog/şema
  eklemeleri, DB entegrasyon testleri — `PermissionSplitDatabaseTests`,
  `OrdersTransferServerPermissionMigrationTests`, ve tüm Postgres'e bağlı
  Orders/Billing/Kitchen/Tables test projeleri — zaten gerçek Postgres'e
  karşı up migrasyonunu uyguluyor ve doğruluğunu kanıtlıyor).
- `python tools/plan-audit/plan_audit_tool.py validate` ve
  `validate-coverage`: sıfır hata.
- `python tools/consistency-audit/consistency_audit.py`: bu görevin
  değiştirdiği hiçbir dosyada ihlal yok.
- Semih'in elle deneyebileceği senaryo: iki garson hesabı oluştur
  (V1-RMD-110), her biriyle farklı masalara sipariş aç, birinin
  hesabında void/comp dene — kendi hesabında normal akış, diğerininkinde
  403; ardından `transfer-server` ile hesabı diğer garsona devret ve
  aynı denemeyi tekrarla — artık devredilen garson için normal akış.

## Handoff

- V1-GOV-099
