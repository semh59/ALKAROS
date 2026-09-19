# V1-RMD-251 - Wire IAlertService and IObservabilityService into a real HTTP surface

- Task ID: V1-RMD-251
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`IAlertService` (`V1-ALT-001`, alert lifecycle: raise/ack/escalate/
suppress/resolve, dedup, append-only event trail) and `IObservabilityService`
(`V1-OBS-001`, health-check recording/query — its correlation-scope/redaction
methods are in-process only and out of scope here) were domain-complete and
modül testleriyle (`tests/Modules/Observability/AlertFoundation/**` 12/12,
`tests/Modules/Observability/Foundation/**` 22/22) kapsanmış olduğundan beri
hiçbir HTTP endpoint'inden çağrılmıyordu — Semih'in "Hepsini bağla"
kararıyla kapatılan görev serisinin üçüncüsü/sonuncusu (bu oturumda daha
önce tamamlanan 10-ajan denetiminin bulduğu 5 tamamen ölü modülün beşincisi
ve sonuncusu). `ObservabilityModule` zaten `ModuleRegistry.DefaultCatalog`'a
kayıtlı ve her iki servisi de DI'ya kayıt ediyor, ama `src/Host` altında
sıfır kullanım vardı (grep ile doğrulandı).

Bir alarmı/health-check'i okumak `reports.view` (Supervisor+, mevcut rapor
görüntüleme izniyle aynı aile) ile korunur; bir alarmı onaylamak/eskale
etmek/susturmak/çözmek veya manuel bir health-check kaydı yazmak ise yeni,
`reconciliation.manage`/`bills.void` ile aynı tier'daki `observability.manage`
iznini gerektirir — bir alarmı yönetmek de tam olarak bir supervisor'ın
günlük işi.

## Owned surface

- `src/Host/Experience/Observability/ObservabilityEndpoints.cs` (yeni)
- `tests/Host/Experience/Observability/**` (yeni proje —
  `ALKAROS.Host.Experience.Observability.Tests.csproj`, `ALKAROS.slnx`'e
  eklenir, önceki üç görevin kendi test projeleriyle aynı desen).
- `database/migrations/V1/V1-RMD-251/**` (yeni — `observability.manage`
  izni, supervisor ve manager rollerine).
- `evidence/V1-RMD-251/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authorization/Catalog/ApplicationPermissions.cs
  (V1-IAM-017 sahipliğinde kalır) — `ObservabilityManage = "observability.manage"`
  eklenir, `Codes` listesine ve `SupervisorEscalations` kümesine eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authorization/Catalog/ApplicationPermissionsTests.cs,
  tests/Modules/Identity/Authorization/Catalog/PermissionSplitDatabase.cs
  (V1-IAM-017 sahipliğinde kalır) — kod sayısı 20→21; yeni migration'ın
  fixture zincirine eklenmesi.
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json,
  src/Host/Composition/Migrations/MigrationManifest.cs,
  tests/Host/MigrationComposition/Manifest/ManifestTests.cs (migration
  altyapısı sahipliğinde kalır) — yeni migration'ın pozisyonu, `PhaseBMax`
  ve test literalleri eklenir/güncellenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs
  (çok sayıda geçmiş dalga görevinin sahipliğinde kalır) — servis kaydı
  ve route eşleme zincirine ekleme.

## In scope

- `POST /api/v1/management/observability/alerts/raise` — `{AlertType,
  Severity, Title, Message, DeduplicationKey?, SourceReferenceType?,
  SourceReferenceId?, PayloadJson?}`, `observability.manage` gerektirir,
  gerçek `RaiseAlertAsync` üzerinden (dedup edilirse aynı alarmı döner).
- `POST /api/v1/management/observability/alerts/{alertId}/acknowledge` /
  `/escalate` / `/suppress` / `/resolve` — `{ExpectedRowVersion, Reason?}`
  (resolve ayrıca `ResolutionReason` zorunlu), `observability.manage`
  gerektirir, gerçek `AcknowledgeAlertAsync`/`EscalateAlertAsync`/
  `SuppressAlertAsync`/`ResolveAlertAsync`; geçersiz durum/versiyon için 409.
- `GET /api/v1/management/observability/alerts/active` — `reports.view`
  gerektirir, gerçek `GetActiveAlertsAsync`.
- `GET /api/v1/management/observability/alerts/{alertId}` — `reports.view`
  gerektirir, gerçek `GetByIdAsync`; bulunamazsa 404.
- `GET /api/v1/management/observability/alerts/{alertId}/events` —
  `reports.view` gerektirir, gerçek `GetEventsAsync` (audit trail).
- `GET /api/v1/management/observability/alerts/by-source?sourceReferenceType=&sourceReferenceId=` —
  `reports.view` gerektirir, gerçek `GetBySourceReferenceAsync`.
- `POST /api/v1/management/observability/health-checks` — `{CheckType,
  Target, Status, RetentionPolicyId, DetailsJson?}`, `observability.manage`
  gerektirir, gerçek `RecordHealthCheckAsync` (onaysız retention policy
  400 döner — zaten domain'in kendi kuralı).
- `GET /api/v1/management/observability/health-checks/{healthCheckId}` —
  `reports.view` gerektirir, gerçek `GetHealthCheckByIdAsync`.
- `GET /api/v1/management/observability/health-checks/by-target?target=&limit=` —
  `reports.view` gerektirir, gerçek `GetLatestHealthChecksByTargetAsync`.
- `GET /api/v1/management/observability/health-checks/unhealthy` —
  `reports.view` gerektirir, gerçek `GetUnhealthyChecksAsync`.
- Yeni `observability.manage` izni, `supervisor` ve `manager` rollerine
  (migration seed).

## Out of scope

- `IObservabilityService.BeginCorrelationScope`/`AddTraceStep`/`RedactPayload` —
  in-process, DB'siz yardımcılar; bir HTTP isteğinin kendi middleware/
  altyapı katmanında kullanılır, dışa açık bir uç noktaları olmaz.
- Alarmların OTOMATİK üretici tarafı (hangi domain olayının bir alarm
  tetikleyeceği, health-check'lerin periyodik toplanması) — bu görev
  yalnız zaten var olan domain servisinin manuel/HTTP tetiklemeli
  yüzeyini açar, otomatik üretim ayrı bir entegrasyon görevi.

## Dependencies

- V1-ALT-001
- V1-OBS-001

## Acceptance evidence

- Gerçek Postgres + gerçek Host'a karşı HTTP testi: bir alarm açılır, aynı
  dedup anahtarıyla ikinci açma isteği aynı alarmı döner
  (`WasDeduplicated=true`), onaylama/eskale/susturma/çözme akışları doğru
  durum geçişlerini üretir, `GET .../events` audit trail'i doğru sırada
  döner; geçersiz versiyon 409 döner; bir health-check kaydedilir ve
  onaysız bir retention policy ID ile 400 döner; `observability.manage`
  olmayan (yalnız `reports.view` sahibi) bir kullanıcı okuma yapabilir ama
  değiştiremez (403); anonim çağrı 401 alır.
- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `dotnet test` (yeni testler + `ALKAROS.Identity.Authorization.Tests`) →
  yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None
