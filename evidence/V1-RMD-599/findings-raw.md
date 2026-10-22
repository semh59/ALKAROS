# V1-RMD-599 — Tekilleştirilmiş ham bulgular (SHA d1dc86b6)

Durum: hepsi DOĞRULANMAMIŞ; doğrulayıcı çıktısı `verifier/` altındadır. Kimlik ön eki `V1-RMD-599-`. Kaynak biçimi `ajan:kimlik`.

## Sayılar
- Ham 279 (api 21, backend 38, dokuman 18, entegrasyon 27, frontend 18, güvenlik 25, mevzuat 11, operasyon 28, performans 16, mimari 29, test 12 + 19 mutant, ux 18, veritabanı 18).
- Tekil 143 bulgu (130 hata, 13 doküman sapması) + 10 fırsat.
- Önem (143): Kritik 0, Yüksek 7, Orta 65, Düşük 71. Semih sorusu 8 bulgu / 9 soru.

## Yüksek
- **B001** Tünel kaynak kapısı bağlayıcı alt ağını tanımıyor; tünelden NFC/QR dışı API izolasyonsuz ulaşılır, QR/NFC 404 verir. `DualScreenApplication.cs:560-587`, `compose.yaml:164`. Kaynak: guvenlik:G-01, entegrasyon:E-01, operasyon:B1.
- **B002** Yardım çağrısı kaybolur (kalıcı kayıt yok, alıcı yoksa 200, bekleme süresi yeniden çağırmayı engeller). Kaynak: backend:B1, entegrasyon:E-05, frontend:F9, ux:B-10, operasyon:B12.
- **B003** Müşteri ekranı eşleştirme, ekran iptali, `session/current`, `orders/active` için sunucu testi yok. Kaynak: api:B-04, test:T-01, T-02, T-03.
- **B004** Müşteri ekranı ağ/5xx/429 sonrası kendiliğinden toparlanmıyor. Kaynak: frontend:F1, F2.
- **B005** Pasifleştirilen personelin cihazı bildirim almaya devam ediyor (S6). Kaynak: mevzuat:5, guvenlik:G-06, operasyon:B10.
- **B006** Müşteri vergi kimliği için erişim/denetim izi yok, sunucu sabit Manager rolüyle çözüyor (S5). Kaynak: mevzuat:9, guvenlik:G-09.
- **B007** Bildirim tabloları KVKK envanterinde ve anonimleştirmede yok (S1, S2, S3). Kaynak: mevzuat:1, 7.

