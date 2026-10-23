# V1-RMD-697 - Yedek, kurtarma ve dağıtım düzeltmeleri

## Ne değişti

- **Uzak yedek işi (B037):** `compose.yaml` içindeki api artık yedek hacmini salt okunur (`/backups`) ve uzak yedek dizinini (`/offsite-backup`, hacim `alkaros-offsite-backup`) bağlıyor; iş "yedek dizini yok" diye kapalı kalmıyor. Gerçek uzak kopya için o hacme harici depolama bağlanmalı (belgelendi).
- **restore.sh (B038):** hedef canlı veritabanı adıysa (`ALKAROS_LIVE_DB`, varsayılan `alkaros`) ya da `postgres` ise hiçbir şey silinmeden çıkış 5 ile reddediliyor.
- **Terfi adımı (B039):** runbook `api`, `connector` ve `watchdog`'u birlikte durdurup başlatıyor.
- **PITR çıktısı (B040):** `basebackup` servisine `alkaros-restore` hacmi bağlandı; dizin tek kullanımlık konteynerle kaybolmuyor. `rpo-rto-targets.md` betiğin WAL'ı kendisi oynatmadığını söylüyor.
- **Yedeğe girmeyenler (B042):** runbook `.env`, parola dosyaları ve `caddy-data` (iç CA) için ayrı yedek istiyor.
- **Sırlar belgesi (B044):** kontrol listesi, ana anahtar ve Telegram anahtarının ortam değişkeni olarak `.env`'den geldiğini açıkça söylüyor.
- **Derleme bağlamı (B045):** `.dockerignore` `.env`, `.env.*` ve `.git`'i dışlıyor.
- **Geliştirme ve önbellek (B119):** geliştirme compose'u tüm portları yalnız 127.0.0.1'e açıyor; üretim Caddy'si uygulama kabuğunu her yüklemede yeniden doğrulatıyor (`/assets` hariç). Caddyfile geçici bir Caddy ile doğrulandı.
- **Kapanış (B118):** ana makine kapanış bekleme süresi 35 sn, `stop_grace_period` 50 sn.
- **WAL arşivi (B120):** arşiv komutu geçici ada kopyalayıp yeniden adlandırıyor; yarım kalan kopya gerçek adda kalmıyor.
- **Göç koşucusu kilidi (B108):** ileri ve geri alma koşuları PostgreSQL danışma kilidi alıyor; ikinci eşzamanlı koşu "kilit başka koşuda" mesajıyla, hiçbir şey çalıştırmadan reddediliyor.
- **Npgsql (B125):** Host projesi Npgsql'i doğrudan referanslıyor; kilit dosyaları (63) yenilendi, `--locked-mode` geri yükleme temiz.

## Kanıt

- `mutate.mjs` / `mutations.log`: 20 mutant; tümü KILLED. İlk geçişte S1 (kapanış süresi ayarının silinmesi) sağ kaldı çünkü .NET varsayılanı 30 sn'ydi; süre 35 sn'ye çıkarılıp test sınırı varsayılanın üstüne alındı, yeniden koşuda KILLED.
- Yeni testler: `DeployFilesTests` (restore koruması gerçek `sh` ile, arşiv komutu, derleme bağlamı, compose, dev portları, Caddy), göç kilidi (ileri, geri alma), kapanış süresi.
- Tam `Host.Tests` ve ilgili proje koşuları `logs/` altında.

## Yapılmayanlar / doğrulanamadı

- **B041 (bayat geri yükleme tatbikatı):** gerçek tatbikat ve güncel şema kanıtı V20-DRL-001'in işi; bu görevde koşulmadı.
- **B046 (watchdog Caddy'yi izlemiyor):** iç watchdog bilerek api'yi izliyor; Caddy'yi dışarıdan izleme kontrol listesinde ayrı adım olarak duruyor. Değiştirilmedi.
- **B107 (göç kimliği tavanı):** üç hane ve sabit `PhaseBMax` bilinçli, fail-closed; tavan 999, şu an 197. order.json kısmı V1-RMD-597'nin.
- **B037 gerçek uzak hedef:** yerel dizin varsayılan kalır; harici depolama bağlantısı dağıtıma özgü ve burada doğrulanamadı.
- **B120 ve Docker davranışı:** WAL arşivi komutu gerçek PostgreSQL'de çalıştırılmadı; kabuk testi komutun davranışını kanıtlıyor. `stop_grace_period` ve hacimler `docker compose config` ile yalnız sözdizimi doğrulandı; canlı yığına dokunulmadı.
