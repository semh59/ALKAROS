# V1-RMD-249 - Wire OperationalReportService (EOD/BusinessDay) into a real HTTP surface

- Task ID: V1-RMD-249
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`IOperationalReportService` (`V1-RPT-001`, gün açma/kapama +
garson/print-hata özetli EOD raporu) domain-complete ve modül
testleriyle (`tests/Modules/Reporting/V1Operations/**`, 5/5) kapsanmış
olduğundan beri hiçbir HTTP endpoint'inden çağrılmıyordu — Semih'in
"Hepsini bağla" kararıyla (bu oturumda daha önce tamamlanan 10-ajan
denetiminin bulduğu 5 tamamen ölü modülden biri) kapatılan üçüncü/beşinci
görev serisinin ilki. `ReportingModule` zaten `ModuleRegistry.DefaultCatalog`'a
kayıtlı ve DI'ya kayıt oluyor, ama `src/Host` altında sıfır kullanım vardı
(grep ile doğrulandı). Aynı modülün kardeş servisi
`IMenuInventoryReportingService` zaten `InventoryReportingEndpoints.cs`
(V11-RPT-002) ile bağlanmış — bu görev aynı deseni (manager-cookie +
kendi auth/filter sınıfları + gerçek DI) `IOperationalReportService`'e
uygular.

Gün açma/kapama, bir raporu görüntülemekten daha hassas bir işlem
(muhasebe kaydını kalıcılaştırır, `V1-RMD-085`'in tek-transaction
garantisiyle); bu yüzden okuma uçları mevcut `reports.view` (Supervisor+)
ile, açma/kapama ise yeni, manager-only `reports.close-day` izniyle
korunur — `settings.manage`/`integrations.manage` ile aynı "tek-seferlik,
eskale edilemez manager eylemi" ailesi.

## Owned surface

- `src/Host/Experience/Reporting/EndOfDayEndpoints.cs` (yeni)
- `tests/Host/Experience/Reporting/**` (yeni proje —
  `ALKAROS.Host.Experience.Reporting.Tests.csproj`, `ALKAROS.slnx`'e
  eklenir, Settings/Recipes'in kendi test projeleriyle aynı desen).
- `database/migrations/V1/V1-RMD-249/**` (yeni — `reports.close-day`
  izni, manager rolüne).
- `evidence/V1-RMD-249/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authorization/Catalog/ApplicationPermissions.cs
  (V1-IAM-017 sahipliğinde kalır) — `ReportsCloseDay = "reports.close-day"`
  eklenir, `Codes` listesine ve `RoleManager`'ın kendi ek grant kümesine
  eklenir; başka hiçbir kod değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json,
  src/Host/Composition/Migrations/MigrationManifest.cs (migration altyapısı
  sahipliğinde kalır) — yeni migration'ın pozisyonu ve `PhaseBMax`
  eklenir/güncellenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs
  (çok sayıda geçmiş dalga görevinin sahipliğinde kalır) — servis kaydı
  ve route eşleme zincirine ekleme.

## In scope

- `POST /api/v1/management/reporting/business-day/open` —
  `{BusinessDate}`, `reports.close-day` gerektirir, gerçek
  `OpenBusinessDayAsync` üzerinden; zaten açık bir gün için 409.
- `POST /api/v1/management/reporting/business-day/{businessDate}/close` —
  `{TotalRevenue, TotalOrders, CancelledItems, PrintFailures,
  WaiterSummaries?, PrintSummaries?}`, `reports.close-day` gerektirir,
  gerçek `CloseBusinessDayAsync` üzerinden (tek transaction, V1-RMD-085);
  bulunamayan/zaten kapalı gün için 404/409.
- `GET /api/v1/management/reporting/business-day/{businessDate}` —
  `reports.view` gerektirir, gerçek `GetBusinessDayByDateAsync`.
- `GET /api/v1/management/reporting/business-day/{businessDate}/full-report` —
  `reports.view` gerektirir, gerçek `GetFullDailyReportAsync`
  (gün + garson özetleri + print hata özetleri).
- Yeni `reports.close-day` izni, yalnız `manager` rolüne (migration seed).

## Out of scope

- `CalculateServiceWindow`'un kendi bir HTTP yüzeyi — pure/DB'siz bir
  yardımcı, endpoint'lerin kendi iç hesaplaması dışında dışa açılmaz.
- Gün açma/kapamanın otomatik (zamanlanmış) tetiklenmesi — yalnız manuel,
  manager tetiklemeli bir HTTP yüzeyi; bir hosted service/zamanlayıcı
  ayrı bir görev.
- `WaiterPerformanceRecord`/`PrintErrorSummaryRecord`'ın kendi hesaplama
  kaynağı (hangi sorgudan toplanacakları) — bu görev yalnız zaten
  hesaplanmış özetleri kabul eden HTTP sözleşmesini açar, çağıranın bu
  özetleri nasıl hesapladığı ayrı bir görev/entegrasyon.

## Dependencies

- V1-RPT-001

## Acceptance evidence

- Gerçek Postgres + gerçek Host'a karşı HTTP testi: gün açılır, kapatılır
  (özetlerle birlikte), `GET .../full-report` doğru birleşik sonucu
  döner; zaten açık günü tekrar açmak/kapalı günü tekrar kapatmak 409
  döner; `reports.close-day` olmayan bir manager açma/kapama
  denediğinde 403 alır (yalnız `reports.view` yeterli değildir);
  anonim çağrı 401 alır.
- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `dotnet test` (yeni testler) → yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None
