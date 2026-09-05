# V1-RMD-102 - Independent audit: remaining High/Medium/Low defects fixed

- Task ID: V1-RMD-102
- Status: Done
- Assignee: claude-session-01Dhks7X2RG1fxScJpZRzZiL
- Work type: implementation
- Surface state: Existing

## Goal

Semih onayıyla ("Hepsi", 2026-09-05), `V1-RMD-101`'in kapsam dışı bıraktığı
2026-09-05 4-ajan bağımsız denetiminin geri kalan High/Medium/Low
bulgularının tamamı giderildi. Her bulgu için gerçek bir regresyon testi
eklendi; mümkün olan her yerde düzeltme geçici olarak geri alınıp testin
GERÇEKTEN kırıldığı doğrulandıktan sonra düzeltme geri getirildi (Bulgu 2 ve
Bulgu 6).

## Owned surface

- `plan/v1/remediation/V1-RMD-102-independent-audit-remaining-findings.md`
- `src/Host/Experience/Roles/**` (yeni)
- `tests/Host/Experience/Roles/**` (yeni proje)
- Paylaşılan dosyalarda sınırlı ek (V1-RMD-089/9. dalga deseni — sahiplik
  ilgili görevde kalır):
  `src/Modules/Billing/BillFoundation/PostgresBillRepository.cs` (`V1-RMD-002`
  sahipliğinde kalır) — `UpdateBillItemAsync`'in WHERE fıkrasına
  `row_version` kontrolü eklendi (Bulgu 1); mevcut hiçbir sorgu değişmedi.
  `tests/Modules/Billing/BillFoundation/PostgresBillTests.cs` (`V1-RMD-002`
  sahipliğinde kalır) — yeni bir regresyon testi eklendi.
  `src/Host/Experience/Catalog/CatalogManagementEndpoints.cs` (`V1-RMD-076`
  sahipliğinde kalır) — `AddCatalogManagement()`'a
  `IRoleRepository`/`IDenialEventSink`/`IAuthorizationService` kaydı eklendi
  (Bulgu 2); mevcut endpoint'ler değişmedi.
  `tests/Host/Experience/Catalog/CatalogManagementHttpTests.cs` (`V1-RMD-076`
  sahipliğinde kalır) — `InitializeAsync()`'teki 3 satırlık elle-kayıt
  geçici çözümü kaldırıldı, artık yalnız `AddCatalogManagement()` çağrılıyor.
  `src/Host/DualScreen/DualScreenApplication.cs` (`V1-IAM-024` sahipliğinde
  kalır) — `AddRoleManagementExperience()`/`MapRoleManagementApi()` çağrıları
  diğer Experience kayıtlarının yanına eklendi (Bulgu 3); mevcut hiçbir kayıt
  değişmedi.
  `ALKAROS.slnx` (`V1-RMD-036` sahipliğinde kalır) — yeni
  `ALKAROS.Host.Experience.Roles.Tests.csproj` girişi eklendi.
  `src/Clients/Cashier/wwwroot/cashier-app.js` (`V1-RMD-101` sahipliğinde
  kalır) — `dispatchOrderToKitchen`'a çift-tıklama/yeniden-giriş koruması
  eklendi (Bulgu 4); sepet/gönderim mantığının geri kalanı değişmedi.
  `tests/Clients/Cashier/Frontend/test_cashier_frontend.py` (`V1-RMD-101`
  sahipliğinde kalır) — yeni assertion'lar eklendi.
  `src/Clients/WaiterPwa/wwwroot/waiter-app.js` (`V1-RMD-083` sahipliğinde
  kalır) — `btnSendKitchen` click handler'ına aynı koruma eklendi (Bulgu 4).
  `tests/Clients/WaiterPwa/Frontend/test_waiter_pwa_frontend.py` (`V1-RMD-051`
  sahipliğinde kalır) — yeni assertion'lar eklendi.
  `src/Modules/Orders/OrderAggregate/Order.cs` (`V1-KIT-005` sahipliğinde
  kalır) — `CancelItem` artık `RebuildWith`'e geçirilen yeni bir `history`
  parametresiyle bir `OrderStatusHistoryEntry` ekliyor (Bulgu 5); item
  cancel'in kendi mantığı değişmedi.
  `tests/Modules/Orders/OrderAggregate/OrderDomainTests.cs` (`V1-KIT-005`
  sahipliğinde kalır) — yeni bir regresyon testi eklendi.
  `src/Modules/Kitchen/TicketLifecycle/PostgresKitchenTicketRepository.cs`
  (`V1-RMD-074` sahipliğinde kalır) — item `ON CONFLICT DO UPDATE`'ine
  `WHERE status IS DISTINCT FROM EXCLUDED.status` eklendi (Bulgu 6); ticket
  seviyesi sorgu değişmedi.
  `tests/Modules/Kitchen/TicketLifecycle/KitchenTicketTests.cs` (`V1-RMD-074`
  sahipliğinde kalır) — yeni bir regresyon testi eklendi.
  `tests/Host/Experience/KitchenOperations/KitchenOperationsHttpTests.cs`
  (`V1-RMD-082` sahipliğinde kalır) — Bulgu 6'nın düzeltmesiyle artık geçersiz
  olan sayısal `RowVersion` beklentileri (dokunulmamış kalemin sürümünün
  sabit kaldığını yansıtacak şekilde) güncellendi; testin kendi amacı
  (yetkili sürümlerin kullanılması, bayat yazmaların reddi) değişmedi.
  `database/MigrationComposition/order.json` (`V1-IAM-025` sahipliğinde
  kalır) — `phaseBRange.max` `049`'dan gerçek değer `052`'ye düzeltildi
  (Bulgu 7); migration listesi değişmedi.
  `src/Host/Composition/Migrations/MigrationManifest.cs` (`V1-FND-004`
  sahipliğinde kalır, yalnız doc-comment) — sınıf üstü yorumdaki aynı stale
  "031-049" aralığı "031-052" olarak düzeltildi; `PhaseBMax` sabiti zaten
  doğruydu (`052`), hiç değişmedi.
  `src/Clients/PosTerminal/src/features/catalog/CatalogWorkspace.tsx`
  (`V1-RMD-076` sahipliğinde kalır) — kullanılmayan `CatalogCategory` type
  import'u kaldırıldı (Bulgu 9).
  `src/Clients/PosTerminal/src/shell/ProductionShell.test.tsx` (`V1-RMD-023`
  sahipliğinde kalır) — kullanılmayan `createElement` import'u kaldırıldı
  (Bulgu 9).