## Orta
- B008 WebPush abonelik adresi SSRF (api:B-01, backend:B6, guvenlik:G-04, entegrasyon:E-14, mimari:B-05)
- B009 Push gönderimi sıralı ve istek yolunda (performans:P-01, mimari:B-10, entegrasyon:E-03, api:B-02)
- B010 Push istisna filtresi dar (backend:B2, performans:P-02, operasyon:B9)
- B011 Push abonelik sayısı sınırsız, failure_count okunmuyor (performans:P-03, backend:B3, entegrasyon:E-02, veritabani:2)
- B012 VAPID özel anahtarı düz metin, rotasyon yok (S4) (guvenlik:G-05, mevzuat:8, operasyon:B11, mimari:B-16, veritabani:13, entegrasyon:E-16)
- B013 Bildirim kanalları yalıtılmamış (backend:B4, entegrasyon:E-04, performans:P-09)
- B014 Housekeeping/saklama eksikliği (S1) (backend:B20, entegrasyon:E-18, performans:P-11, operasyon:B26, veritabani:1, mevzuat:2,3,4,6, guvenlik:G-10)
- B015 Ekran oturumu 12 saat mutlak (backend:B8, entegrasyon:E-06, frontend:F5, ux:B-7)
- B016 Hub bağlantıları iptal/pasifleştirme sonrası açık kalıyor (backend:B10,B24, entegrasyon:E-08, guvenlik:G-14, mimari:B-21)
- B017 İkinci ekran eşleşince eski ekran yetkili kalıyor (backend:B9)
- B018 Tek çakışma kodu/mesajı ve eşleştirme hataları ayırt edilemiyor (api:B-07, backend:B16, frontend:F6, mimari:B-13, ux:B-1)
- B019 Hub yeniden bağlanma ~9 sn sonra bırakılıyor (frontend:F4, entegrasyon:E-07, performans:P-13)
- B020 Snapshot yanıt sırası yarışı (frontend:F3)
- B021 Veritabanı hata eşlemesi 500/503, /health/ready (api:B-05, backend:B18, entegrasyon:E-23, performans:P-04, operasyon:B20)
- B022 InvalidOperationException 409'a çevriliyor (api:B-06, backend:B17, operasyon:B19)
- B023 Fatura kapatma hatası yutuluyor (api:B-12, backend:B13, entegrasyon:E-11, operasyon:B13, mimari:B-12)
- B024 Hata kataloğu test boşlukları, hayatta kalan mutantlar (test:T-04,T-05,T-06,T-12)
- B025 Modül kenar testi 11 ad alanıyla sınırlı (mimari:B-04, test:T-07,T-11)
- B026 Kompozisyon testi dar (mimari:B-08, test:T-08)
- B027 Hub/push hata yolu testleri yok (test:T-09, dokuman:D-18, entegrasyon:E-22, backend:B35)
- B028 CustomerDisplay istemci testleri yalnız mutlu yol (frontend:F8)
- B029 WaiterPwa push/yardım/sw.js testsiz (frontend:F16)
- B030 Tünel statik sayfaları güvenlik başlıksız (guvenlik:G-02, operasyon:B14)
- B031 Ekran koruyucu yükleme yetki filtresinden önce 25 MB okutuyor (guvenlik:G-07)
- B032 Yönetim/mutfak/fatura gruplarında hız sınırı yok (guvenlik:G-08)
- B033 Giriş hız sınırı tek kova (guvenlik:G-13)
- B034 qr-order hız sınırı istemci başlığına bağlı (performans:P-05, guvenlik:G-24)
- B035 Terminal kimliğini istemci seçiyor (guvenlik:G-03)
- B036 Süper kullanıcı DB hesabı, root konteyner (guvenlik:G-12, operasyon:B17)
- B037 Uzak yedek işleri compose'ta çalışmaz (operasyon:B2)
- B038 restore.sh canlı DB'yi silebilir (operasyon:B3)
- B039 Terfi adımı connector'ı durdurmuyor (operasyon:B4)
- B040 PITR çıktısı kalıcı hacimde değil (operasyon:B5)
- B041 Geri yükleme provası 38 göçlük şemaya ait (operasyon:B6)
- B042 CA, secret ve .env yedeksiz (operasyon:B7)
- B043 kvkk-retention servisi yok, silinen veri geri dönebilir (S7) (operasyon:B8)
- B044 Ana anahtar ve Telegram anahtarı ortam değişkeninde (operasyon:B15)
- B045 .dockerignore .env/.git dışlamıyor (operasyon:B16)
- B046 Watchdog Caddy'yi atlıyor (operasyon:B18)
- B047 Göç dosyalarında CRLF/karışık satır sonu, sağlama toplamı ham bayt (veritabani:5, backend:B29)
- B048 Program.cs beş modül şemasına ham yazıyor (mimari:B-01)
- B049 ManagementSessionLookup identity'ye ham UPDATE (mimari:B-02)
- B050 DualScreen masa durumunu ham SQL ile yazıyor, 11 yer (mimari:B-03)
- B051 Kasa hareketi/kapanış yarışı (backend:B15, mimari:B-11)
- B052 Aktif oturum ucu 5000 satır çekiyor (performans:P-06)
- B053 İkinci havuz bağlantısı (backend:B23, performans:P-07)
- B054 Ekran koruyucu ETag/boyut CHECK (veritabani:4, performans:P-10, mimari:B-27)
- B055 Makbuz sorgusu indeksi (veritabani:3)
- B056 Sipariş satırı birleştirme imzası (backend:B12)
- B057 Snapshot tutarlılığı ve modifiyer (backend:B11, ux:B-18)
- B058 Kart tahsilatı yer tutucu, bilinen V13-HUG-001 (entegrasyon:E-26)
- B059 pushsubscriptionchange işleyicisi yok (entegrasyon:E-12)
- B060 OrderItemReady yeniden bağlanınca tekrar oynatılmıyor (entegrasyon:E-13)
- B061 OrderItemReady masa bilgisi yok, tüm cihazlara gidiyor (frontend:F14, backend:B25)
- B062 Sabit bildirim tag'i (backend:B5)
- B063 Push açma akışı yönlendirmiyor, çıkışta abonelik siliniyor (frontend:F11,F12,F13, ux:B-14)
- B064 Elle işlerde aktör kaydı yok (guvenlik:G-11)
- B065 İstemci ham sunucu mesajı basıyor (ux:B-6,B-17)
- B066 Ekran talimatı ile kasa etiketi uyuşmuyor (ux:B-5)
- B067 Yardım uyarısı sessiz (ux:B-11)
- B068 Bekleyen hesap kasada sessiz (ux:B-12)
- B069 Eşleştirme hatası diyalogda görünür mü belirsiz (frontend:F7)
- B070 İptal onaysız, "iptal edildi" mesajı yanıltıcı (ux:B-3,B-4)
- B126 Kullanılmış eşleştirme kodu temizlenmiyor (ux:B-2)

