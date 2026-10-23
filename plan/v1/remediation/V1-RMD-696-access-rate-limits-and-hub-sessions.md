# V1-RMD-696 - Hız sınırı, kimlik, hub oturumu ve veritabanı rolü sertleştirme

- Task ID: V1-RMD-696
- Status: InProgress
- Assignee: claude-code-session_01CEenPfDb6pEWVcDd9W7VdF
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

V1-RMD-599 bulguları B016, B031, B032, B033, B034, B035, B036, B064, B077, B078, B079, B080, B088, B103, B116, B117 kapanır. Hub bağlantıları oturum iptali ve pasifleştirmeden sonra düşer; ekran koruyucu yükleme yetkiden sonra okunur; yönetim, mutfak ve fatura gruplarına hız sınırı gelir; qr-order sınırı istemci başlığına bağlı olmaz; terminal kimliği sunucuda doğrulanır; uygulama süper kullanıcı olmayan veritabanı rolüyle çalışır; elle başlatılan bakım işi aktör kaydı bırakır; parola taşıyan kayıtlar ve sağlık yanıtı sır sızdırmaz.

## Owned surface

- `plan/v1/remediation/V1-RMD-696-access-rate-limits-and-hub-sessions.md`
- `evidence/V1-RMD-696/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/** - yalnız kimlik, hız sınırı ve hub
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/** - yalnız hız sınırı ve hub oturum kontrolü
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Program.cs - yalnız bağlantı dizesi ve komut doğrulaması
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/** - yalnız seçenekler
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/SecurityAdministration/** - yalnız aktör kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/** - yalnız oturum doğrulama
- Sınırlı ek (paylaşılan, geri-tik olmadan): compose.yaml - yalnız rol ve kullanıcı ayarları
- Sınırlı ek (paylaşılan, geri-tik olmadan): deploy/docker/** - yalnız rol ve TLS dosya izinleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/** - yalnız yeni test
- `src/Host/DualScreen/HubSessionRegistry.cs` - yeni dosya, hub oturum kaydı ve tarayıcı
- `tests/Host/MigrationComposition/DualScreen/AccessHardeningTests.cs` - yeni dosya
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/OnlineOrdering/** - yalnız platform adresi ana makine kısıtı (B080)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/OnlineOrdering/** - yalnız platform adresi hata iletisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-695-module-boundaries-and-composition.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Hub yeniden doğrulama, filtre sırası, hız sınırı kovaları, terminal kaydı, rol ayrımı, kayıt türleri, TLS sertifika dosyası, testler.

## Out of scope

- CSP/HSTS (V1-RMD-335).

## Dependencies

- V1-RMD-695

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-696/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-697
