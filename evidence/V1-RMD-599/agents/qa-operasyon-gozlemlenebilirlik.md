# V1-RMD-599 Host (platform katmanı) - Operasyon, dağıtım, gözlemlenebilirlik denetimi

Rol: qa-operasyon-gozlemlenebilirlik. Commit: d1dc86b6 (git rev-parse --short HEAD). Salt okunur; docker/compose çalıştırılmadı, canlı yığına (8090/5080/5433) dokunulmadı, test koşulmadı.

## 1. Kapsam envanteri ve kontrol sayısı

Envanter komutu: `wc -l evidence/V1-RMD-599/baseline/scope-files.txt` -> 53. Ek kapsam: deploy/docker/ (Caddyfile, Caddyfile.dev, Dockerfile, README, backup/restore/basebackup/restore-pitr/selfcheck betikleri, pg_hba, postgresql.tuned.conf, seed-demo.sql), compose.yaml, compose.ops.yaml, compose.dev.yaml, compose.test.yaml, .dockerignore, docs/recovery/*, docs/operations/*, docs/architecture/deployment-compatibility-matrix.md, docs/compliance/kvkk-retention-runbook.md (ilgili bölümler), Host/Observability/OutageWatchdog, SecurityAdministration/Maintenance (yedek işleri), Operations/OperationsModule.cs.

Dilim kontrolü: 53/53 dosya envanterde. Satır satır okunan: 38 (Program.cs, HostComposition, MigrationExecutor/PsqlScriptRunner/HistoryStore/Validator/Discoverer/File, ApiErrorArea/Handling/ExceptionHandler, ApiErrorCatalog (başlık+Default+seçili alanlar), DualScreenApplication.cs, DualScreenOptions, DualScreenTls, DualScreenToken, CustomerDisplayHub, WebPush x4, HelpRequests x4, WaiterNotifications x5, ManagementSessionLookup, HostDatabaseConnection, SQL migration x5). Yalnız grep ile örneklenen (catch, log, TODO/Fake, çerez, health): 15 (DualScreenApplication.CashSession/Endpoints/Payments/Receipt/Screensaver, DualScreenStore.* x5, Contracts x3, Exceptions, ModuleRegistry, MigrationManifest/ManifestException). Bu 15 dosyada iş mantığı değil yalnız operasyonel örüntüler arandı.

E8 arama sonuçları: grep GRANT/REVOKE/CREATE ROLE database/migrations/**/*.up.sql -> 0 (pg_dump --no-privileges ile yetki kaybı yok). Dilim .cs dosyalarında ILogger/LoggerMessage yalnız WebPushSender.cs ve ApiExceptionHandler.cs'te var; DualScreen*, HelpRequests, WaiterNotifications günlük yazmaz.

## 2. Bulgu listesi

Kanıt düzeyleri: KO = Kod okuması, ÇK = Çıkarım. Çalıştırılan komut yok (kısıt). Biçim: No · Önem · Kanıt düzeyi · Başlık; sonra Kanıt / Sorun-Etki / Düzeltme.

### 2a. Dağıtım, röle, yedek ve kurtarma

**B1 · Yüksek · KO · Röle/QR kaynak kapısı gerilemesi**
- Kanıt: DualScreenApplication.cs:560-572 `var onNfcOrigin = nfcOriginPorts.Contains(...) || (nfcOriginHeader ...) || (nfcLoopbackOriginTrusted && ... IPAddress.IsLoopback(remoteAddress))`; :582 `if (!onNfcOrigin && isNfcApi)`; compose.yaml:164 `--nfc-trusted-network 172.28.90.0/28`; RelayProvisioningService.cs:64 `LocalOriginService = "http://api:5080"`; CloudflareApiClient.cs:82-86 ingress yalnız hostname+service (başlık eklemez). `NfcTrustedNetworks` yalnız HTTPS kapısında (DualScreenApplication.cs:465-466) okunur, origin kapısında okunmaz. tests/ altında NfcTrustedNetworks için davranış testi yok (yalnız DualScreenOptionsTests.cs ayrıştırma; ProductionExperienceCompositionTests.cs:172 yalnız NfcLoopbackOriginTrusted).
- Sorun: V12-QRT-005 sonrası bağlayıcı ayrı konteynerde (172.28.90.x). Kaynak adres loopback değil ve X-Alkaros-Origin başlığı yok; tünelden gelen istek onNfcOrigin=false sayılır. Sonuç: (a) /api/v1/qr ve /api/v1/nfc tünelde 404; (b) tünel hostname'i üzerinden kasa/yönetici API'leri NFC/QR allowlist'ine kısıtlanmaz (2026-09-09 relay scope hardening gerilemesi; yalnız uygulama kimlik doğrulaması kalır).
- Etki: Röle sağlanmışsa (Ayarlar > Röle) QR siparişi çalışmaz ve tüm uygulama internete açık kalır. Çalışma zamanında doğrulanmadı; doğrulanırsa Kritik.
- Düzeltme: Origin kapısında kaynak adresi NfcTrustedNetworks ile de eşle (HTTPS kapısındaki koşul); relay-internal kaynaklı istek için entegrasyon testi ekle.