## Düşük
B071 Push upsert sahiplik devri · B072 notificationclick data.url yok sayıyor · B073 429'da Retry-After yok · B074 Eşleştirme sayacı tutarsız · B075 /hubs/* tünel izolasyonu ve hız sınırı dışında · B076 CSP/HSTS eksik (bilinen V1-RMD-335) · B077 DB bağlantısı TLS'siz · B078 Okuma uçları izin aramıyor · B079 Sağlık probe'u ex.Message yazıyor · B080 Online platform URL'sinde ana makine kısıtı yok · B081 "Kart çekilmedi" tek kişiyle kapanıyor (S8) · B082 TableId boş Guid · B083 Catch-all 404/405 · B084 Gövdesiz NotFound · B085 Manuel çözüm iki farklı kod · B086 Olmayan oturumda expected-cash 200 · B087 İstemci katalog imlecini izlemiyor · B088 terminalId istemciden isteniyor · B089 İngilizce sızıntı · B090 Ölü kod · B091 Sipariş no UTC · B092 ArgumentException 404'e çevriliyor · B093 Açılışta sync-over-async · B094 KITCHEN_STATION geç doğrulanıyor · B095 Bağlantı dizesi unescape yok · B096 Tutar biçimi kültüre bağlı · B097 Uygulama saati ile DB saati karışıyor · B098 Singleton'a yakalanan HttpClient · B099 RefundIntentsModule kayıtsız (bilinen V13-HUG-003) · B100 Ölü TryAdd (bilinen V1-RMD-686) · B101 Modüller arası çift kayıt · B102 Fişte ödeme yöntemi "Manual" · B103 --db-url parolası reddedilmiyor · B104 Giriş ucunda iş mantığı · B105 PostgresOrderRepository DI dışı · B106 help_requests FK CASCADE · B107 Göç kimliği 999 tavanı, sabit PhaseBMax · B108 Göç koşucusunda advisory lock yok · B109 Bekleyen devir notu tekilliği şemada yok · B110 COLLATE "C" Türkçe sıralama · B111 Done görevlerde kanıt/ileri-geri göç testi yok · B112 Göç şeması küçük tutarsızlıkları · B113 Housekeeping tek işlemde sınırsız silme · B114 Müşteri ekranı her 5 sn UPDATE · B115 Cihaz kaldırmada silme yolu yok · B116 HostDatabaseConnection.ToString() parolayı basar · B117 Sertifika PFX (parolasız, izin, yenileme) · B118 stop_grace_period yok · B119 Dev port bağlama ve Caddy önbellek · B120 archive_command atomik değil · B121 "Paying" durumu üretilmiyor · B122 Table-draft siparişleri müşteri ekranında yok (S9) · B123 Sabit bekleme kullanan testler · B124 Lisans envanteri (bilinen V1-RMD-596) · B125 Npgsql doğrudan referansı yok · B127 Dokunma hedefi küçük · B128 Backdrop a11y uyarıları (oxlint) · B129 "2 dakika" sabit metin · B130 cash-movements replay tutar/yön denetlemiyor

