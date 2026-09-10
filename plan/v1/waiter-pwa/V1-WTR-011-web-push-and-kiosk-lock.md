# V1-WTR-011 - Uygulama kapalıyken bildirim (Web Push) ve tam ekran kilidi

- Task ID: V1-WTR-011
- Status: Done
- Assignee: Claude Opus 5
- Work type: implementation
- Surface state: Existing

## Goal

V1-WTR-010'un kapsam dışı bıraktığı iki iş. İkisi de garsonun elindeki
cihazın gerçek kullanım biçimiyle ilgili:

1. **Bildirim yalnız uygulama açıkken çalışıyor.** SignalR (V1-WTR-009,
   V1-RMD-149) bağlıyken "ürün hazır" ve "misafir siparişi" duyuruluyor;
   telefon cebe girip ekran kapandığında hiçbir şey ulaşmıyor. Mutfak
   hazırladığı tabağı kimseye haber veremiyor. `foundations.md` §5.3 zaten
   "Push (uygulama kapalı/arka planda)" satırını taşıyor — karşılığı yoktu.
2. **Tam ekran ve kilit.** Semih'in isteği (2026-09-10): "tam ekranda
   çıkmayı zorlaştırmak" + telefondaki gibi kaldırılabilir PIN. PIN
   V1-RMD-151 ve V1-WTR-010 ile geldi; tam ekranın kendisi gelmedi.

Bağımlılık eklenmez: RFC 8291 (aes128gcm yük şifrelemesi) ve RFC 8292
(VAPID) `System.Security.Cryptography` ile yazılır — `ECDiffieHellman`
P-256, `HKDF`, `AesGcm` ve ES256 için `ECDsa` .NET 8'de kutudan geliyor.

## Owned surface

- `plan/v1/waiter-pwa/V1-WTR-011-web-push-and-kiosk-lock.md` (yeni)
- `database/migrations/V1/V1-WTR-011/**` (yeni)
- `src/Host/Experience/WebPush/**` (yeni — `WaiterNotifications/` V1-WTR-009'un
  sahipliğinde, oraya dosya eklenmez)
