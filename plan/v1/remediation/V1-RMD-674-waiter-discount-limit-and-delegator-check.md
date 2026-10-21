# V1-RMD-674 - Garson indirim limiti ve devreden kişinin kullanım anındaki aktifliği

- Task ID: V1-RMD-674
- Status: Done
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-21

## Goal

V1-RMD-597 bulguları B016 (indirim kısmı) ve B010'un kullanım anı yarısı kapanır; Ürün Sahibinin 2026-10-21 kararları uygulanır. Garson, tek indirim isteğini 100 TL'ye kadar yönetici onayı beklemeden kendisi onaylar (en çok 10 indirim, 8 saatlik pencere içinde); üstü yöneticiye gider. İkram ve gönderilmiş kalem iptali için otomatik limit açılmaz, yönetici onaylar. Süreli bir devir, devri veren kişi pasifleştirildiyse kullanım anında geçersizdir; devir kaydı silinmez ama artık hiçbir isteği onaylamaz.

## Owned surface

- `plan/v1/remediation/V1-RMD-674-waiter-discount-limit-and-delegator-check.md`
- `evidence/V1-RMD-674/**`
- `database/migrations/V1/V1-RMD-674/196-waiter-discount-policy.up.sql`
- `database/migrations/V1/V1-RMD-674/196-waiter-discount-policy.down.sql`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json - yalnız 196 girdisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs - yalnız PhaseBMax
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs - yalnız 196 girdisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authorization/Delegations/PostgresAuthorizationDelegationRepository.cs - yalnız devreden aktifliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authorization/Delegations/PostgresAuthorizationDelegationRepositoryTests.cs - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authorization/Delegations/AuthorizationDelegationMigrationTests.cs - yalnız politika tohumu testi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Billing/BillingSplitHttpTests.cs - yalnız tohumlanan politikaya uyum
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/domain/authorization-model.md - yalnız indirim limiti ve devir notu
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-673-grant-and-delegation-database-guards.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Göç 196: `waiter` × `bills.discount` için `auto_within` (100 TL, en çok 10, 28800 sn) satırı; zaten bir satır varsa dokunmaz.
- Devir bulunurken devreden pasifse sayılmaz.

## Out of scope

- İkram ve iptal limitleri, politika yönetim ekranı (ayrı iş; limitler ekrandan değişecek). Devirde kümülatif tavan. Hibede devir kaydı. Göçün Host paketleri dışındaki test projelerinde elle seçilen göç listeleri.

## Dependencies

- V1-RMD-673

## Acceptance evidence

1. Her kural için gerçek PostgreSQL testi; mutasyon kanıtı.
2. `evidence/V1-RMD-674/` altında çıktılar; Authorization, MigrationComposition ve Billing testleri yeşil.

## Handoff

- V1-RMD-675
