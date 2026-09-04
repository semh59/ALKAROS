# V1-IAM-019 - Grant Request Engine And Event

- Task ID: V1-IAM-019
- Status: Done
- Assignee: claude-session-01XKRazppo9sW452rdbCZsgy
- Work type: implementation
- Surface state: Existing

## Goal

Asenkron yetki isteği motoru: `grant` sınıfı bir eylem, değişmez bir `authorization_grants` isteği yaratır; çözüm sırası politika, devir ve yönetici kararıdır; her sonlanış tek bir append-only satırdır (isteyen, onaylayan ya da politika, `policy_path`, `reason_code`, para farkı) ve `reporting` projeksiyonunu besler.

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-019-grant-request-engine-and-event.md`
- `database/migrations/V1/V1-IAM-019/**`
- `evidence/V1-IAM-019/**`
- Yüzey devri (giriş): database/MigrationComposition/order.json ve tests/Host/MigrationComposition/Manifest/ManifestTests.cs, migration 045 için V1-IAM-018'den bu göreve devredildi (PO:2026-09-04). src/Modules/Identity/IdentityModule.cs, yetkilendirme dalgasının DI kayıt evi olarak V1-RMD-002'den bu göreve devredildi (PO:2026-09-04).
- Yüzey devri (çıkış): database/MigrationComposition/order.json, tests/Host/MigrationComposition/Manifest/ManifestTests.cs (migration 046), src/Modules/Identity/Authorization/Grants/** ile tests/Modules/Identity/Authorization/Grants/** (tırmanma çözücü kancası ve AuthorizationGrantService çözücü yürüyüşü) ve src/Modules/Identity/IdentityModule.cs, bu görev kapandıktan sonra V1-IAM-021'e devredildi (PO:2026-09-04).
- src/Host/Composition/Migrations/MigrationManifest.cs içindeki PhaseBMax sabiti V1-FND-004 sahipliğinde kalır; bu görevde yalnızca faz üst sınırı 045 değerine güncellenmişti (V1-RMD-089/9. dalga deseni).
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## Dependencies

- V1-IAM-018

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release`: 0 uyarı / 0 hata.
- `dotnet test` (yerel Postgres 18): `ALKAROS.Identity.Authorization.Tests`
  106/106 (yeni `Grants/**` 30 test — `GrantModelTests` saf;
  `AuthorizationGrantsMigrationTests` metin; `AuthorizationGrantServiceTests`
  gerçek repo + DB: no-policy → Pending, idempotent tekrar aynı satırı döner,
  `always_allow`/`always_deny`, `auto_within` limit ile sayaç, own-check
  guard'ın başka garsonun çekinde void'i reddetmesi ve kendi çekinde
  reddetmemesi; `PostgresAuthorizationGrantRepositoryTests` ile
  `...ShapeTests` ve `...DownMigrationTests`: pending→granted stamp, append-once trigger
  (alan mutasyonu / DELETE / yeniden çözüm reddi), idempotency UNIQUE,
  `CountAutoGrantsSince` yalnız granted+auto sayar, `reporting.authorization_grant_daily`
  görünümü granted satırları gün/gerekçe/rol/policy_path kırılımında toplar);
  `Host.Tests` `Manifest.ManifestTests` 16/16 (`PhaseBMax` 045, 44 pozisyon,
  son giriş tabloları `["authorization_grants"]`).
- Migration ileri: 001..045 zinciri boş `alkaros_fm3` veritabanına uygulandı;
  `identity.authorization_grants` + `reporting.authorization_grant_daily`
  oluştu. Geri: `045-*.down.sql` uygulandı; tablo, trigger, fonksiyon ve
  görünüm düştü, `identity.authorization_policies` ile `reporting` şeması
  yerinde kaldı (`CREATE SCHEMA IF NOT EXISTS reporting;` guard'ı yalnız
  izole uygulamada devreye girer).
- Semih için gerçek senaryo: garson kendi çekinde ikram (`bills.comp`) dener;
  politika yoksa istek `pending` olur (yöneticiye düşer). Başka garsonun
  çekinde denerse own-check guard yöneticiye ulaşmadan `denied` yazar.
  Yönetici `('bills.comp','waiter')` için `auto_within` (≤ ₺150, ≤ 2 / 8 saat)
  tanımlarsa limit içindeki ilk iki istek `granted`/`policy_path=auto`, üçüncü
  yeniden `pending`.

## Handoff

- V1-IAM-020
- V1-IAM-023
