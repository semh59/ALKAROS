# V1-RMD-691 - Bildirim (push): SSRF, sıralı gönderim, hata yönetimi, sınırlar, pasif kullanıcı

- Task ID: V1-RMD-691
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

V1-RMD-599 bulguları B005, B008, B009, B010, B011, B012, B013, B029, B059, B060, B061, B062, B063, B071, B072, B098 kapanır. Abonelik adresi iç ağa gidemez; gönderim istek yolundan çıkar ve hata sayısına göre budanır; veritabanı ve şifreleme hataları kalan abonelere bildirimi kesmez; kullanıcı başına abonelik sınırı vardır; pasifleşen kullanıcıya bildirim gitmez; bildirim masayı söyler ve ardışık bildirimler birbirini ezmez; VAPID özel anahtarı şifreli saklanır; izin ve abonelik akışı kullanıcıyı yönlendirir. Push için testler yazılır.

## Owned surface

- `plan/v1/remediation/V1-RMD-691-push-notification-hardening.md`
- `evidence/V1-RMD-691/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/WebPush/** - yalnız gönderim, kayıt ve anahtar saklama
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/WaiterNotifications/** - yalnız bildirim dağıtımı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/KitchenOperations/** - yalnız bildirim çağrısı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/PendingOrderNotifications/** - yalnız bildirim çağrısı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/** - yalnız push açma ve service worker
- `database/migrations/V1/V1-RMD-691/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json - yalnız yeni göç girdisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs - yalnız PhaseBMax
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs - yalnız yeni göç girdisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/data/migration-rehearsal-profile.md - yalnız yeni göç satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-690-customer-display-resilience-and-tests.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Adres doğrulaması, arka plan gönderim, hata süzgeci, failure_count ile budama, abonelik sınırı, aktif kullanıcı süzgeci, bildirim içeriği ve etiketi, VAPID şifreleme, izin akışı ve service worker, testler.

## Out of scope

- Push adresinin KVKK sınıfı ve saklama süresi (V1-RMD-692).

## Dependencies

- V1-RMD-690

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-691/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-692
