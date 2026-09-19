# V1-RMD-250 - Wire ReconciliationService (CaseFoundation) into a real HTTP surface

- Task ID: V1-RMD-250
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`IReconciliationService` (`V1-REC-001`, mutabakat/uyuşmazlık vaka
yaşam döngüsü — açma/dedup, durum geçişi, not ekleme) domain-complete ve
modül testleriyle (`tests/Modules/Reconciliation/CaseFoundation/**`, 6/6)
kapsanmış olduğundan beri hiçbir HTTP endpoint'inden çağrılmıyordu —
Semih'in "Hepsini bağla" kararıyla kapatılan görev serisinin ikincisi
(`V1-RMD-249`'un EOD wiring'inden sonra). `ReconciliationModule` zaten
`ModuleRegistry.DefaultCatalog`'a kayıtlı ve DI'ya kayıt oluyor, ama
`src/Host` altında sıfır kullanım vardı (grep ile doğrulandı).

Bir uyuşmazlık vakasını okumak (`reports.view`, Supervisor+, zaten var
olan rapor görüntüleme izniyle aynı aile — bir vaka da sonuçta bir
mutabakat raporu) ile onu değiştirmek (durum geçişi, not ekleme, yeni vaka
açma) farklı ağırlıkta eylemler; ikincisi yeni bir `reconciliation.manage`
izni gerektirir. Bu izin, `bills.void`/`bills.comp` ile aynı "supervisor
zaten eskale edilmiş bir istisnayı çözebilir" ailesine konur (manager-only
değil) — çünkü bir mutabakat vakasını araştırıp kapatmak, tam olarak bir
supervisor'ın günlük işi.

## Owned surface

- `src/Host/Experience/Reconciliation/ReconciliationCaseEndpoints.cs` (yeni)
- `tests/Host/Experience/Reconciliation/**` (yeni proje —
  `ALKAROS.Host.Experience.Reconciliation.Tests.csproj`, `ALKAROS.slnx`'e
  eklenir, Settings/Recipes/Reporting'in kendi test projeleriyle aynı desen).
- `database/migrations/V1/V1-RMD-250/**` (yeni — `reconciliation.manage`
  izni, supervisor ve manager rollerine).
- `evidence/V1-RMD-250/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authorization/Catalog/ApplicationPermissions.cs
  (V1-IAM-017 sahipliğinde kalır) — `ReconciliationManage = "reconciliation.manage"`
  eklenir, `Codes` listesine ve `SupervisorEscalations` kümesine eklenir;
  başka hiçbir kod değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authorization/Catalog/ApplicationPermissionsTests.cs,
  tests/Modules/Identity/Authorization/Catalog/PermissionSplitDatabase.cs
  (V1-IAM-017 sahipliğinde kalır) — kod sayısı 19→20; yeni migration'ın
  fixture zincirine eklenmesi (V1-RMD-249'un aynı sınıfta bıraktığı
  desenle aynı, gerçek `dotnet test` koşusuyla yakalanan bir gereklilik).
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json,
  src/Host/Composition/Migrations/MigrationManifest.cs,
  tests/Host/MigrationComposition/Manifest/ManifestTests.cs (migration
  altyapısı sahipliğinde kalır) — yeni migration'ın pozisyonu, `PhaseBMax`
  ve test literalleri eklenir/güncellenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs
  (çok sayıda geçmiş dalga görevinin sahipliğinde kalır) — servis kaydı
  ve route eşleme zincirine ekleme.

## In scope

- `POST /api/v1/management/reconciliation/cases` — `{DeduplicationKey,
  CaseType, SourceARef, SourceBRef, DiscrepancyAmount, Severity,
  DetailsJson?}`, `reconciliation.manage` gerektirir, gerçek
  `CreateOrDeduplicateCaseAsync` üzerinden (aynı dedup anahtarıyla ikinci
  çağrı, yeni kayıt değil var olanı döner — 201 yerine 200).
- `POST /api/v1/management/reconciliation/cases/{caseId}/transition` —
  `{NewStatus, ExpectedVersion, ReasonOrNote?}`, `reconciliation.manage`
  gerektirir, gerçek `TransitionCaseStatusAsync`; geçersiz geçiş/versiyon
  uyuşmazlığı için 409.
- `POST /api/v1/management/reconciliation/cases/{caseId}/notes` —
  `{Note}`, `reconciliation.manage` gerektirir, gerçek `AddCaseNoteAsync`.
- `GET /api/v1/management/reconciliation/cases/{caseId}` — `reports.view`
  gerektirir, gerçek `GetCaseByIdAsync`; bulunamazsa 404.
- `GET /api/v1/management/reconciliation/cases/{caseId}/actions` —
  `reports.view` gerektirir, gerçek `GetCaseActionsAsync` (audit trail).
- `GET /api/v1/management/reconciliation/cases?status=&limit=` —
  `reports.view` gerektirir, gerçek `GetCasesByStatusAsync`.
- Yeni `reconciliation.manage` izni, `supervisor` ve `manager` rollerine
  (migration seed) — `bills.void`/`bills.comp` ile aynı tier.

## Out of scope

- Bir vakayı OTOMATİK açan üretici tarafı (hangi domain olayının/karşılaştırmanın
  bir uyuşmazlık vakası tetikleyeceği) — bu görev yalnız zaten var olan
  domain servisinin manuel/HTTP tetiklemeli yüzeyini açar, otomatik
  üretim ayrı bir entegrasyon görevi.
- `GetActiveCaseByDedupKeyAsync`'in kendi bir HTTP yüzeyi — `CreateOrDeduplicateCaseAsync`
  zaten aynı işlevi (dedup) tek çağrıda kapsıyor.
- `ReconciliationCaseChanged` entegrasyon event'inin (module-dependency-rules.md
  satır 22) gerçek yayınlanması — bu görev yalnız HTTP CRUD'unu açar,
  event yayını ayrı, zaten var olan sözleşme kapsamına giren bir görev.

## Dependencies

- V1-REC-001

## Acceptance evidence

- Gerçek Postgres + gerçek Host'a karşı HTTP testi: yeni vaka açılır,
  aynı dedup anahtarıyla ikinci açma isteği AYNI vakayı döner (yeni kayıt
  oluşturmaz), durum geçişi + not ekleme çalışır, `GET .../actions` audit
  trail'i doğru sırada döner; geçersiz versiyon 409 döner;
  `reconciliation.manage` olmayan (yalnız `reports.view` sahibi) bir
  kullanıcı okuma yapabilir ama değiştiremez (403); anonim çağrı 401 alır.
- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `dotnet test` (yeni testler + `ALKAROS.Identity.Authorization.Tests`) →
  yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None
