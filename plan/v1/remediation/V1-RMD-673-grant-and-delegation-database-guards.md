# V1-RMD-673 - Yetki hibesi ve devir tablolarında veritabanı korumaları

- Task ID: V1-RMD-673
- Status: Done
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-21

## Goal

V1-RMD-597 bulgusu B017 kapanır. Yetki hibesi tablosu, onaylayanın talep eden olduğu bir satırı artık motor düzeyinde reddeder (uygulama koduna güvenmeden), talep edenin rolü ve konu alanları da değişmez alanlara eklenir. Yetki devri tablosu silinemez ve yalnız iptal alanları (iptal anı ve iptal eden), yalnız bir kez ve yalnız henüz iptal edilmemiş satırda değişebilir; süre, sınır, taraflar ve tutar sonradan değiştirilemez. Yeni göç 195'tir.

## Owned surface

- `plan/v1/remediation/V1-RMD-673-grant-and-delegation-database-guards.md`
- `evidence/V1-RMD-673/**`
- `database/migrations/V1/V1-RMD-673/195-authorization-guards.up.sql`
- `database/migrations/V1/V1-RMD-673/195-authorization-guards.down.sql`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json - yalnız 195 girdisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs - yalnız PhaseBMax
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs - yalnız 195 girdisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/data/migration-rehearsal-profile.md - yalnız 195 girdisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authorization/Grants/GrantDatabase.cs - yalnız 195'in uygulanması
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authorization/Grants/AuthorizationGrantsMigrationTests.cs - yalnız yeni testler
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authorization/Delegations/DelegationDatabase.cs - yalnız 195'in uygulanması
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authorization/Delegations/AuthorizationDelegationMigrationTests.cs - yalnız yeni testler
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/domain/authorization-model.md - yalnız başlangıç limitleri notu
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-672-identity-documents-and-licences.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Hibe: `approver_user_id <> requester_user_id` CHECK (yalnız yeni satırlar için, `NOT VALID`), değişmez alanların genişletilmesi.
- Devir: silme yasağı ve iptal dışı değişim yasağı.

## Out of scope

- Hibe satırına devir kaydı (delegation_id): kayıt tipi ve çok sayıda test değişir, ayrı iş olarak açık kalır. Kullanım anında devredenin aktifliği ve kümülatif tavan (ürün kararı belirsiz). Kullanıcı ve oturum yabancı anahtarları (tam veritabanında V1-RMD-189 ile gelir). Bekleyen ret ve bütçe kayıtlarının temizliği. Başlangıç limitlerinin tohumlanması: garsona indirimde otomatik onay vermek, 2026-09-04 kararıyla (sabit indirim merdiveni yok) çelişir; limitler, politika yönetim ekranı yazılınca ve Ürün Sahibi çelişkiyi çözünce girilir.

## Dependencies

- V1-RMD-672

## Acceptance evidence

1. Her koruma için gerçek PostgreSQL'de test; her koruma için mutasyon kanıtı (göç satırı kaldırılınca test kırılır).
2. `evidence/V1-RMD-673/` altında çıktılar; Authorization ve MigrationComposition testleri yeşil.

## Handoff

- V1-RMD-598
