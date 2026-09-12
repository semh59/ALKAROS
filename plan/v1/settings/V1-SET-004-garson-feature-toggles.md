# V1-SET-004 - Garson modülü opsiyonel özellik anahtarları

- Task ID: V1-SET-004
- Status: Done
- Assignee: claude-session-0146mk6QkE2Ck7HvT8TArd5a
- Work type: implementation
- Surface state: Existing

## Goal

Semih (2026-09-12): "ben işletmelere özellikleri komple kapatmak istiyorum" —
ALKAROS tek-kiracılı (her işletme kendi Host sürecinde, kurulumda paylaşılan
bir `business_id` tablosu yok, doğrulandı), yani "bir işletme için bir
özelliği kapat" burada "bu Host için kapat" anlamına geliyor. Kapsam
onaylandı (AskUserQuestion): Garson modülündeki **tüm** opsiyonel özellikler
— sadece 2-3 örnek değil. Dokuz özellik, tek bir `GarsonFeatureToggles`
paylaşılan yardımcı sınıfı üzerinden, `V1-SET-002`/`V1-SET-003`'ün
`TypedSettings` desenini izleyerek (kayıtlı ilk soruda self-registering,
`SettingScope.Global`, `SettingDataType.Toggle`) — ama dokuz ayrı dosya
yerine bir `enum GarsonFeature` + bir paylaşılan yardımcı.

Bilinçli olarak **opt-OUT**, `V1-SET-002/003`'ün opt-IN'inin tersi: bu dokuz
özellik zaten her kurulumda aktif olarak sevk ediliyor, varsayılan `true`
(bu görevden önceki davranışın birebir aynısı) — bir işletme bir özelliği
istemiyorsa kurulumda bir operatör bunu bir kez elle kapatır.

## Owned surface

- `plan/v1/settings/V1-SET-004-garson-feature-toggles.md`
- `src/Modules/Settings/GarsonFeatureToggles/**` (yeni)

Paylaşılan dosyalarda sınırlı ek (mevcut sahiplikte kalır, bu görev sadece
kod ekliyor — dosya adları bilinçli olarak backtick'siz, aksi halde plan
denetimi bunları bu görevin münhasır sahipliği olarak okur):

- src/Host/DualScreen/DualScreenApplication.Endpoints.cs — runtime-configuration
  yanıtına dokuz alanlı garsonFeatures nesnesi eklendi.
- src/Host/Experience/Orders/OrderManagementEndpoints.cs — TableDraftService/
  OrderSubmissionCoordinator/ShiftSummaryStore'a ISettingsService DI'sı,
  GarsonFeatureDisabledException -> 403 eşlemesi, ve
  PersonalCompBudgetEscalationResolver'ı GarsonFeatureGatedEscalationResolver
  ile sarmalayan (yeni, bu dosyada tanımlı) bir kayıt.
- src/Host/Experience/Orders/TableDraft/TableDraftService.cs — kurs/koltuk/
  kuver alanları kapalıyken hoşgörülü biçimde yok sayılıyor.
- src/Host/Experience/Orders/OrderSubmissionCoordinator.cs — fire-course
  kapalıyken outright reddediliyor.
- src/Host/Experience/Orders/ShiftSummaryStore.cs — kapalıyken outright reddediliyor.
- src/Host/Experience/Orders/ServingHandoffNoteStore.cs — kapalıyken
  hoşgörülü biçimde yok sayılıyor.
- src/Host/Experience/HelpRequests/HelpRequestStore.cs,
  HelpRequestExperience.cs — kapalıyken outright reddediliyor (403).
- src/Host/Experience/QrOrdering/QrOrderingEndpoints.cs — /bill kapalıyken
  boş/henüz sipariş yok durumunu döndürüyor.
- src/Host/Experience/Billing/BillingSplitStore.cs,
  BillingSplitApplication.cs — /tip kapalıyken outright reddediliyor (403).
- src/Clients/WaiterPwa/wwwroot/waiter-app.js — loadFeatures()/
  featureEnabled(), koltuk/kurs/kuver seçicileri, yardım butonu, devir notu
  alanı ve vardiya özeti menü öğesi ilgili anahtar kapalıyken gizleniyor.