## Doküman sapması (kod esastır)
B131 (Orta) dual-screen-pos-topology.md koda göre eski (dokuman:D-01..04) · B132 api-contract-standard.md/hata kodu adı · B133 idempotency anahtarı gövdede (bilinen V1-RMD-430) · B134 iki hata zarfı biçimi · B135 operasyon belgeleri · B136 güvenlik gerekçeli yanlış yorumlar · B137 yorum–kod çelişkileri · B138 KVKK runbook (V1-RMD-673) · B139 CLI kullanım metni · B140 order.json 139≠197 (bilinen V1-RMD-597) · B141 eski veri/ayar belgeleri · B142 bildirim matrisi (V15-NOT-001) · B143 ApplicationPermissions katalog dışı kodlar

## Fırsat
F001 eşleştirmeyi tek dokunuşa indirmek · F002 ekran "bağlı" rozeti · F003 iptalde onay ve geri al · F004 yardım çağrısı "görüldü" geri bildirimi · F005 bekleyen hesap sesi/rozeti · F006 push izni yönlendirmesi · F007 kasaya boşta kilit · F008 error.code → Türkçe sözlük · F009 iptal sonrası marka ekranı · F010 hub/push/snapshot/outbox yük senaryosu

## Semih soruları
S1 devir notu ve yardım çağrısı saklama süresi · S2 anonimleştirilen personelin abonelik/not/çağrıları · S3 push endpoint URL'si kişisel veri mi · S4 VAPID anahtarı veritabanında düz kalabilir mi · S5 vergi kimliği erişim kaydı kapsamı · S6 pasif personele bildirim · S7 yedek saklama ve silme talepleri · S8 "kart çekilmedi" tek kişiyle kapanabilir mi · S9 table-draft siparişleri müşteri ekranında görünsün mü

## Çürütülen / bulgu sayılmayan iddialar (ajan başına)
- test-kalitesi: "HelpRequests/WebPush sunucu testi yok" çürütüldü (testler var; gerçek boşluk B027); PostgresException→503 ve InvalidOperationException→409 testsiz iddiası çürütüldü (mutantlar öldü); A03 mutantı geçersiz (derleme hatası).
- frontend: sunucunun üretmediği kodu taklit eden istemci testi bulunamadı (kod `ApiErrorCatalog.cs:580`'de üretiliyor).
- dokuman: D-06 kısmen çürütüldü (CONCURRENT_MODIFICATION kodda var; yalnız Catalog alanı CONCURRENCY_CONFLICT).
- mimari: B-09 etkisi kanıtlanamadı (B098 Düşük); B-22 yalnız yorum çelişkisi.
- operasyon: "doğrulanırsa Kritik" doğrulanmadı; Host.Observability exit=1 hiçbir ajanda bulgu sayılmadı.
- backend/entegrasyon: hub'dan kimlik sızıntısı abartılı (yalnız orderId/revision, veri ≤5 sn'de kesiliyor).
- performans: hız sınırı kotası paylaşımı (V1-RMD-008) giderilmiş, bulgu yapılmadı.

## Doğrulanamayan altyapı
Host.Observability test koşusu bellek yüzünden yarım (exit=1, log kesik); cloudflared başlık davranışı, gerçek push servisi ve cihaz, tarayıcıda elle deneme.
