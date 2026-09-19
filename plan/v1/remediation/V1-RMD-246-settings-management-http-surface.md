# V1-RMD-246 - Wire ISettingsService's write path into a real management endpoint

- Task ID: V1-RMD-246
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`ISettingsService.SetValueAsync`/`DeactivateAsync` (`V1-SET-001`) var
olduğundan beri hiçbir HTTP endpoint'inden çağrılmıyordu — bir ayar yalnız
kod içinden self-register edilip okunabiliyordu, bir yönetici/operatör
bunu API üzerinden hiçbir zaman değiştiremiyordu (`GarsonFeatureToggles`'ın
kendi "bir operatör kurulumda bunu elle kapatır" belgesi gerçekte
yalnızca doğrudan veritabanı müdahalesiyle mümkündü). Bir bağımsız
denetim ajanı (2026-09-18, tüm proje kod denetimi, Reporting/
Reconciliation/Observability/Audit/Settings/Operations alanı) bunu
tespit etti. Bu görev, Menu/Purchasing/Production'ın zaten kurulu
manager-cookie deseniyle gerçek bir Settings yönetim yüzeyi açar.

## Owned surface

- `src/Host/Experience/Settings/**` (yeni)
- `tests/Host/Experience/Settings/**` (yeni)
- `database/migrations/V1/V1-RMD-246/**`
- `evidence/V1-RMD-246/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs
  (çok sayıda geçmiş dalga görevinin sahipliğinde kalır) — `Build`'in
  servis kaydı zincirine `builder.Services.AddSettingsManagementExperience();`,
  route eşleme zincirine `app.MapSettingsManagement();` eklenir; mevcut
  hiçbir kayıt/eşleme değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json,
  src/Host/Composition/Migrations/MigrationManifest.cs,
  tests/Host/MigrationComposition/Manifest/ManifestTests.cs — yeni migration
  pozisyonunun (132) kaydı.
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx, build/project-manifest.json
  — yeni test projesinin kaydı (V11-UNT-001 emsaliyle aynı desen).

## In scope

- `GET /api/v1/management/settings` (opsiyonel `moduleOwner` filtresiyle) —
  tüm aktif ayarları listeler.
- `GET /api/v1/management/settings/{key}` — tek bir ayarın kaydı.
- `PUT /api/v1/management/settings/{key}` — `{NewValue, ExpectedRowVersion,
  Reason?}`, `ISettingsService.SetValueAsync` üzerinden (kendi
  `ISettingValidator`'ı zaten `PostgresSettingsRepository` içinde
  çağırıyor — tip uyuşmazlığında fail-closed).
- `DELETE /api/v1/management/settings/{key}` — `{ExpectedRowVersion, Reason?}`,
  `DeactivateAsync` üzerinden (fiziksel silme değil).
- `GET /api/v1/management/settings/{key}/history` — `GetHistoryAsync`.
- Yeni `settings.manage` izni, yalnız `manager` rolüne (Menu/Purchasing/
  Production'ın kendi manager-only izinleriyle aynı desen).

## Out of scope

- Yeni ayar KAYDI (`RegisterSettingAsync`) için bir endpoint — kayıt zaten
  her modülün kendi kodundan self-register ediliyor, bu görevin bulgusu
  yalnız var olan bir ayarı okuma/değiştirme/kapatmanın erişilemez olması.
- `GarsonFeatureToggles`'ın kendi anahtar adlarını/varsayılanlarını
  değiştirmek.

## Dependencies

- V1-SET-001

## Acceptance evidence

- Gerçek Postgres + gerçek Host'a karşı HTTP testi: bir ayar `PUT` ile
  değiştirilip `GET` ile doğru yeni değer okunur; yanlış `ExpectedRowVersion`
  409 döner; yanlış tipte bir değer 400 döner; `DELETE` sonrası ayar
  `GetValueOrDefaultAsync` üzerinden varsayılana düşer; `settings.manage`
  izni olmayan bir çağrı 403/401 alır.
- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `dotnet test` (yeni Settings management testleri) → yeşil.
- Migration boş bir veritabanında ileri/geri (up/down) denenir.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None