- tests/Host/MigrationComposition/DualScreen/DualScreenHostTests.cs —
  RuntimeConfigurationRequiresTheBoundTerminalAndReturnsTheKitchenStationAndReservationStationFlag
  yeni garsonFeatures alanını da doğruluyor (mevcut sahiplikte kalır).
- Beş test projesinin .csproj dosyasına 026-typed-settings.up.sql bağlantısı
  eklendi (izole test veritabanı artık settings.settings şemasını kuruyor):
  tests/Host/Experience/Orders/TableDraft/**.csproj,
  tests/Host/Experience/HelpRequests/**.csproj,
  tests/Host/Experience/QrOrdering/**.csproj (ayrıca 005-users.up.sql —
  setting_history.changed_by'nin identity.users'a referansı için),
  tests/Host/Experience/Orders/Comp/**.csproj,
  tests/Host/Experience/Orders/VoidSent/**.csproj.

## In scope

- `enum GarsonFeature` (9 değer) + `GarsonFeatureToggles` (`KeyFor`,
  `EnsureAllRegisteredAsync`, `IsEnabledAsync`, `GetAllAsync`) +
  `GarsonFeatureDisabledException`.
- Eşzamanlı ilk-kayıt yarışına karşı process-genelinde bir `SemaphoreSlim`
  (bkz. Errors/fixes) — `EnsureAllRegisteredAsync`'in kendisinde.
- Üç alan (kurs, koltuk, kuver) hoşgörülü biçimde yok sayılıyor; üç aksiyon
  (fire-course, yardım çağrısı, bahşiş, vardiya özeti) outright reddediliyor
  — hangi davranışın seçildiği her dosyadaki yorum satırında gerekçelendirildi.
- `PersonalCompBudgetEscalationResolver` (Identity modülü) Settings'e asla
  bağımlı olmuyor — modül sınırını korumak için Host katmanında
  `GarsonFeatureGatedEscalationResolver` adlı bir dekoratör eklendi.
- WaiterPwa: `/runtime-configuration`'ın `garsonFeatures` alanı
  `state.features`'a yükleniyor, `featureEnabled(name)` ile okunuyor.

## Out of scope

- `GuestLiveBill` için bir istemci — bu repoda ayrı bir CustomerWeb istemcisi
  yok (QR misafir sayfası bu reponun dışında barındırılıyor); sadece
  sunucu tarafı kapatıldı.
- `VoluntaryTip` için bir istemci arayüzü — `ApplyTipAsync`'in hiçbir
  istemcide (PosTerminal dahil) bir çağrı noktası yok, bu görevden önce de
  yoktu; sadece sunucu tarafı kapatıldı.
- Anahtarları değiştirecek bir yönetici ekranı — genel Settings API'si
  üzerinden mevcut (V1-SET-003'ün aynı kararı).
- `docs/engineering/garson-refactor-plan.md`'nin 2. bölümü (`waiter-app.js`'in
  ES modüllerine bölünmesi) — Semih'in "Devam edelim" dediği ayrı iş, bu
  görevden bağımsız, henüz başlanmadı.
- Pre-existing, bu görevden bağımsız bir E2E-flake sınıfı bug: bkz.
  Errors/fixes — bu görev sırasında bulundu ve düzeltildi ama V1-SET-004'ün
  konusu değildi.

## Dependencies

- V1-SET-001
- V1-WTR-025
- V1-WTR-022
- V1-WTR-012
- V1-WTR-013
- V1-WTR-014
- V1-WTR-018
- V1-WTR-020
- V1-WTR-021
- V1-WTR-015

## Acceptance evidence

Bu oturumda bulunan ve düzeltilen sorunlar (kanıtın parçası):

- `TableDraftService`/`OrderSubmissionCoordinator`'a `ISettingsService`
  eklenince beş izole test projesi `settings.settings` şeması hiç
  kurulmadığı için "relation does not exist" ile kırılacaktı — beşi de
  `.csproj`'larına `026-typed-settings.up.sql` (ve gerekirse `005-users.up.sql`)
  bağlantısı eklenerek düzeltildi, her biri gerçekten çalıştırılıp
  doğrulandı (aşağıya bkz).
- `GarsonFeatureToggles.EnsureAllRegisteredAsync`: `PostgresSettingsRepository
  .RegisterSettingAsync` atomik bir upsert değil (SELECT sonra INSERT) —
  aynı anahtarı ilk kez kaydetmeye çalışan iki eşzamanlı istek (bu
  deployment'ın ilk yardım çağrısı/masa taslağı gibi) kontrolü ikisi de
  geçip kaybedenin INSERT'i ham bir Postgres unique-violation'ıyla
  patlayabiliyordu — repository'nin kendi `DuplicateSettingKeyException`'ı
  değil, dolayısıyla yakalanmıyordu. Bu, `tests/Host/Experience/Orders
  /TableDraft`'taki `ConcurrentIdenticalFirstSubmissionsForATableResolve
  ToTheSameOrder` testinde 409 olarak ve `tests/Host/Experience/HelpRequests`
  'teki `TwoGenuinelyConcurrentRequestsForTheSameTableProduceExactlyOneWinner`
  testinde 0 kazanan olarak ortaya çıktı. Process-genelinde bir
  `SemaphoreSlim` ile `EnsureAllRegisteredAsync`'in tamamı serileştirilerek
  düzeltildi; her iki test de sonrasında yeşil.
- Bu görevden bağımsız, pre-existing bir bug bulundu ve düzeltildi:
  `tests/Host/Experience/Orders/TableDraft`'ın kendi `.csproj`'unda
  `076-orders-table-submission-idempotency-index.up.sql` (V1-RMD-123'ün
  `ux_orders_table_submission` unique index'i) hiç bağlanmamıştı — izole
  test veritabanında bu index gerçekte hiç var olmadığı için
  `ConcurrentIdenticalFirstSubmissionsForATableResolveToTheSameOrder`
  testi (bu görevden tamamen bağımsız olarak) 3/3 çalıştırmada başarısız
  oluyordu; `git stash` ile bu oturumun tüm değişiklikleri geri alınıp
  aynı test aynı şekilde 3/3 başarısız olarak doğrulandı — V1-SET-004'ün
  bir regresyonu değil. Migration bağlantısı eklenerek düzeltildi.

- `dotnet build src/Host/ALKAROS.Host.csproj -c Debug`: 0 uyarı / 0 hata
  (her ara adımda tekrar tekrar doğrulandı).
- `dotnet test` (yerel Postgres 18, `alkaros-test-pg`, port 55432):
  `ALKAROS.Host.Experience.Orders.TableDraft.Tests` 66/66,
  `ALKAROS.Host.Experience.HelpRequests.Tests` 7/7,
  `ALKAROS.Host.Experience.QrOrdering.Tests` 23/23,
  `ALKAROS.Host.Experience.Billing.Tests` 18/18,
  `ALKAROS.Host.Experience.Orders.Comp.Tests` 14/14,
  `ALKAROS.Host.Experience.Orders.VoidSent.Tests` 14/14,
  `ALKAROS.Host.Experience.Orders.Confirmation.Tests` 19/19,
  `ALKAROS.Host.Experience.Orders.Void.Tests` 5/5,
  `ALKAROS.Host.Tests` (tam `MigrationComposition` paketi) 135/135.
- `node --check src/Clients/WaiterPwa/wwwroot/waiter-app.js`: sözdizimsel
  olarak geçerli.
- `python tools/consistency-audit/consistency_audit.py`: bu göreve ait
  dosyalarda temiz (kalan tek ihlal, `InventoryAdjustmentService.cs:96`,
  bu görevden tamamen bağımsız, pre-existing, dokunulmadı).
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata / 0 uyarı.
- `npx playwright test` (`tests/E2E/WaiterPwa`, gerçek Chromium + gerçek
  Postgres + gerçek Host ikili dosyası, mock yok): **4/4 çalıştırmada
  18/18 temiz** (~43s/çalıştırma) — `waiter-app.js`'teki görünürlük/gizleme
  değişikliklerinin gerçek tarayıcıda hiçbir regresyona yol açmadığı
  doğrulandı.

## Handoff

- None
