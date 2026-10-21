# V1-RMD-687 - Ayar geçmişi değişmez ve logo tablosu sertleşir (göç)

- Task ID: V1-RMD-687
- Status: Done
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-21

## Goal

V1-RMD-598 bulguları R9 (değişmezlik) ve R16 (logo tablosu) kapanır. Ayar geçmişi satırları silinemez ve değiştirilemez (ayar silinse bile geçmiş gitmez: ON DELETE CASCADE yerine RESTRICT); logo tablosunda içerik türü ve boyut CHECK ile sınırlanır, ölü sürüm kolonu belgelenir. Göç artımlıdır (197), mevcut 026 ve 136 değişmez, geri alma betiği ve testi vardır.

## Owned surface

- `plan/v1/remediation/V1-RMD-687-settings-history-immutable-migration.md`
- `evidence/V1-RMD-687/**`
- `database/migrations/V1/V1-RMD-687/197-settings-history-immutable.up.sql`
- `database/migrations/V1/V1-RMD-687/197-settings-history-immutable.down.sql`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json - yalnız 197 girdisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs - yalnız PhaseBMax
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs - yalnız 197 girdisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/data/migration-rehearsal-profile.md - yalnız 197 satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Settings/TypedSettings/PostgresSettingsRepositoryTests.cs - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Settings/TypedSettings/Fixtures/SettingsTestDatabase.cs - yalnız 197'nin uygulanması
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Settings/BusinessIdentity/BusinessLogoStoreTests.cs - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Settings/BusinessIdentity/Fixtures/BusinessIdentityTestDatabase.cs - yalnız 197'nin uygulanması
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Settings/SettingsManagementTestDatabase.cs - yalnız 197'nin uygulanması
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-686-settings-history-cache-tests-docs.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- 197 göçü (geçmişte UPDATE ve DELETE engelleyen tetikleyici, FK RESTRICT, logo CHECK), up ve down betiği, geri alma ve ileri alma testi, mevcut veriyle uyumluluk (mevcut satırlar CHECK'i bozmamalı; NOT VALID ile eklenir), göç kayıt noktaları.

## Out of scope

- Geçmiş sayfalama (V1-RMD-686), logo okuma ve denetim izi (V1-RMD-685).

## Dependencies

- V1-RMD-686

## Acceptance evidence

1. Gerçek PostgreSQL'de ileri ve geri alma testi; silme ve güncelleme denemesinin reddi; mutasyon kanıtı.
2. Manifest, Composition ve ayar test projeleri yeşil; `evidence/V1-RMD-687/` altında çıktılar.

## Handoff

- V1-RMD-599