- Cross-cutting dokümantasyon düzeltmesi (Bulgu 8, ayrı bir "owned surface"
  iddiası değil — her dosyanın asıl sahipliği değişmedi): `Done` durumundaki
  73 görev dosyasının kendi "## Owned surface" bölümünden, hiçbir zaman
  diskte var olmamış `evidence/V1-XXX/**` iddiasını içeren TEK satır
  mekanik olarak kaldırıldı (her dosyada tam olarak bir eşleşme; commit
  diff'i tam liste). Kanıt zaten her görevin kendi "## Acceptance evidence"
  bölümünde satır içi duruyor; bu yalnız yanlış bir dizin iddiasını
  düzeltiyor, içerik kaybı yok.

## In scope

- **Bulgu 1 [Medium] — düzeltildi:** `PostgresBillRepository
  .UpdateBillItemAsync`'in WHERE fıkrası hiç `row_version` kontrol
  etmiyordu (sınıfın kendi doc-comment'i item-seviyesi optimistic
  concurrency iddia etmesine rağmen). Bugün zararsız (yalnız bir yazan var,
  Bill'in kendi row_version'ı zaten serileştiriyor) ama gelecekte ikinci bir
  yazan (adjustments/split) eklenirse gizli lost-update riski. WHERE
  fıkrasına `AND row_version = @row_version` eklendi,
  `ExecuteNonQueryAsync`→`ExecuteScalarAsync` + `RETURNING bill_item_id`'ye
  geçildi, sonuç null ise `InvalidOperationException`. Regresyon testi:
  başka bir yazanın YALNIZ bu bill_item satırını (ebeveyn Bill'e hiç
  dokunmadan) eşzamanlı olarak değiştirdiğini simüle ediyor (ham SQL ile);
  düzeltme geçici geri alınıp testin gerçekten "No exception was thrown"
  ile kırıldığı doğrulandı, sonra geri getirildi.
- **Bulgu 2 [High] — düzeltildi:** `CatalogManagementEndpoints
  .AddCatalogManagement()`, kendi `CatalogManagerEndpointFilter`'ının
  constructor'ında istediği `IAuthorizationService`/`IRoleRepository`/
  `IDenialEventSink`'i hiç kaydetmiyordu — yalnız tam `DualScreenApplication
  .Build()` kompozisyonu bunları başka modüllerden önce kaydettiği için
  çalışıyordu; `CatalogManagementHttpTests.cs` zaten bunu elle üç servisi
  önceden kaydederek atlatıyordu (bu geçici çözümün kendisi kusurun
  kanıtıydı). Üç servis de eklendi; test dosyasındaki geçici çözüm
  kaldırılıp yalnız `AddCatalogManagement()` çağrısına indirgendi, 6/6 geçti.
- **Bulgu 3 [High] — düzeltildi:** `IRoleManagementService`/
  `RoleManagementService` (`src/Modules/Identity/Authorization/**`) rol/izin
  oluşturma, izin atama/geri alma, kullanıcı atama/geri alma komutlarının
  hepsini uygulamış ve `IdentityModule.cs`'te kayıtlıydı, ama hiçbir HTTP
  endpoint'i yoktu — rol/izin yönetimi yalnız ham SQL ile mümkündü.
  `src/Host/Experience/Roles/RoleManagementEndpoints.cs` eklendi:
  `/api/v1/management/roles` altında 6 POST/DELETE endpoint'i (Catalog'un
  manager-cookie kimlik doğrulama desenini yeniden kullanıyor), servisin
  kendisi zaten her komutta kendi `identity.roles.manage`/
  `identity.permissions.manage` yetkilendirme kararını verdiği için filter
  yalnız kimlik doğruluyor, ayrıca tek bir izin dayatmıyor
  (CODE-008 linearization kuralına uygun). Yeni bir test projesi
  (`ALKAROS.Host.Experience.Roles.Tests`, 5/5): anonim/tanınmayan oturum
  401, yetkisiz oturum 403, rol oluşturma + izin ekleme + izin atama/geri
  alma, kullanıcı atama/geri alma — hepsi gerçek Postgres üzerinde.
- **Bulgu 4 [Medium] — düzeltildi:** Ne Cashier (`cashier-app.js`) ne de
  WaiterPwa (`waiter-app.js`) üretim JS'inde çift-tıklama/yeniden-giriş
  koruması vardı — her tıklama taze bir `crypto.randomUUID()` üretiyor ve
  düğme istek sürerken devre dışı bırakılmıyordu (sunucu
  `X-Idempotency-Key`'i kabul eder ama zorunlu kılmaz). Her iki dosyada da
  bir `dispatchInFlight`/`state.dispatchInFlight` bayrağı ve düğmenin
  `disabled = true`/`finally`'de geri açılması eklendi. `pytest` ile
  Python tabanlı statik assertion'lar eklendi (dosyanın var olan test
  yaklaşımıyla tutarlı — bu istemcilerin gerçek bir JS test koşucusu yok).
- **Bulgu 5 [Medium] — düzeltildi:** `Order.CancelItem`, `reason`/
  `changedBy`/`changedAt` parametrelerini kabul ediyor ama sessizce
  atıyordu — `OrderItem.Cancel()` parametre almadığından hiçbir kalem
  seviyesi void için `OrderStatusHistoryEntry` eklenmiyordu
  (`SentItemVoidStore` özenle bir `historyReason` string'i kuruyor ama
  gönderiyor gönderiyor sonuçta atılıyordu). `RebuildWith`'e yeni bir
  `history` parametresi eklendi; `CancelItem` artık `OldStatus`/`NewStatus`
  ikisi de mevcut `Status` olan (order'ın kendi durumu bir kalem void'inde
  değişmiyor — bu satır yalnız sebep/aktörü kaydetmek için var) bir
  `OrderStatusHistoryEntry` ekliyor. `PostgresOrderRepository.SaveAsync`
  zaten yeni history girişlerini id'ye göre fark edip ekliyor (satır
  120-124), repository'de hiçbir değişiklik gerekmedi. Regresyon testi
  reason/actor/timestamp'in gerçekten kaydedildiğini doğruluyor.
- **Bulgu 6 [Medium] — düzeltildi:** `PostgresKitchenTicketRepository
  .SaveAsync`'in item `ON CONFLICT DO UPDATE`'i, ticket'taki HER kalemin
  row_version'ını her save'de koşulsuz artırıyordu, yalnız çağıranın
  gerçekten dönüştürdüğü kalemi değil — `KitchenOperationsStore`'un
  sözleşmesi (`ExpectedItemRowVersion`) kalem-seviyesi optimistic
  concurrency vaat etmesine rağmen. `WHERE kitchen.kitchen_ticket_items
  .status IS DISTINCT FROM EXCLUDED.status` eklendi (`TransitionTo` her
  gerçek geçişte `status`'u değiştiren tek metod, dolayısıyla "status
  değişmedi" güvenilir şekilde "bu kalem bu save'de dokunulmadı" anlamına
  gelir). Bu düzeltme `KitchenOperationsHttpTests
  .TicketItemLifecycleUsesAuthoritativeVersionsAndRejectsStaleWrites`'ı
  kırdı — test, saf bir ticket-seviyesi geçişin (Accepted) dokunulmamış tek
  kalemin row_version'ını da 1→2 artırdığını sabit sayı olarak varsayıyordu;
  bu, düzeltilmiş davranışla artık doğru değil (kalem gerçekten
  değişmediyse 1 kalmalı) — test, istemcinin gerçek çağrı zincirinde HER
  ZAMAN en son sunucu yanıtının sürümünü kullandığı (bu düzeltmeyle hâlâ
  doğru çalışan asıl sözleşme) gerçek sayılara güncellendi. Yeni bir
  regresyon testi (`SaveAsyncDoesNotBumpRowVersionOfAnUntouchedItem`)
  eklendi; düzeltme geçici geri alınıp testin gerçekten "Expected ... to be
  1L, but found 2L" ile kırıldığı doğrulandı, sonra geri getirildi.
- **Bulgu 7 [Low] — düzeltildi:** `database/MigrationComposition
  /order.json`'ın `phaseBRange.max` alanı ve `MigrationManifest.cs`'in
  sınıf-üstü doc-comment'i "031-049" diyordu; gerçek son migration "052"
  (`MigrationManifest.PhaseBMax` sabiti zaten doğru, JSON alanı çalışma
  zamanında hiç okunmuyor — `MigrationManifest.Load` yalnız
  `Version`/`Migrations`'ı deserialize ediyor). İkisi de "052"ye düzeltildi;
  ölü/yanıltıcı metadata dışında hiçbir işlevsel etkisi yoktu.
- **Bulgu 8 [Low] — düzeltildi:** `Done` durumundaki 292 görevden 73'ü
  (~%25), "## Owned surface" bölümünde diskte hiç var olmamış bir
  `evidence/V1-XXX/**` dizini iddia ediyordu — dalga 7 (`V1-GOV-036`)
  civarında kanıt depolama ayrı bir dizinden her görevin kendi "##
  Acceptance evidence" bölümüne kaydığında bu şablon satırı hiç
  güncellenmeden kopyalanmaya devam etmiş. Python ile (byte-safe,
  `newline=''`, her dosyanın kendi CR/LF stiliyle) her dosyadan tam olarak
  eşleşen tek satır mekanik olarak kaldırıldı; her dosyada değişikliğin tam
  olarak bir satır silme olduğu `git diff --numstat` ile doğrulandı.
- **Bulgu 9 [Low] — düzeltildi:** İki kullanılmayan import
  (`CatalogWorkspace.tsx`'in `CatalogCategory`, `ProductionShell.test.tsx`'in
  `createElement`) kaldırıldı — `tsc --noEmit`'in `noUnusedLocals`
  etkinleştirilmediği için yakalamadığı, zararsız ama derleyicinin
  yakalayabileceği kod kokusu.

## Out of scope

- `ItemExceptionHandler`'ın `IsManagerAuthorized` ölü kodu (bağımsız
  denetimde ayrıca not edildi, ama üretimde exploit edilebilir değil —
  gerçek HTTP çağıranları zaten grant akışıyla kapılıyor) — ayrı, daha
  düşük öncelikli bir temizlik.
- Gerçek, yetkilendirilmiş ikram akışının Cashier hızlı-satış istemcisine
  bağlanması (V1-RMD-101'in kendi Out of scope'unda zaten not edildi).

## Dependencies

- V1-RMD-101

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release`: 0 uyarı / 0 hata (yeni
  `ALKAROS.Host.Experience.Roles.Tests` projesi dahil).
- `dotnet test` (yerel Postgres 18, `alkaros-test-pg`, port 55432):
  `ALKAROS.Billing.BillFoundation.Tests` 41/41 (40→41, yeni
  `SaveAsyncThrowsWhenAnItemWasConcurrentlyModifiedEvenIfTheBillRowVersionIsCurrent`
  — düzeltme geçici geri alınıp kırıldığı doğrulandı);
  `ALKAROS.Host.Experience.Catalog.Tests` 6/6 (geçici DI geçici çözümü
  kaldırıldıktan sonra);
  `ALKAROS.Host.Experience.Roles.Tests` 5/5 (yeni proje);
  `ALKAROS.Host.Experience.Composition.Tests` 5/5 (rota çakışması yok, yeni
  Roles endpoint'leri dahil);
  `ALKAROS.Orders.OrderAggregate.Tests` 102/102 (Debug, WDAC Release DLL
  flakiness nedeniyle — bkz. önceki dalgaların ortam notu);
  `ALKAROS.Host.Experience.Orders.VoidSent.Tests` 8/8 (Release);
  `ALKAROS.Kitchen.TicketLifecycle.Tests` 19/19 (18→19, yeni
  `SaveAsyncDoesNotBumpRowVersionOfAnUntouchedItem` — düzeltme geçici geri
  alınıp "Expected ... 1L, but found 2L" ile kırıldığı doğrulandı);
  `ALKAROS.Host.Experience.KitchenOperations.Tests` 4/4 (güncellenen
  sayısal beklentilerle); `ALKAROS.Kitchen.OrderItemStateSync.Tests` 7/7;
  `ALKAROS.Kitchen.PhysicalPrintRecovery.Tests` 17/17;
  `ALKAROS.Kitchen.PrintQueue.Tests` 18/18 — hepsi regresyonsuz.
- `python -m pytest tests/Clients/Cashier/Frontend
  tests/Clients/WaiterPwa/Frontend`: 10/10 (yeni çift-tıklama assertion'ları
  dahil).
- `pnpm --dir src/Clients/PosTerminal typecheck`/`test`/`build`: hepsi 0
  çıkış kodu, 118/118 test.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata (73 dosyalık
  evidence-bullet düzeltmesi dahil).
- `python tools/plan-audit/plan_audit_tool.py validate-coverage`: 0 hata.
- `python tools/consistency-audit/consistency_audit.py`: temiz.
- Ortam notu: `tests/Host/MigrationComposition` süiti bu makinede kurulu
  `psql.exe`'nin eksik bir Windows CRT DLL'i (`api-ms-win-crt-locale-l1-1-0
  .dll`) nedeniyle yüklenememesinden ötürü 40/121 test başarısız — bu
  dalgadan tamamen bağımsız, önceki oturumlarda belgelenmiş bir ortam
  boşluğu (G2); `order.json`/`MigrationManifest.cs` değişikliğiyle
  ilgisizliği doğrulandı (hiçbir test bu iki dosyanın stale değerine karşı
  assert etmiyordu).

## Handoff

- V1-GOV-082