- `tests/Host/Experience/WebPush/**` (yeni — bu görevin test projesi)
- `src/Clients/WaiterPwa/wwwroot/brand/**` (yeni — üst barın ve kilit
  perdesinin koyu zemininde kullanılan logo, kaynağı `docs/design/brand/`)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır (yollar
  geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası olarak
  parse etmesin):
  - database/migrations/order.json — 096 kaydı.
  - src/Host/Composition/Migrations/MigrationManifest.cs — PhaseBMax.
  - tests/Host/MigrationComposition/** — manifest testinin sınır değerleri.
  - ALKAROS.slnx — test projesinin çözüme eklenmesi.
  - tools/consistency-audit/consistency_audit.py — HOST_AREA_SCHEMA'ya yeni
    alanın şema sahipliği eklenir (denetim atlanmaz, doğru cevap yazılır).
  - src/Host/Experience/WaiterNotifications/WaiterNotificationsExperience.cs,
    src/Host/Experience/PendingOrderNotifications/SignalRPendingOrderAnnouncer.cs
    (V1-RMD-149 sahipliğinde) — duyuru artık SignalR + push.
  - src/Host/DualScreen/DualScreenApplication.Endpoints.cs (V1-DSP-00x
    sahipliğinde) — abonelik uç noktaları.
  - src/Clients/WaiterPwa/wwwroot/sw.js (V1-WTR-006 sahipliğinde) — push ve
    notificationclick olayları.
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js,
    src/Clients/WaiterPwa/wwwroot/waiter-app.css,
    src/Clients/WaiterPwa/wwwroot/index.html (V1-WTR-010 sahipliğinde) —
    abonelik ve tam ekran.

## In scope

1. **Abonelik deposu.** `identity.push_subscriptions`: endpoint (birincil
   anahtar niteliğinde), p256dh, auth, kullanıcı, terminal, oluşturulma ve
   son başarı zamanı. Aynı cihaz yeniden abone olursa satır güncellenir.
2. **VAPID.** Sunucu anahtar çiftini ayardan okur, yoksa üretip
   `settings`'e yazar; istemci genel anahtarı bir uç noktadan alır. Özel
   anahtar hiçbir zaman istemciye gitmez.
3. **Şifreleme.** RFC 8291 aes128gcm: rastgele salt, sunucu tarafı geçici
   ECDH anahtarı, HKDF ile CEK ve nonce, `AesGcm`. Gövde RFC 8188 başlığını
   taşır. Yük 3993 bayta sığar (RFC 8291 §4 sınırı), aşarsa kırpılır.
4. **Gönderim.** `WebPushSender`: VAPID `Authorization: vapid t=<JWT>,
   k=<pubkey>` ve `Content-Encoding: aes128gcm`. 404/410 aboneliği siler
   (RFC 8030 §7.3), 429 geri çekilir, diğer hatalar loglanır ve yutulur —
   bildirim gönderilemedi diye sipariş akışı durmaz.
5. **Bağlama.** `IPendingOrderAnnouncer`'ın Host uygulaması SignalR'ın
   yanına push ekler; mutfak "hazır" olayı da aynı yoldan gider.
6. **İstemci.** `sw.js` `push`/`notificationclick`; uygulama izni
   *kullanıcı jestiyle* ister (tarayıcı zaten şart koşuyor), abone olur ve
   aboneliği sunucuya yazar; çıkışta abonelikten çıkar.
7. **Tam ekran.** Kullanıcı jestiyle `requestFullscreen`, `screen.orientation`
   serbest, `navigator.wakeLock` ile ekran sönmesin. Tam ekrandan çıkış
   PIN kurulmuşsa kilidi tetikler — "çıkmayı zorlaştırmak" tam olarak bu:
   web sayfası işletim sistemi düzeyinde sabitleme yapamaz, ama çıkışın
   bedeli PIN olur.

## Out of scope

- Gerçek kiosk cihaz ayarı (Android Ekran Sabitleme / iOS Rehberli Erişim):
  bir web sayfasının erişemeyeceği işletim sistemi ayarı. Tam ekran + PIN
  bu görevin verebileceği en yakın karşılık ve §7'de veriliyor.
- iOS'ta Web Push yalnız ana ekrana eklenmiş (standalone) PWA'da çalışır —
  Apple'ın kısıtı; istemci bunu algılayıp sessizce SignalR'a düşer.
- Kasa, PosTerminal ve mutfak ekranlarının abone edilmesi: aynı altyapı,
  kendi görevleri.

## Dependencies

- V1-WTR-010
- V1-RMD-149
- V1-RMD-151

## Acceptance evidence

- `dotnet build ALKAROS.slnx`: exit 0, 0 Uyarı, 0 Hata. Yeni paket yok;
  `Directory.Packages.props` değişmedi.
- Testler (hepsi `ALKAROS_TEST_PG_PORT=55432` ile), toplam 226:
  - `ALKAROS.Host.Experience.WebPush.Tests` 18/18 (yeni)
  - `MigrationComposition` 134/134, `KitchenOperations` 7/7,
    `WaiterNotifications` 1/1, `Orders.TableDraft` 32/32,
    `NfcOrdering` 17/17, `QrOrdering` 17/17
- **Şifreleme RFC'nin kendi vektörüyle doğrulandı.** `WebPushCryptoTests`
  RFC 8291 §5'in worked example'ını birebir üretiyor. Bu test ilk turda
  DÜŞTÜ ve gerçek bir hatayı yakaladı: RFC 8188 §2.1'deki `rs` alanına
  gönderilen kaydın uzunluğunu yazmıştım; oysa alan göndericinin *ilan
  ettiği* kayıt boyutu (4096). Yanlış hâliyle her push servisi gövdeyi 201
  ile kabul ediyor, hiçbir tarayıcı çözemiyordu — yani "çalışıyor" gibi
  görünen, sessizce hiç ulaşmayan bir bildirim. Vektör olmasa görülmezdi.
- VAPID başlığı ayrıca bağımsız doğrulanıyor: test, `k=` ile ilan edilen
  anahtarla ES256 imzasını `ECDsa.VerifyData` ile bir push servisinin
  yaptığı gibi kontrol ediyor; `aud`/`sub`/`exp` iddiaları da okunuyor.
- Migration gerçekten çalıştırıldı (scratch veritabanı, `wtr011_scratch`):
  up → tablolar oluştu; tek satır VAPID kısıtı ikinci `INSERT`'te
  kimliği korudu (`pub1` kaldı); aynı endpoint'e yeniden abonelik satırı
  çoğaltmadı, güncelledi (1 satır, anahtar `k2`); kullanıcı silinince
  abonelikler `ON DELETE CASCADE` ile gitti; down → şema tamamen kalktı;
  up yeniden uygulandı (idempotent).
- HTTP yüzeyi: oturumsuz istek 401, bozuk abonelik (http, `file://`,
  base64 olmayan anahtar, boş endpoint) 400 ve hiç satır yazılmıyor,
  silme gerçekten siliyor, genel anahtar iki çağrıda aynı kalıyor.
- İstemci tarayıcıda denendi: profil sayfasında dört seçenek çıkıyor
  (Tam ekran / Uygulama kapalıyken de bildir / Ekran kilidi / Çıkış);
  sunucunun VAPID anahtarı istemcinin çözücüsünden 65 baytlık sıkıştırılmamış
  P-256 noktası olarak geçiyor (`applicationServerKey`'in beklediği biçim);
  **tam ekrandan çıkınca PIN kilidi devreye giriyor** — `fullscreenchange`
  sonrası kilit perdesi açılıyor, 4 noktalı PIN ve `123456789⌫0✓` tuş
  takımı geliyor.
- Denemede doğrulanamayan iki şey, olduğu gibi: (1) tam ekrana *geçiş*,
  bu Chrome örneğinin otomasyon bağlamında düz bir düğmeden bile
  `TypeError: not granted` ile reddediliyor (`document.fullscreenEnabled`
  true; kısıt ortamın, kodun değil); (2) `pushManager.subscribe`, kum
  havuzundan FCM'e ağ çıkışı olmadığı için 45 sn'de zaman aşımına uğradı.
  İkisi de gerçek cihazda denenmeli.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: bu görevden yeni
  ihlal yok. Denetim iki kez haklı çıktı ve ikisinde de kural atlanmadı,
  doğru cevap yazıldı: yeni Experience alanının şema sahipliği
  `HOST_AREA_SCHEMA`'ya eklendi, ve migration yorumundaki Türkçe alıntı
  İngilizceye çevrildi. Kalan tek ihlal
  `InventoryAdjustmentService.cs:96`, bu görevin diff'inde değil.

## Handoff

- None
