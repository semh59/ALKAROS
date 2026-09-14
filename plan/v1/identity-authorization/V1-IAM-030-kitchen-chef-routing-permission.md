# V1-IAM-030 - Mutfak Şefi'ne yazıcı/rota yönetimi izni

- Task ID: V1-IAM-030
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`V1-IAM-029`'un bilinçli olarak açık bıraktığı soru artık cevaplandı
(Semih, 2026-09-14): Mutfak Şefi ("kitchen-chef") rolü
`kitchen.routing.manage`'i de taşısın — yazıcı/rota yönetim ekranını
supervisor/manager'la paylaşsın. `V1-IAM-029` zaten `Done` ve kapalı bir
görev olduğu için (kendi Owned Surface'ı sabitlendi, bağımsız denetimden
geçti) bu değişiklik o görevi yeniden açmak yerine yeni, dar kapsamlı bir
migration'la yapılır — `V1-IAM-029`'un kendi deseniyle birebir aynı
(rol zaten var, yalnız bir izin daha eklenir).

## Owned surface

- `database/migrations/V1/V1-IAM-030/**` (yeni) — `role_permissions`'a
  `kitchen-chef` + `kitchen.routing.manage` ataması (izin kodu zaten
  migration 042'den beri `identity.permissions`'ta var, yalnız yeni bir
  role_permissions satırı).
- database/MigrationComposition/order.json, src/Host/Composition/
  Migrations/MigrationManifest.cs, tests/Host/MigrationComposition/
  Manifest/ManifestTests.cs (Sınırlı ek, paylaşılan — V1-IAM-028/029
  emsaliyle aynı desen) — yeni migration pozisyonu 112.
- tests/Modules/Identity/Authorization/Catalog/PermissionSplitDatabase.cs,
  PermissionSplitDatabaseTests.cs (Sınırlı ek, paylaşılan — V1-IAM-028
  sahipliğinde kalan dosyalar) — 112'yi up/down zincirine ekler, mevcut
  `kitchen-chef` testini günceller.
- `docs/domain/authorization-model.md` — §3.2'deki "kitchen.routing.manage
  kasıtlı olarak verilmiyor" cümlesinin güncellenmesi.

## Out of scope

- `kitchen-staff` rolüne bu izni vermek — yalnız Mutfak Şefi, düz
  personel hâlâ yalnız ileri geçiş yapabilir.
- Frontend'in bu izni kullanması — `PrinterPanel`'in `canOperate`
  (`orders.send`) kapısı zaten var, bu görev yalnız rolün İZNİ
  taşımasını sağlar; frontend zaten `capabilitySet.has(...)` üzerinden
  her izni ayrı ayrı okuyabilir durumda, ayrı bir görev gerekmiyor (bu
  görevin kendi Acceptance evidence'ında elle doğrulanır).

## Dependencies

- V1-IAM-029

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → **0 uyarı, 0 hata** (doğrulandı).
- `dotnet test tests/Modules/Identity/Authorization` → gerçek Postgres'e
  karşı **200/200 yeşil** (`KitchenChefRoleExistsAndHoldsCancelReprintSuspendAdvanceAndRouting`
  artık `kitchen.routing.manage`'i de bekliyor; down-migration testi
  `kitchen.routing.manage`'in KENDİSİNİN — 042'nin sahipliğinde —
  hayatta kaldığını, yalnız kitchen-chef'e verilen grant'in kaybolduğunu
  doğruluyor).
- `dotnet test tests/Host/MigrationComposition` → **135/135 yeşil**,
  migration 112 kapsanır.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı (doğrulandı).
- `python tools/consistency-audit/consistency_audit.py` → `clean`
  (doğrulandı).
- Semih'in elle deneyebileceği senaryo: "Mutfak Şefi" rolüne atanmış bir
  kullanıcıyla oturum aç, mutfak ekranındaki yazıcı rotası formunu
  kullanabildiğini doğrula.

## Handoff

- None