**B2 · Orta · KO · Uzak yedek ve geri yükleme doğrulama işleri gönderilen compose'ta çalışmaz**
- Kanıt: compose.yaml api servisinde ALKAROS_BACKUP_DIR, yedek hacmi ve ALKAROS_OFFSITE_BACKUP_DIR yok (grep ALKAROS_BACKUP_DIR/OFFSITE compose.yaml -> 0; yalnız compose.ops.yaml:28). OffsiteBackupMaintenanceJob.cs:23,37 DisabledReason; OperationsModule.cs:40 varsayılan hedef AppContext.BaseDirectory/deployment/offsite-backup (konteyner yazılabilir katmanı). Şifreleme anahtarı env'den: EnvironmentVariableSecretProvider.cs:14 `VariablePrefix = "ALKAROS_SECRET_"`; compose'ta yalnız ENVELOPE_MASTER_KEY geçiyor.
- Sorun/Etki: api yedek dizinini görmez; iş sessizce devre dışı, off-host kopya yalnız elle. Biri ALKAROS_BACKUP_DIR verirse "uzak" kopya konteyner katmanında kalır (aynı host, --force-recreate ile silinir) ve yanlış güvence verir; anahtar sürümleri env'den gelmedikçe şifreleme de çalışmaz.
- Düzeltme: İşi açıkça devre dışı belgele ya da yedek hacmini bağla + gerçek uzak dizin zorunlu kıl (konteyner varsayılanını reddet). Runbook'a offsite-backup gizli anahtar sürümlerinin ayrıca saklanmasını ekle.

**B3 · Orta · KO · restore.sh canlı veritabanını silebilir, belge "asla dokunmaz" diyor**
- Kanıt: restore.sh:28 `TARGET_DB="${2:-${ALKAROS_RESTORE_DB:-alkaros_restore}}"`; restore.sh:71 hedef veritabanını bağlantıları zorla keserek silen komut (WITH (FORCE)); hedefin canlı ad olup olmadığı denetlenmiyor. backup-restore-runbook.md:65 "`restore.sh` **never touches the live `alkaros` database.**"
- Etki: `restore.sh <dump> alkaros` canlı veritabanını onaysız siler; operatör hatası = tüm canlı veri kaybı.
- Düzeltme: Hedef canlı ad (alkaros) ise reddet ve açık mesaj ver; doküman-kod uyumu.

