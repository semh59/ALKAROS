# V1-RMD-700 - Düşük yetkili veritabanı rolü ve veritabanı TLS

- Task ID: V1-RMD-700
- Status: Done
- Assignee: claude-code-session_01CEenPfDb6pEWVcDd9W7VdF
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-23

## Goal

V1-RMD-599 bulguları B036 ve B077 kapanır. Uygulama (api) ve QR bağlayıcısı süper kullanıcı olmayan, veritabanı veya rol oluşturamayan bir rolle çalışır; göç ve operasyon araçları sahip rolünü kullanır. Rol betiği her `up` adımında göçten sonra yeniden uygulanır, böylece sonradan eklenen şemalar ve tablolar da yetkilenir. Veritabanı bağlantısı TLS'lidir: sunucu `hostssl` dışı uzak bağlantıyı reddeder, istemciler SSL kipini açıkça ayarlar, sertifika anahtarı grup ve herkes tarafından okunamaz. api ve bağlayıcı konteynerlerinden gereksiz yetkiler alınır (`cap_drop`, `no-new-privileges`).

## Owned surface

- `plan/v1/remediation/V1-RMD-700-database-role-and-tls.md`
- `evidence/V1-RMD-700/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): compose.yaml - yalnız rol, TLS ve konteyner yetkileri
- Sınırlı ek (paylaşılan, geri-tik olmadan): deploy/docker/** - yalnız rol betiği, pg_hba ve TLS dosyaları
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/** - yalnız SSL kipi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/** - yalnız SSL kipi ve seçenekler
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Program.cs - yalnız SSL kipi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/SecurityAdministration/** - yalnız geri yükleme provası yetkisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Integrations/QrRelay/LocalConnector/** - yalnız SSL kipi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): compose.ops.yaml - yalnız TLS ortamı ve gizli anahtar
- Sınırlı ek (paylaşılan, geri-tik olmadan): compose.dev.yaml - yalnız TLS ortamı ve gizli anahtar
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/operations/** - yalnız canlıya geçiş kontrol listesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-699-client-accessibility-and-doc-deviations.md - yalnız Out of scope notu
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/ModuleBoundaries/** - yalnız DualScreen için QrRelay bağımlılığı
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/architecture/** - yalnız DualScreen için QrRelay bağımlılığı
- `deploy/docker/roles.sql`
- `deploy/docker/generate-db-tls.sh`
- `deploy/docker/app_db_password.example`
- `deploy/docker/restore_db_password.example`
- `tests/Host/MigrationComposition/Composition/DatabaseRolesTests.cs`
- `tests/Host/MigrationComposition/Composition/DatabaseSslTests.cs`
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Rol betiği ve yetki testi (gerçek PostgreSQL), çalışma zamanında DDL kullanan yolların listesi, SSL kipi ayarı, tek kullanımlık deneme konteynerinde gerçek TLS doğrulaması.

## Out of scope

- Canlı yığına dokunmak; canlıya geçiş ayrı onaylı bir adımdır.
- Terminal kimliğinin sunucuda doğrulanması V1-RMD-701 görevindedir.

## Dependencies

- V1-RMD-697

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-700/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-701
