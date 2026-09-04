# V1-IAM-018 - Authorization Policy Engine

- Task ID: V1-IAM-018
- Status: Done
- Assignee: claude-session-01XKRazppo9sW452rdbCZsgy
- Work type: implementation
- Surface state: Existing

## Goal

Politika motorunun deposu ve değerlendirmesi kurulur (karar dokümanı §4 adım 1):
`identity.authorization_policies` tablosu (`always_deny` / `always_allow` /
`auto_within(limit_amount, max_count, window_seconds)`), saf
`AuthorizationPolicyEvaluator` ve optimistic-concurrency'li
`IAuthorizationPolicyRepository`. Politikaları yöneticinin düzenleyeceği HTTP
yüzeyi ile grant motoruna bağlanması `V1-IAM-020` ve `V1-IAM-019`'a aittir; bu
görev DI kaydı yapmaz (tüketici yoktur).

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-018-authorization-policy-engine.md`
- `database/migrations/V1/V1-IAM-018/**`
- `src/Modules/Identity/Authorization/Policies/**`
- `tests/Modules/Identity/Authorization/Policies/**`
- `evidence/V1-IAM-018/**`
- Yüzey devri (giriş): database/MigrationComposition/order.json ve tests/Host/MigrationComposition/Manifest/ManifestTests.cs, migration 044 için V1-IAM-017'den bu göreve devredildi; ardından migration 045 için V1-IAM-019'a devredildi (PO:2026-09-04).
- src/Host/Composition/Migrations/MigrationManifest.cs içindeki PhaseBMax sabiti V1-FND-004 sahipliğinde kalır; bu görevde yalnızca faz üst sınırı 044 değerine güncellendi (V1-RMD-089/9. dalga deseni).
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## Dependencies

- V1-IAM-016
- V1-IAM-017

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release`: 0 uyarı / 0 hata.
- `dotnet test` (yerel Postgres 18): `ALKAROS.Identity.Authorization.Tests`
  78/78 (yeni `Policies/**` 29 test — `AuthorizationPolicyEvaluatorTests` saf
  mantık; `AuthorizationPoliciesMigrationTests` metin;
  `Postgres...RepositoryTests` ile `PolicyDatabaseShapeTests` ve
  `...DownMigrationTests` gerçek 005..044
  zinciriyle: upsert, optimistic-concurrency çakışması, tablo CHECK reddi,
  sıralama, silme); `Host.Tests` `Manifest.ManifestTests` 16/16
  (`PhaseBMax` 044, 43 pozisyon, son giriş tabloları `["authorization_policies"]`).
- Migration ileri: 001..044 zinciri boş `alkaros_fullmig2` veritabanına
  uygulandı; `\d identity.authorization_policies` beklenen kolon/index/CHECK
  kümesini gösterir. Geri: `044-*.down.sql` uygulandı; tablo düştü,
  `identity.role_permissions` yerinde kaldı.
- Semih için gerçek senaryo: repository ile `('bills.comp','cashier')` için
  `auto_within` limiti (≤ ₺150, ≤ 2 / 8 saat) yazılır; `AuthorizationPolicyEvaluator`
  ₺150 / 1 önceki grant için `AutoApprove`, ₺150,01 veya 2. grant için `Escalate`
  döner; politika yoksa `Escalate` (fail-closed).

## Handoff

- V1-IAM-019
