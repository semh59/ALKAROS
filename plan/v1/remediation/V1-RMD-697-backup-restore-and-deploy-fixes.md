# V1-RMD-697 - Yedek, kurtarma ve dağıtım düzeltmeleri

- Task ID: V1-RMD-697
- Status: Done
- Assignee: claude-code-session_01CEenPfDb6pEWVcDd9W7VdF
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

V1-RMD-599 bulguları B037, B038, B039, B040, B041, B042, B044, B045, B046, B107, B108, B118, B119, B120, B125 kapanır. Uzak yedek işleri gönderilen compose'ta çalışır; restore.sh canlı veritabanını silemez; terfi adımı bağlayıcıyı da durdurur; PITR çıktısı kalıcı hacimdedir; geri yükleme provası güncel şemayla yapılır; Caddy iç CA, sırlar ve .env yedek kapsamındadır; .dockerignore .env ve .git'i dışlar; watchdog Caddy'yi de izler; kapanış süresi, atomik arşiv komutu, port bağlama ve göç koşucusu kilidi tamamlanır; Npgsql doğrudan referanslanır.

## Owned surface

- `plan/v1/remediation/V1-RMD-697-backup-restore-and-deploy-fixes.md`
- `evidence/V1-RMD-697/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): compose.yaml - yalnız yedek, watchdog ve kapanış ayarları
- Sınırlı ek (paylaşılan, geri-tik olmadan): compose.ops.yaml - yalnız yedek ve geri yükleme servisleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): compose.dev.yaml - yalnız port bağlama
- Sınırlı ek (paylaşılan, geri-tik olmadan): .dockerignore - yalnız dışlamalar
- `.dockerignore` - yalnız .env ve .git dışlamaları
- Sınırlı ek (paylaşılan, geri-tik olmadan): deploy/** - yalnız betikler, Caddyfile ve yapılandırma
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/recovery/** - yalnız kurtarma belgeleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/operations/** - yalnız operasyon belgeleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/ALKAROS.Host.csproj - yalnız paket referansı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Program.cs - yalnız göç koşucusu kilidi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Operations/** - yalnız yedek işi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/** - yalnız yeni test
- `tests/Host/MigrationComposition/Composition/DeployFilesTests.cs` - yeni dosya
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/** - yalnız paket kilit dosyası
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs - yalnız kapanış süresi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/HostComposition.cs - yalnız göç koşucusu kilidi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/packages.lock.json - yalnız Npgsql referansı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-696-access-rate-limits-and-hub-sessions.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Servis tanımları, betik korumaları, kurtarma kılavuzu ve prova, göç koşucusu kilidi, paket referansı.

## Out of scope

- V1-RMD-698 kapsam genişlemesi (göç satır sonu politikası, göç 201, ekran koruyucu okuması) bu görevi etkilemez.
- Gerçek yedek ve geri yükleme provasının canlıda koşulması (V20-DRL-001).

## Dependencies

- V1-RMD-696

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-697/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-698