**B4 · Orta · KO+ÇK · Terfi adımı bağlayıcıyı durdurmuyor**
- Kanıt: backup-restore-runbook.md "Promoting" adım 1 yalnız `docker compose stop api`; compose.yaml `connector` command `--db-url postgresql://alkaros@postgres:5432/alkaros` (DB'ye bağlı). PostgreSQL'de bağlı oturum varken ALTER DATABASE RENAME başarısız olur (ÇK, doğrulanamadı).
- Etki: Belgelenen terfi adımı bağlayıcı yüzünden hata verebilir; felaket anında belirsizlik, RTO belirsiz.
- Düzeltme: Runbook'ta connector'ı da durdur, kalan oturumları sonlandırma adımını yaz; terfiyi bir kez prova et.

**B5 · Orta · KO · PITR kurtarma yolu konteynerle birlikte kaybolur**
- Kanıt: rpo-rto-targets.md:86 ve compose.ops.yaml:11 `restore-pitr.sh /backups/base/<dir> /restore/<datadir>`; compose.ops.yaml basebackup servisinde hacimler yalnız /repo/deploy/docker, /backups, /wal-archive (grep "/restore" compose.ops.yaml -> yalnız yorum satırı). rpo-rto-targets.md "Promote ... same steps as ... Promoting a restored copy": o adımlar mantıksal DB yeniden adlandırma; fiziksel veri dizini takası yok.
- Etki: `run --rm` ile çalışan kurtarma /restore altına yazılır, konteyner çıkınca yok olur; doğrulama (pg_ctl) ve terfi uygulanamaz. 5 dk RPO'lu para/fiscal/denetim verisinin belgelenmiş kurtarma yolu fiilen tamamlanamaz.
- Düzeltme: /restore için adlandırılmış hacim bağla; doğrulama + hacim takası adımlarını yaz ve prova et.

**B6 · Orta · KO · Geri yükleme prova kanıtı bayat**
- Kanıt: evidence/V1-GOV-065/restore-drill-2026-09-01.md "current 38-migration schema"; database/MigrationComposition/order.json şimdi 196 pozisyon (python ile sayıldı). V15-BKP-002 kanıtı yalnız build/test çıktıları.
- Etki: Prova 158 migration eski (KVKK, bölümlenmiş denetim, şifreli müşteri verisi vb. sonradan geldi); güncel şema ile yedek/geri yükleme ve PITR kanıtlanmadı, RTO ölçümü yok.
- Düzeltme: Güncel şemada backup-restore + PITR provasını yeniden koş, kanıtı evidence'a koy.

**B7 · Orta · KO · Yedeklenmeyen kritik durum: Caddy iç CA, secret dosyaları, .env**
- Kanıt: compose.yaml:288 `caddy-data:/data` (iç CA kök anahtarı burada); yedek betikleri yalnız PG verisi. Runbook yalnız ENVELOPE_MASTER_KEY'i yedekleme dışı anıyor. README: "Export and install the internal root CA on every waiter device".
- Etki: Kaybedilen caddy-data hacmi yeni kök CA üretir; tüm garson telefonları ve müşteri ekranları yeniden CA kurmak zorunda (service worker/çevrimdışı kuyruk güvenli bağlam ister). db_password, admin_password, .env (ENVELOPE_MASTER_KEY, Telegram anahtarı) de yedek kapsamında değil.
- Düzeltme: Runbook'a "yedeklenmeyenler" tablosu: caddy-data (veya kök CA dışa aktarımı), secret dosyaları, .env.

**B8 · Orta · KO(+ÇK) · KVKK: geri yükleme sonrası adım çalıştırılamıyor, silinen veri geri dönebilir**
- Kanıt: grep kvkk-retention compose.yaml compose.ops.yaml -> servis yok (yalnız bir yorum satırı dosya adını anıyor); kvkk-retention-runbook.md:94 çıplak `dotnet ALKAROS.Host.dll kvkk-retention ...`. backup-restore-runbook.md "After a restore" bu adımı şart koşuyor.
- Etki: Zorunlu KVKK adımı Docker dağıtımında belgelendiği biçimde çalıştırılamaz (host'ta dotnet yok). ÇK: retention penceresi dışında, veri sahibi talebiyle yedekten sonra yapılan anonimleştirmeler geri yüklemede kaybolur (talep kayıtları da DB'dedir) ve kvkk-retention bunları yeniden uygulamaz. Aylık yedekler 12 ay, WAL arşivi elle budanıyor: silinen kişisel veri yedekten geri döner.
- Düzeltme: compose.ops.yaml'a kvkk-retention (dry-run varsayılan) servisi ekle; runbook'a yedek sonrası silme taleplerinin harici listeden yeniden uygulanması adımını yaz; yedek/WAL saklama üst sınırını KVKK penceresiyle hizala.

### 2b. Gözlemlenebilirlik, bildirim, güvenlik

**B9 · Orta · KO · Web push: sıralı/awaited gönderim, filtre dışı istisna döngüyü keser**
- Kanıt: WebPushSender.cs:138-141 `foreach (var subscription in subscriptions) { await SendOneAsync(...) }`; :186 filtre `HttpRequestException or TaskCanceledException or FormatException or ArgumentException`; MarkSuccess/Delete/MarkFailure aynı try içinde (:169,:179,:184) ama NpgsqlException filtre dışı; WebPushExperience.cs:92-95 kayıtta yalnız base64 çözümü doğrulanır (nokta eğri/uzunluk doğrulanmaz; WebPushCrypto.cs:78,82 gönderimde). İstek yolu: KitchenOperationsStore.cs:198 `await NotifyWaitersItemIsReadyAsync(...)` (bilet commit'inden sonra) -> WaiterNotificationDispatch.cs:45-47.
- Sorun/Etki: (a) Ölü uç noktalarda her biri 10 sn zaman aşımı (WebPushExperience.cs:31) mutfak "hazır" isteğini N x 10 sn geciktirir. (b) Filtre dışı istisna (DB hatası; eğri dışı ama base64 geçerli p256dh için olası CryptographicException, tür doğrulanamadı) döngüyü keser: kalan cihazlar bildirim almaz, hata istek yoluna çıkar ve bilet commit edilmişken 500 döner; bozuk satır silinmez, her yayında tekrarlar. Sınıf yorumu (WebPushSender.cs:11-15) "hiçbir hata çağıranı bozmaz" diyor.
- Düzeltme: Gönderimi paralel + sınırlı süreli yap ya da arka plana al; her aboneliği kendi try/catch'inde (log + failure_count) yalıt; N başarısızlıktan sonra sil; kayıtta p256dh nokta doğrulaması.

**B10 · Orta · KO · Push abonelikleri sunucu tarafında temizlenmiyor, tüm abonelere yayın**
- Kanıt: PushSubscriptionStore.cs:64-67 GetAllAsync, `SELECT ... FROM notifications.push_subscriptions;` (kullanıcı aktifliği yok); grep push_subscriptions src/ yalnız PushSubscriptionStore.cs; temizlik yalnız istemci push.js:138-146 unsubscribePush (en iyi çaba, boş catch).
- Etki: Ayrılan/pasifleştirilen personelin telefonu, abonelik silinmedikçe BroadcastAsync ile misafir sipariş ve masa bildirimlerini almaya devam eder (kullanıcı pasifleştirme, KVKK anonimleştirme veya oturum iptali aboneliği silmez). Masa no/ürün adı sızıntısı.
- Düzeltme: Yayında aktif kullanıcıya JOIN; kullanıcı pasifleşince/anonimleşince abonelikleri sil; failure_count eşiğiyle ölü satırları temizle.

**B11 · Orta · KO · VAPID özel anahtarı düz metin, rotasyon yok, subject sabit (mimari ajan B-16 doğrulandı)**
- Kanıt: PushSubscriptionStore.cs:185 `INSERT INTO notifications.vapid_keys (single_row, public_key, private_key, subject)` düz metin; migration 096 `private_key TEXT NOT NULL`. WebPushSender.cs:54 sabit `"mailto:destek@alkaros.local"`, DI'dan değiştirilemez.
- Etki: Özel anahtar her pg_dump, base backup ve şifresiz WAL arşivinde dolaşır; mevcut ENVELOPE şifreleme altyapısı kullanılmıyor. Döndürme yolu yok (yorum: "nothing here ever rewrites"): sızıntıda çare tabloyu elle silmek ve tüm cihazları yeniden abone etmek. Subject geçersiz alan (.local); push servislerinin reddedip etmediği doğrulanamadı. Etki düşük: yalnız bildirim taklidi.
- Düzeltme: Özel anahtarı ENVELOPE ile sar; rotasyon komutu; subject'i yapılandırmaya taşı.

**B12 · Orta · KO · Yardım çağrısı: alıcı yoksa sessiz kayıp, cooldown yanlış engeller**
- Kanıt: HelpRequestExperience.cs:82 `store.RaiseAsync` (commit), sonra :110-114 `await hub.Clients.Group(...).SendAsync(...)`, ardından Results.Ok. Yalnız POST var (grep MapGet src/Host/Experience/HelpRequests -> 0, bu oturumda çalıştırıldı); iletim sayısı/onay yok; HelpRequestStore.cs:24 `Cooldown = 2 dk`.
- Etki: Hiçbir yönetici bağlı değilse veya SignalR gönderimi hata verirse garson "başarılı" yanıt alır (ya da commit'ten sonra 500) ve satır cooldown'u başlatır: aynı masa için 2 dk "az önce zaten yardım çağrıldı" (429). Geç bağlanan yönetici kaçırılan çağrıyı göremez (okuma uç noktası yok). Geçici yol: yüz yüze.
- Düzeltme: Bağlı alıcı sayısı 0 ise garsona açık uyarı ve cooldown'u başlatma; yönetici bağlanınca bekleyenleri getiren uç nokta; gönderim hatasını logla.

**B13 · Orta · KO · Sessiz catch: tahsilat sonrası hesap kapatma hatası günlüğe düşmüyor**
- Kanıt: DualScreenApplication.Payments.cs:96 `catch (Exception exception) when (exception is not OperationCanceledException)` -> `return false;` (günlük yok); :89 iç catch; SignalRCashierQueueAnnouncer.cs:30 aynı kalıp; DualScreenApplication.cs:242 `catch (Npgsql.NpgsqlException)` -> `firstSeen = null;`.
- Etki: TryCloseBillAsync (6 çağrı yeri) hesap kapatma/sipariş tamamlama hatasını yutar, günlük/alarm bırakmaz; hesap açık kalır, neden izlenemez. Kasa kuyruğu bildirimi ve performans bütçesi sürüm kaydı da sessiz (CLAUDE.md "boş catch" borç kalıbına yakın).
- Düzeltme: LoggerMessage ile Error/Warning logla (billId, TraceIdentifier) ve ErrorEvent üret.

**B14 · Orta · KO · Güvenlik başlıkları: röle statik sayfaları başlıksız; HTML'de CSP/HSTS yok (HSTS/CSP kararı bilinen)**
- Kanıt: DualScreenApplication.cs:629,645 UseStaticFiles (/qr, /nfc) ve /nfc geri dönüşü, güvenlik başlıkları ara katmanından önce (:660 UseRouting, :664 `Content-Security-Policy`). Caddyfile:30-37 başlık kümesi yalnız nosniff, X-Frame-Options DENY, Referrer-Policy, Permissions-Policy; CSP ve HSTS yok (gerekçe Caddyfile:20-29, V1-RMD-335).
- Etki: (a) İnternete açık röle yolunda sunulan QR/NFC statik sayfaları API'nin CSP/nosniff/Referrer-Policy/Cache-Control başlıklarından yoksun (Caddy bu yola girmez). (b) Caddy'nin sunduğu tüm HTML kabukları (PosTerminal, WaiterPwa, Cashier) CSP'siz; API'nin CSP'si JSON yanıtlarında etkisiz. HSTS bilinçli yok (iç CA). (b) bilinen: Caddyfile yorumu, V1-RMD-335, kasıtlı karar.
- Düzeltme: Başlık ara katmanını statik sunumdan önceye taşı; Caddy'de HTML için raporlama-modlu CSP ile başla.

**B15 · Orta · KO · Doküman sapması + sır: ana anahtar ve Telegram anahtarı compose ortamında**
- Kanıt: compose.yaml:126 ve :224 (connector) `ALKAROS_SECRET_ENVELOPE_MASTER_KEY` ortam değişkeni olarak; :201-202 `Monitoring__Telegram__BotKey`. production-go-live-checklist.md:44 "no secret is in the Compose environment or in logs".
- Etki: Ana şifreleme anahtarı ve Telegram bot anahtarı konteyner ortam değişkeni (docker inspect ile docker grubundakiler okur); DB parolası ise Docker secret. Belge tersini söylüyor.
- Düzeltme: İkisini de secret/dosya (_FILE) olarak geçir ya da belgeyi düzelt.

**B16 · Orta · KO · .dockerignore .env ve .git'i dışlamıyor**
- Kanıt: .dockerignore:28 yalnız deploy/docker/db_password ve admin_password; grep "env" .dockerignore -> yalnız o yorum; Dockerfile:18 `COPY . .` (backend-build; test hedefi bundan türer). Kök dizinde .env var (içeriği açılmadı).
- Etki: .env (ENVELOPE_MASTER_KEY, Telegram anahtarı) ve .git build bağlamına girer; test hedefli build (compose.test.yaml) imajında ve yerel önbellekte kalır. Son api imajına geçmez (yalnız /out, database, manifest kopyalanır). Test imajı/önbellek paylaşılırsa anahtar sızar.
- Düzeltme: .dockerignore'a .env, .env.*, *.secret.*, .git, alkaros-root.crt, *.pfx ekle.

**B17 · Orta · ÇK · Kök kullanıcı konteynerler, süper kullanıcı DB rolü**
- Kanıt: Dockerfile'da USER satırı yok (grep ile 0 sonuç); compose'ta cap_drop/read_only/no-new-privileges yok (grep 0); compose.yaml:42 `POSTGRES_USER: alkaros` (imaj bu kullanıcıyı süper kullanıcı yapar) ve api/connector/migrate aynı rolle bağlanır; RestoreVerificationMaintenanceJob uygulama rolüyle veritabanı oluşturur/siler.
- Etki: api/connector/web kök kullanıcıyla, uygulama süper kullanıcı DB rolüyle çalışır: tek bir SQL/RCE kusuru tüm küme ve host için sonuç doğurur. Çalışma zamanında doğrulanmadı.
- Düzeltme: Uygulama için süper kullanıcı olmayan rol (migrate ayrı), imajda kök olmayan kullanıcı, cap_drop ALL.

**B18 · Orta · KO · Watchdog Caddy'yi atlıyor; Telegram boşsa kesinti uyarısı kimseye gitmiyor**
- Kanıt: compose.yaml:203 watchdog `--health-url http://api:5080/health/ready`; :201-202 Telegram anahtarı varsayılan boş; OutageWatchdogHost yalnız başlangıçta `telegramConfigured=` yazar. README TLS: "If Caddy is down the stack is down". go-live-checklist §4 "external watchdog ... pending" (watchdog zaten gönderiliyor: doküman sapması).
- Etki: Caddy (TLS/ön yüz) çökerse alarm yok; cihazlar bağlanamaz ama api sağlıklı görünür. Telegram yapılandırılmadan kurulan yığında uyarı yalnız /data/outage-buffer.log dosyasına yazılır.
- Düzeltme: Watchdog web üzerinden de yoklasın; Telegram boşsa görünür uyarı; checklist'i güncelle.

**B19 · Orta · KO · InvalidOperationException 409 olarak sınıflanıyor, günlüğe düşmüyor**
- Kanıt: ApiErrorCatalog.cs:126-127 ve :487-489 `InvalidOperationException => 409 ... CONCURRENCY_CONFLICT / ROLE_MANAGEMENT_CONFLICT`; ApiExceptionHandler.cs:49-50 yalnız `status >= 500` loglar.
- Etki: Katalog ve rol alanlarında her InvalidOperationException (programlama/DI hatası dahil) 409 ve "başka bir işlem tarafından değiştirildi" mesajına çevrilir, günlüğe düşmez; gerçek sunucu hatası çakışma gibi görünür.
- Düzeltme: Çakışmayı özel istisna türüyle ayır; genel InvalidOperationException 500'e gitsin.

### 2c. Düşük önemli bulgular

**B20 · Düşük · KO · /health/ready DB kapalıyken 500; belge 503 ve "self-heal" diyor (Doküman sapması)**
- Kanıt: DualScreenStore.cs:37-42 CheckReadyAsync; ApiErrorCatalog.cs:609 `PostgresException => (503, ...)`, bağlantı reddi NpgsqlException Default'ta yok -> :610 `_ => (500, "INTERNAL_ERROR"...)`. go-live-checklist.md:152 "... / 503", :154 "docker self-heal".
- Etki: DB tamamen kapalıyken her çağrıda (konteyner kontrolü 10 sn, watchdog, sentetik yoklama) 500 ve Error düzeyinde yığın izli günlük (gürültü). restart: unless-stopped sağlıksız konteyneri yeniden başlatmaz (Docker davranışı, ÇK): "self-heal" iddiası yanlış.
- Düzeltme: NpgsqlException -> 503; health yolunu 5xx loglamasından ayır.

**B21 · Düşük · ÇK · stop_grace_period yok**
- Kanıt: compose.yaml'da stop_grace_period yok (grep 0); grep ShutdownTimeout src/Host -> 0 (src genelinde yalnız CloudflaredProcessFactory.GracefulShutdownTimeout; .NET varsayılanı).
- Etki: Docker varsayılan 10 sn sonra SIGKILL gönderir; uçuştaki ödeme/outbox işleri uzarsa yarıda kesilir.
- Düzeltme: stop_grace_period (örn. 40s) ver, zarif kapatmayı prova et.

**B22 · Düşük · KO · Operatör mesajları İngilizce, göç hatasında runbook yönlendirmesi yok, göçte kilit yok**
- Kanıt: Program.cs:141 Console.Error.WriteLine FATAL ile tam istisna; HostComposition.cs:106 "Migration failed at position [..]"; Program.cs:56,82,95,108 ve :445 İngilizce (yalnız audit-disposal Türkçe, :519-521). grep advisory src/Host/Composition/Migrations/*.cs -> 0.
- Etki: Göç hatasında operatöre Türkçe mesaj ve runbook yönlendirmesi yok; iki eşzamanlı migrate koşusunda kilit yok (compose'ta tek koşu). FATAL tam yığını yazar (parola içermez; Redact yalnız psql çıktısında).
- Düzeltme: Göç hatası için docs yönlendirmeli Türkçe özet; migrate öncesi pg_advisory_lock.

**B23 · Düşük · KO · Parola içeren kayıt türü ve parolasız PFX**
- Kanıt: HostDatabaseConnection.cs:10 `public sealed record HostDatabaseConnection(string ConnectionString)`: parola dahil bağlantı dizisi; kayıt türünün otomatik ToString() tüm özellikleri basar. DualScreenTls.cs:109-123 self-signed PFX parolasız, content-root altında tls dizini; compose'ta --self-signed-host yok.
- Etki: Biri örneği logladığında DB parolası günlüğe düşer (şu an kullanım yok). PFX yolu kullanılmıyor; etkinleşirse özel anahtar parolasız konteyner katmanında.
- Düzeltme: ToString'i maskele; PFX'i parolayla yaz.

**B24 · Düşük · KO · Dev bindings ve Caddy prod önbellek/erişim günlüğü**
- Kanıt: compose.dev.yaml:29 ve :33 (5433 ve 5080 portları tüm arayüzlere bağlanır). Caddyfile (üretim) erişim günlüğü (log) ve index.html için Cache-Control içermiyor; Caddyfile.dev:37 dev'de no-cache koyuyor.
- Etki: Dev kapsamı LAN'a açılırsa parolalı Postgres ve düz HTTP api erişilebilir (README "Never deploy"). Üretimde bayat kabuk riski ve proxy erişim günlüğü yok.
- Düzeltme: Dev'de 127.0.0.1 öneki; prod'da index için Cache-Control no-cache.

**B25 · Düşük · ÇK · archive_command atomik değil, arşiv hatası alarmı yok**
- Kanıt: postgresql.tuned.conf:73 archive_command: test ! -f ile hedef yok denetimi, ardından nihai ada doğrudan cp; rpo-rto-targets.md "check the volume's free space at least weekly" (otomatik arşiv hatası alarmı yok; pg_stat_archiver izlenmiyor).
- Etki: Yarım kalan cp eksik segment bırakırsa sonraki denemeler hedef var denetimi yüzünden sonsuza dek başarısız olur, WAL birikir; disk dolması / PITR zinciri kopması.
- Düzeltme: Geçici dosyaya yaz + mv; pg_stat_archiver.failed_count alarmı.

**B26 · Düşük · KO · notifications.* tablolarında saklama süresi yok**
- Kanıt: grep help_requests/push_subscriptions docs/operations/data-housekeeping.md -> 0; Program.cs:285-306 housekeeping yalnız idempotency_keys + device_sessions; 096/101 migration'larında saklama süresi yok.
- Etki: notifications.help_requests (personel kimliği) ve push_subscriptions zamanla büyür, housekeeping/KVKK kapsamında değil.
- Düzeltme: Saklama süresi + süpürme ekle.

**B27 · Düşük · KO · ALKAROS_KITCHEN_STATION_ID eksikliği başlangıçta değil ilk siparişte patlıyor**
- Kanıt: DualScreenApplication.cs:305 "... is required before cashier order submission is enabled." tembel IOrderSubmissionDispatcher fabrikasında; compose.yaml varsayılan kitchen-main verir, Dockerfile ENTRYPOINT'i ve tek başına çalıştırma vermez.
- Etki: Değişken eksikse api sağlıklı başlar, ilk siparişte 500. Düşük (compose güvenli).
- Düzeltme: Başlangıçta doğrula.

**B28 · Düşük · KO · Doküman sapmaları (karar verilmedi)**
- (a) deploy/docker/README.md:69-71 röle bağlayıcısı "same container as api ... loopback http://localhost:5080" (kod: ayrı konteyner, http://api:5080); (b) Dockerfile:135 yorumu network_mode "service:api"; (c) ProductionExperienceCompositionTests.cs:150 yorumu http://localhost:5080; (d) deployment-compatibility-matrix.md "Minimum .NET 10.0.0, Windows 11 server" vs src/Host/ALKAROS.Host.csproj:4 net8.0 ve aspnet:8.0-alpine Linux imajı; (e) backup-restore-runbook.md "scheduled automatic restore drills are out of scope" vs RestoreVerificationMaintenanceJob.cs (günlük); (f) go-live-checklist §4 "to wire" watchdog vs compose watchdog servisi.


## 3. Temiz bulunan alanlar (kontrol edildi)

- Çerezler: DualScreenApplication.Endpoints.cs:609-616 HttpOnly, Secure (HTTPS), SameSite=Strict, Path=/, Expires; oturum 8 sa.
- Sır dosyaları: git check-ignore -v deploy/docker/db_password, admin_password, .env, alkaros-root.crt yok sayılıyor; git ls-files .env alkaros-root.crt boş. DB parolası Docker secret ile; psql PGPASSWORD env ile, argv'de değil; PsqlScriptRunner.Redact parola/URL maskeler.
- Caddy: X-Alkaros-Origin istemciden soyuluyor (ana vhost), display/nfc vhost'ları ayrı; nfc vhost'ta respond 404 kapanışı; admin off; init true ile zombi süreç düzeltmesi; her vhost'ta nosniff, X-Frame-Options DENY, Referrer-Policy, Permissions-Policy.
- İmaj sabitleme: postgres, caddy, dotnet, node digest ile sabit; dotnet restore --locked-mode; cloudflared sha256 doğrulamalı.
- pg_hba samenet + scram-sha-256; yedek/geri yükleme sağlama toplamı kapısı (restore.sh çıkış kodları 3/4), bozuk dosya reddi.
- Göç yürütücüsü: tek dosya = tek işlem (single-transaction + geçmiş satırı), sağlama toplamı ve bitişik önek doğrulaması, bildirilmemiş SQL dosyası reddi, 5 dk zaman aşımı + süreç ağacı öldürme, geçmiş tablo şeması sıkı doğrulanır.
- Hata zarfı: 5xx için ham istisna mesajı yok (Türkçe sabit mesaj), TraceIdentifier korelasyonu, 5xx günlükleniyor; HTTPS kapısı Türkçe mesajlı; hız sınırlayıcı Türkçe 429.
- Migration 096/100/101/119/038: down betiği mevcut, FK/CHECK kısıtları var; rollback yalnız en son uygulanan pozisyon için (yürütücü zorluyor).
- WebPush: yalnız https uç noktası kabul (SSRF'e karşı), sahibine özel silme, hız sınırı.
- Dilimde TODO/NotImplementedException/#pragma/Fake/Simulator: 0 (grep).

## 4. Doğrulanamayanlar

- B1 çalışma zamanı etkisi (QR 404 ve kasa API'sinin tünelden erişilebilirliği): Docker/compose komutu yasak; yalnız kod ve yapılandırma okundu.
- B9: eğri dışı p256dh için fırlatılan istisna türü (CryptographicException varsayımı).
- B11: push servislerinin .local VAPID subject'ini reddedip reddetmediği.
- B4/B17/B21/B25: PostgreSQL yeniden adlandırma sırasında bağlı oturum hatası, süper kullanıcı varsayılanı, Docker varsayılan 10 sn bekleme ve archive_command davranışı Çıkarım (ürün belgesi/web yok).
- HttpClientFactory'nin push uç nokta URL'sini Information düzeyinde günlüğe yazıp yazmadığı ve üretim günlük düzeyi (appsettings yok).
- .NET 8 çalışma zamanının destek bitiş tarihi (B28d ile ilgili) web olmadan doğrulanamadı.
- Host.Observability testinin yarım kalması: altyapı (PG bağlantısı); test koşulmadı, bulgu sayılmadı.

## 5. Ek düzeltmeler ve özet (inceleme sonrası)

Kontrol sayısı: 53/53 kalem kontrol edildi (M=53): 38 tam okuma, 15 operasyonel örüntü taraması (catch, günlük, TODO/Fake, çerez, health).

Çalıştırma kayıtları (evidence/V1-RMD-599/runs/exit-codes.txt, yeniden koşulmadı): ApiConventions exit=0 (5 geçti), ModuleBoundaries exit=0 (10 geçti), Host-Errors exit=0 (180 geçti), Host-MigrationComposition exit=0 (196 geçti, 17 dk 36 sn), Host-Observability exit=1: günlük 939 satır, hiçbir "Başarısız" özet satırı yok, çalışma test_çalıştırması satırında yarım kesilmiş; doğrulanamadı (altyapı), bulgu değil. Host-Errors ve MigrationComposition yeşil olması B19/B20'yi çürütmez: testler 5xx/503 eşlemesini ve DB-kapalı health yanıtını kapsamıyor (kapsama ayrıca doğrulanmadı).

B1 sertleştirme: src/Apps ve src/Clients JS/TS içinde Alkaros-Origin için sonuç yok (istemci başlığı kendisi koymuyor). evidence/V12-QRT-005/closure-evidence.txt yalnız HTTPS_REQUIRED 400 düzeltmesini anlatır, tünel üzerinden QR turu içermez. Commit 2e014e47 diff'inde NfcTrustedNetworks yalnız HTTPS kapısına eklenmiş (satır 78), origin kapısı değişmemiş. Ek KO gerçeği: röle alt ağı 172.28.90.0/28, --trusted-network 172.16.0.0/12 içinde; bu yüzden tünel trafiği güvenilir vekil sayılır (Caddy başlık soyma tünelde uygulanmaz). Tünelden istemcinin hangi başlıkları yollayabildiği Çıkarım, doğrulanamadı. B1 kanıtında alıntı: DualScreenApplication.cs:560 "var onNfcOrigin = nfcOriginPorts.Contains(context.Connection.LocalPort)" ve :582 "if (!onNfcOrigin && isNfcApi)".

B9 niteleyici: bildirim yolu yalnız mutfak canlı eşitleme açıkken tetiklenir (KitchenOperationsStore.cs:197 liveSyncEnabled koşulu); QR/NFC bekleyen sipariş duyurusu (SignalRPendingOrderAnnouncer) bundan bağımsızdır.

B6 niteleyici: evidence/V15-BKP-001 ve V15-BKP-002/README.md bu turda açılmadı; "yalnız build/test çıktısı" ifadesi dosya adlarına dayanır, içerik doğrulanamadı.

Park edilmiş görevler: plan/v1/remediation/V1-RMD-474, 519, 572, 588 açıldı; içerikleri çevrimiçi sipariş indirimi/tahsilat, kalem bazlı kısmi iade, tıklama gecikmesi ve yemek kartı faturalama kararıdır; bu raporun bulgularıyla örtüşme yok. Kayıt defteri docs/engineering/remaining-gaps-register.md:41 ("Şifreli dış yedek var; sürekli arşiv yok") B2/B5 ile ilişkili: bilinen (remaining-gaps-register). SKILL dosyalarında (alkaros-api-endpoint, boundary-event, database-migration, frontend-screen) "bilinen/sapma" listesi bulunamadı (grep 0); bu yüzden B15, B18, B20, B28 "(bilinen)" etiketi almadı.

### Bulgu özet tablosu

No | Önem | Kanıt | Yer | Başlık
B1 | Yüksek | KO | DualScreenApplication.cs:560,582 | Röle origin kapısı bağlayıcı ağını tanımıyor
B2 | Orta | KO | compose.yaml, OffsiteBackupMaintenanceJob.cs:23 | Uzak yedek işleri devre dışı (bilinen kısmen)
B3 | Orta | KO | restore.sh:28,71 | Canlı DB silinebilir
B4 | Orta | KO+ÇK | backup-restore-runbook.md | Terfi adımı bağlayıcıyı durdurmuyor
B5 | Orta | KO | rpo-rto-targets.md:86 | PITR çıktısı kalıcı hacimde değil
B6 | Orta | KO | V1-GOV-065 kanıtı | Prova 38 migration, şimdi 196
B7 | Orta | KO | compose.yaml:288 | Caddy CA ve secret dosyaları yedeksiz
B8 | Orta | KO+ÇK | compose.ops.yaml | kvkk-retention servisi yok, silinen veri dönebilir
B9 | Orta | KO | WebPushSender.cs:138,186 | Push sıralı, istisna filtresi dar
B10 | Orta | KO | PushSubscriptionStore.cs:64 | Ayrılan personele push
B11 | Orta | KO | PushSubscriptionStore.cs:185 | VAPID özel anahtarı düz metin
B12 | Orta | KO | HelpRequestExperience.cs:82,110 | Yardım çağrısı sessiz kayıp
B13 | Orta | KO | DualScreenApplication.Payments.cs:96 | Sessiz catch
B14 | Orta | KO | DualScreenApplication.cs:629,664 | Röle statik sayfaları başlıksız, HTML CSP yok
B15 | Orta | KO | compose.yaml:126,201 | Sır ortam değişkeninde, belge çelişkili
B16 | Orta | KO | .dockerignore:28 | .env build bağlamında
B17 | Orta | ÇK | Dockerfile, compose.yaml:42 | Kök kullanıcı, süper kullanıcı DB rolü
B18 | Orta | KO | compose.yaml:203 | Watchdog Caddy'yi atlıyor
B19 | Orta | KO | ApiErrorCatalog.cs:126,487 | InvalidOperationException 409
B20 | Düşük | KO | ApiErrorCatalog.cs:610 | Health 500, belge 503
B21 | Düşük | ÇK | compose.yaml | stop_grace_period yok
B22 | Düşük | KO | Program.cs:141, HostComposition.cs:106 | İngilizce operatör mesajı, migrate kilidi yok
B23 | Düşük | KO | HostDatabaseConnection.cs:10 | Parola içeren kayıt
B24 | Düşük | KO | compose.dev.yaml:29,33 | Dev port bağlama
B25 | Düşük | ÇK | postgresql.tuned.conf:73 | archive_command atomik değil
B26 | Düşük | KO | data-housekeeping.md | notifications saklama yok
B27 | Düşük | KO | DualScreenApplication.cs:305 | İstasyon kimliği geç doğrulanıyor
B28 | Düşük | KO | README.md:69 ve diğerleri | Doküman sapmaları
