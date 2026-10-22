# V1-RMD-689 - Yardım çağrısı kalıcı, geri bildirimli ve kasada görünür

- Task ID: V1-RMD-689
- Status: Planned
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

V1-RMD-599 bulguları B002, B067, B073, B082, N001 kapanır. Bağlı yönetici yokken yardım çağrısı kaybolmaz ve garsona 'iletildi' denmez; çağrı kasada görünür kalır, görüldü bilgisi garsona döner; bekleme süresi 429 yanıtında Retry-After taşır; boş masa kimliği reddedilir; bilinmeyen çağrı türü Türkçe gösterilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-689-help-request-delivery-feedback.md`
- `evidence/V1-RMD-689/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/HelpRequests/** - yalnız çağrı akışı
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/migrations/V1/V1-RMD-689/** - yeni göç dosyaları
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/** - yalnız kasa yardım uyarısı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/** - yalnız yardım çağrısı geri bildirimi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/ApiConventions/idempotency_allowlist.json - yalnız yeni uç girdisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json - yalnız yeni göç girdisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs - yalnız PhaseBMax
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs - yalnız yeni göç girdisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/data/migration-rehearsal-profile.md - yalnız yeni göç satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-688-tunnel-route-isolation.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Kalıcı çağrı listesi ve görüldü işareti, alıcı yoksa dürüst yanıt, bekleme süresi başlığı, giriş doğrulaması, kasada sessiz olmayan uyarı, testler.

## Out of scope

- Yardım çağrısı saklama süresi (V1-RMD-692).

## Dependencies

- V1-RMD-688

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-689/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-690
