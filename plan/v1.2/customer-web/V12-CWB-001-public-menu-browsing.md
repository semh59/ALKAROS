# V12-CWB-001 - Build public menu browsing

- Task ID: V12-CWB-001
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:II.2.18
- PDF:II.6.8
- PDF:II.7.3
- PDF:III.21

## Goal

Authenticated QR customer session için available sellable menu'yü internal management verisini açmadan sunmak.
Semih ile görüşülüp netleşen sıra: QR'ın kendi ayrı uygulaması
(`src/Apps/CustomerWeb`) → relay kapsam sıkılaştırması (V1-RMD-140) → masa
anti-kötüye-kullanım politikası (V12-QRO-002) → şimdi bu görev: QR'ın hiç
var olmayan HTTP yüzeyi (oturum değişimi + menü okuma) ve gerçek müşteri
sayfası.

## Owned surface

- `src/Host/Experience/QrOrdering/**`, `tests/Host/Experience/QrOrdering/**`
  (yeni) — `POST /api/v1/qr/sessions` (V12-QRS-002/003'ün ham masa
  token'ı → oturum değişimi) ve `GET /api/v1/qr/menu` (oturum korumalı,
  oturumsuz NFC'nin aksine — Goal'ın kendisi "authenticated QR customer
  session" diyor). Sipariş gönderim/poll route'ları buraya V12-CWB-002
  tarafından eklenecek (aynı dosya, "Sınırlı ek" olarak).
- `src/Apps/CustomerWeb/Menu/**`, `tests/Apps/CustomerWeb/Menu/**` (yeni) —
  bağımsız, derlemesiz (bundler'sız) HTML/CSS/JS müşteri sayfası, Cashier/
  WaiterPwa'nın vanilla-shell geleneğiyle aynı; PosTerminal'in React/Vite
  zincirine hiç dokunmuyor (Semih'in kendi kararı: "tamamen ayrı bir
  uygulama").
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik
  iddiası olarak parse etmesin):
  - src/Host/DualScreen/DualScreenOptions.cs, DualScreenApplication.cs
    (V0-ARC-009/V1-RMD-139/V1-RMD-140 sahipliğinde):
    - `--qr-web-root <path>` yeni CLI bayrağı ve `QrWebRoot` seçeneği:
      `--api-only` modunda bu süreç hiç statik dosya sunmuyordu (proxy
      sunuyordu), ama Cloudflare Tunnel bağlayıcısı bu sürece doğrudan
      loopback üzerinden ulaşıyor ve proxy'den hiç geçmiyor
      (`RelayProvisioningService.LocalOriginService`) — bu bayrak
      olmadan QR'ın müşteri sayfası gerçek röle üzerinden hiçbir zaman
      yüklenemezdi. `/qr/*` altında sunuluyor, `UseRouting()`'den ÖNCE
      kayıt edildi (routing'den sonraki bir middleware'in, zaten seçilmiş
      bir fallback endpoint'in ardında çalışabildiği gözlemlendi — bkz.
      Acceptance evidence'taki kök-neden notu).
    - `isNfcApi` kontrolü artık `/api/v1/qr/*`'i de kapsıyor — V1-RMD-140'ın
      kendi "Out of scope"unun önceden işaretlediği tek satırlık
      genişletme, QR'ın uç noktaları var olduğunda yapılacaktı.
    - **Gerçek röle üzerinden ilk uçtan uca deneme sırasında bulunan
      gerçek bir bulgu:** HTTPS zorunluluğu kapısı (`app.Use` en üstte),
      yalnızca `/health/ready` yolunu loopback-istisnası olarak tanıyordu
      — `--nfc-loopback-origin` açık olsa bile her NFC/QR isteği
      `HTTPS_REQUIRED` ile reddediliyordu, çünkü `compose.yaml` loopback'i
      hiç `--trusted-proxy` olarak eklemiyor (yalnızca
      `--trusted-network 172.16.0.0/12`, Docker köprü ağı). V1-RMD-140'ın
      kendi kanıtı bunu hiç yakalamamıştı çünkü o görevin kompozisyon
      testi `TrustedProxies: [IPAddress.Loopback]` kullanıyordu — bu,
      `compose.yaml`'ın gerçek yapılandırmasını yansıtmıyor. Düzeltme: HTTPS
      kapısına, `NfcLoopbackOriginTrusted` açıkken bir loopback bağlantısını
      da geçerli sayan üçüncü bir istisna eklendi (origin gate'in kendi
      "port yayınlanmıyor, sahtecilik imkânsız" gerekçesiyle aynı). Gerçek
      Docker konteynerinde (`docker exec alkaros-api-1 curl
      http://127.0.0.1:5080/...`) doğrulandı: düzeltmeden önce her istek
      400 dönüyordu, sonrasında tam akış (oturum + menü) 200 döndü.
  - tests/Host/MigrationComposition/DualScreen/DualScreenOptionsTests.cs,
    tests/Host/Experience/Composition/ProductionExperienceCompositionTests.cs
    (aynı sahiplikler) — `--qr-web-root` doğrulama testleri, gerçek statik
    dosya sunumu testi, aşırı büyük QR isteği reddi testi, genişletilmiş
    loopback-origin testi.
  - compose.yaml, deploy/docker/Dockerfile (paylaşılan dağıtım
    konfigürasyonu) — `api` servisinin CMD/ENTRYPOINT'ine `--qr-web-root
    /app/qr-web` eklendi; `api` imajına `src/Apps/CustomerWeb/Menu/wwwroot`
    kopyalandı. `web` (Caddy) imajına KASITLI OLARAK kopyalanmadı — bkz.
    Acceptance evidence'taki gerekçe (LAN/Caddy yolunda API çağrıları zaten
    404 dönerdi, yarı-çalışır bir tuzak olurdu).
  - deploy/docker/README.md (paylaşılan dağıtım dokümantasyonu) — mevcut
    "QR/NFC public relay origin scope" bölümüne, statik dosya sunumu ve
    HTTPS-kapısı düzeltmesiyle ilgili bir not eklendi.
  - ALKAROS.slnx (paylaşılan çözüm dosyası) — yeni
    `ALKAROS.Host.Experience.QrOrdering.Tests` girdisi.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Kategori navigasyonu, ürün ayrıntıları, fiyat/bulunabilirlik sunumu ve eski oturum yönetimi.
- Ham masa token'ının (`t` sorgu parametresi, taranan QR koduyla açılan URL) bir müşteri oturumuna değişimi
  (`POST /api/v1/qr/sessions`, V12-QRS-002'nin nonce/zaman damgası tekrar-oynatma koruması dahil).
- Oturum korumalı, anonim menü okuma (`GET /api/v1/qr/menu`) — yalnız yayınlanmış, satılabilir ve
  yönetici tarafından askıya alınmamış ürünler (V1-RMD-128'in `is_available` kontrolüyle aynı).
- Süresi dolan/iptal edilen/boşta kalan bir oturumun sessizce bir kez yenilenmesi (`loadMenuWithSessionRetry`).

## Out of scope

- Sepet gönderimi, payment, QR token verilmesi (masaya ait ham token'ın personel tarafından üretilmesi/döndürülmesi
  — `TableTokenService.IssueAsync/RotateAsync`'in kendi HTTP yüzeyi henüz hiç yok, ayrı bir görev) ve menü yönetimi.
- Alerjen bilgisi: `catalog.products`/`CatalogProductDto` içinde hiç alerjen alanı yok; bunu gerçek anlamda
  eklemek yeni bir Catalog şema özelliği gerektirir, bu görevin "menü + sipariş verme" kapsamının açıkça dışında.
  Sahte/uydurma bir alerjen göstergesi eklenmedi.
- `V12-STK-001` (cross-channel last-portion arbitration): planın orijinal bağımlılık listesinde vardı, bu
  görevden çıkarıldı (plan-audit `DONE_DEPENDENCY_NOT_FINAL` — hâlâ Planned). NFC'nin kendi emsali
  (`PortionReservationStatus.NotApplicable`, V12-NFC-001/V12-QRO-001, ikisi de zaten Done) aynı gerçek coupling'i
  hiç bağımlılık olarak bildirmeden aynı şekilde gönderildi — QR'ın sipariş kalemleri zaten bu stopgap'i
  kullanıyor. V12-STK-001 gerçek "Online" kanalı gerektiren, çok daha büyük bir çapraz-kanal işi; Semih'in kendi
  kapsam daraltmasıyla ("şimdilik sadece menü ve sipariş verme") orantısız bulundu.
- QR'ın kendi ham token'ının LAN/Caddy üzerinden (`web:8443`) erişilebilir olması: NFC'nin kendi emsali gibi
  (`/api/v1/nfc/*`, aynı origin-gate altında), QR'ın müşteri yüzeyi artık yalnızca röle/tünel host adı üzerinden
  erişiliyor — müşteri WiFi'da olsun ya da mobil veride, aynı URL, Semih'in bu oturumdaki kararıyla tutarlı.

## Dependencies

- V12-QRS-003
- V11-MNU-003
- V0-CMP-005

## Deliverables

- Duyarlı genel menü arayüzü (`src/Apps/CustomerWeb/Menu/wwwroot`).
- `POST /api/v1/qr/sessions`, `GET /api/v1/qr/menu` HTTP uç noktaları.
- `--qr-web-root` ile gerçek röle topolojisi üzerinden statik dosya sunumu.
- Erişilebilirlik, yetkilendirme, eski veriler ve kullanılamayan öğe testleri.

## Acceptance evidence

- Geçerli bir table oturumu yalnızca yayınlanmış satılabilir öğeleri görür; iptal edilen/süresi dolan oturumlar ve
  kullanılamayan ürünler, dahili tanımlayıcılar sızdırılmadan işlenir.
- `dotnet build ALKAROS.slnx -c Debug`: 0 Uyarı, 0 Hata.
- Gerçek Postgresql'e karşı Docker'da, real exit code 0 ile:
  - `ALKAROS.Host.Experience.QrOrdering.Tests` (yeni): **11/11** — geçerli/bilinmeyen/iptal/süresi dolmuş token,
    tekrar-oynatma reddi, bayat zaman damgası reddi, geçerli/eksik/bilinmeyen oturumla menü okuma,
    kullanılamayan ürünün menüden gizlenmesi.
  - `ALKAROS.Host.Experience.NfcOrdering.Tests`: **17/17** (regresyon yok).
  - `ALKAROS.Host.Experience.Composition.Tests`: **9/9** (yeni: genişletilmiş loopback-origin testi `/api/v1/qr`
    kapsıyor, aşırı büyük QR isteği reddi, gerçek statik dosya sunumu testi).
  - `ALKAROS.Host.Tests` (`DualScreenOptionsTests` alt kümesi): **18/18** (yeni: `--qr-web-root` doğrulamaları).
  - `tests/Architecture/ModuleBoundaries`: **8/8** (regresyon yok).
- `python -m pytest tests/Apps/CustomerWeb/Menu/test_customer_web_menu.py`: **4/4**.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 1 önceden var olan, ilgisiz ihlal (değişmedi), yeni
  ihlal yok.
- **Gerçek Docker Compose ile uçtan uca doğrulama** (`docker compose build/up`, gerçek `alkaros-postgres-1`/
  `alkaros-api-1`/`alkaros-web-1` konteynerleri, gerçek migration 078/081/085 uygulanmış):
  - `docker exec alkaros-api-1 curl http://127.0.0.1:5080/qr/`: **200**, gerçek `index.html`; `/qr/menu-app.js`
    ve `/qr/menu-app.css`: **200** — QR'ın statik sayfası, gerçek röle topolojisinin (yalnızca loopback,
    hiç yayınlanmış port yok) simülasyonuyla ilk kez uçtan uca yüklendi.
  - Elle oluşturulmuş bir masa + ürün + gerçek `TableTokenGenerator` algoritmasıyla üretilmiş bir masa token'ı
    ile: `POST /api/v1/qr/sessions` → gerçek oturum token'ı döndü; `GET /api/v1/qr/menu` (oturum başlığıyla) →
    gerçek ürün listesi döndü. Test verisi doğrulama sonrası veritabanından silindi (kalıcı geliştirme
    veritabanında iz bırakılmadı).
  - `docker exec alkaros-api-1 curl http://127.0.0.1:5080/api/v1/terminals/.../catalog`: **404** — Cashier API'si
    loopback/röle origin'inde hâlâ tamamen kapalı (origin-gate'in ana koruması regresyona uğramadı).
  - `curl https://localhost:8443/api/v1/qr/menu` (Caddy/LAN yolu): **404** — beklenen ve kasıtlı (yukarıdaki Out
    of scope notuna bakın); `curl https://localhost:8443/qr/`: PosTerminal SPA'sının kendi fallback'i döner
    (QR sayfası değil — `web` imajına hiç kopyalanmadı), yanıltıcı bir "yarı çalışır" durum bırakılmadı.

## Handoff

- V12-CWB-002
