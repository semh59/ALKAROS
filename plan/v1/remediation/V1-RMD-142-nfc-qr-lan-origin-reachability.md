# V1-RMD-142 - NFC/QR LAN origin reachability

- Task ID: V1-RMD-142
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-141'in kendi "Out of scope"unda Semih'e ayrıca bildirilen, sonra
"Düzelt" denilerek onaylanan gerçek boşluk: `web`/Caddy'nin LAN yolu
üzerinden `/api/v1/nfc/*` ve `/api/v1/qr/*`'a erişim, bu görevden önce hep
404 dönüyordu (`--nfc-origin-header` hiç yapılandırılmamıştı). Sonuç:
restoran WiFi'ındaki bir müşteri NFC/QR'ı yalnızca genel tünel/röle host
adı üzerinden kullanabiliyordu, kendi LAN'ından değil. Müşteri-görünür
izolasyonu (V1-RMD-139/141) bozmadan LAN yolu da açıldı: NFC/QR için ayrı
bir `nfc.<ALKAROS_PROXY_HOST>` vhost'u, röle yolunun sunduğu AYNI izole
paketleri (`dist-nfc`, `CustomerWeb`) sunuyor — asla tam PosTerminal SPA'sını
değil.

## Owned surface

- `plan/v1/remediation/V1-RMD-142-nfc-qr-lan-origin-reachability.md` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası
  olarak parse etmesin):
  - deploy/docker/Caddyfile, deploy/docker/Caddyfile.dev (paylaşılan dağıtım
    konfigürasyonu, deep-analysis B-4/V1-RMD-098 sahipliğinde) — yeni
    `nfc.{$ALKAROS_PROXY_HOST:localhost}` vhost'u (prod) ve `http://:82`
    vhost'u (dev): `/api/v1/nfc/*` ve `/api/v1/qr/*`'ı `X-Alkaros-Origin: nfc`
    etiketiyle `api:5080`'e proxy'liyor; `/nfc/*` ve `/qr/*`'ı sırasıyla
    `/srv/nfc-app` ve `/srv/qr-app`'tan `handle_path` ile sunuyor; eşleşmeyen
    her şey için açık bir `respond 404` (bkz. Acceptance evidence — bu olmadan
    Caddy'nin kendi varsayılanı boş bir 200 dönüyordu).
  - deploy/docker/Dockerfile (paylaşılan dağıtım konfigürasyonu) — `web`
    aşamasına `COPY --from=frontend-build /src/dist-nfc /srv/nfc-app` ve
    `COPY src/Apps/CustomerWeb/{Menu,OrderEntry}/wwwroot /srv/qr-app`
    eklendi; frontend-build aşamasındaki eski "QR bundle deliberately NOT
    copied" yorumu, yeni gerçeği yansıtacak şekilde güncellendi.
  - compose.yaml, compose.dev.yaml (paylaşılan dağıtım konfigürasyonu) —
    `api` servisinin komut satırına `--nfc-origin-header X-Alkaros-Origin`
    eklendi (CLI bayrağının kendisi V1-RMD-139'dan beri zaten vardı, hiç
    `compose.yaml`'a bağlanmamıştı); `compose.dev.yaml`'a `web`'in yeni
    `"8092:82"` port eşlemesi eklendi.
  - deploy/docker/README.md (paylaşılan dağıtım dokümantasyonu) — yeni
    "NFC/QR LAN origin (V1-RMD-142)" bölümü eklendi.
  - tests/Deployment/test_container_contract.py (paylaşılan dağıtım testi) —
    yeni vhost/bayrak/port için doğrulamalar eklendi; `api_stage`'de "hiç
    statik içerik yok" iddiası, artık kasıtlı olarak var olan müşteri-yüzü
    NFC/QR paketlerini (staff-yüzü WaiterPwa/Cashier'dan ayırt ederek)
    doğru yansıtacak şekilde düzeltildi (bkz. Acceptance evidence).

## In scope

- NFC/QR API'lerinin ve statik sayfalarının, röle/tünel yoluna ek olarak,
  restoranın kendi LAN'ından da (Caddy üzerinden) erişilebilir olması —
  aynı izolasyon garantisiyle (Cashier/yönetici API'leri bu origin'de hâlâ
  tamamen kapalı).

## Out of scope

- Fiziksel NFC etiketlerinin/QR kodlarının yeniden yazılması — hangi host
  adının (tünel mi LAN mı) kullanılacağı bir dağıtım/operasyon kararı,
  bu görevin kapsamı dışında.

## Dependencies

- V1-RMD-139
- V1-RMD-140
- V1-RMD-141
- V12-CWB-001

## Acceptance evidence

- **Bulunan ve düzeltilen gerçek bir bulgu:** Caddy'nin eşleşmeyen istek
  için varsayılanı, beklenenin aksine boş bir 200'dü, 404 değil. Yeni
  vhost'ta yalnızca `/api/v1/nfc/*`, `/api/v1/qr/*`, `/nfc/*`, `/qr/*` için
  `handle`/`handle_path` blokları vardı, sonunda bir catch-all yoktu (diğer
  vhost'lar `spa_static` import ederek bunu örtük alıyor). Gerçek bir
  `curl` ile `/api/v1/terminals/...` denendiğinde `api`'ye hiç ulaşmadan
  Caddy'nin kendisi `200 OK, Content-Length: 0` döndürdüğü görüldü —
  Cashier verisi sızmadı (backend'e hiç gidilmedi) ama izolasyon sözleşmesi
  (her şey 404 olmalı) ihlal ediliyordu. `handle { respond 404 }`
  eklenerek düzeltildi, gerçek `curl` ile doğrulandı (aşağıya bakın).
- **`tests/Deployment/test_container_contract.py`'de bulunan, bu görevden
  ÖNCEKİ (V12-CWB-001'den beri var olan) gerçek bir regresyon, düzeltildi:**
  `test_api_image_is_headless_http_only`'nin `assert "wwwroot" not in
  api_stage` iddiası, V12-CWB-001'in kasıtlı olarak eklediği
  `COPY src/Apps/CustomerWeb/Menu/wwwroot ./qr-web` satırındaki ham "wwwroot"
  alt dizesiyle çelişiyordu — bu test hiç çalıştırılmamış olmalı (bu
  oturumun kendi evidence listesinde yok). İddia, ham "wwwroot" kelimesini
  yasaklamak yerine özellikle staff-yüzü `WaiterPwa/wwwroot` ve
  `Cashier/wwwroot`'un yokluğunu kontrol edecek şekilde düzeltildi.
- **Önceden var olan, ilgisiz bir test hatası bulundu, DOKUNULMADI:**
  `test_dockerfile_is_multi_stage_and_uses_pinned_toolchains`'in
  `assert dockerfile.count("FROM ") == 4` iddiası, `git show 044d3129` ile
  doğrulandığı üzere bu oturumdan ÖNCE de zaten 5 FROM'a karşı başarısız
  oluyordu ("test" aşaması eklendiğinden beri, dosyanın kendi "Four stages"
  yorumu hiç güncellenmemiş). Bu görevle ilgisi yok, düzeltilmedi — kayda
  geçirildi.
- `docker compose config --quiet` (prod) ve
  `docker compose -f compose.yaml -f compose.dev.yaml config --quiet` (dev):
  ikisi de gerçek exit code 0.
- `python -m pytest tests/Deployment/test_container_contract.py`: **7/8** —
  1 önceden var olan, ilgisiz hata (yukarıya bakın), yeni hata yok.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 1 önceden var olan,
  ilgisiz ihlal (değişmedi), yeni ihlal yok.
- **Gerçek Docker Compose ile uçtan uca doğrulama** (`docker compose
  build/up`, gerçek `alkaros-web-1`/`alkaros-api-1`/`alkaros-postgres-1`
  konteynerleri, TLS dahil gerçek Caddy):
  - `https://nfc.localhost:8443/nfc/{herhangi-id}`: **200**, gerçek
    `index.html`; `/qr/`, `/qr/order-entry.html`: **200**.
  - `https://nfc.localhost:8443/api/v1/nfc/tables/.../catalog`: **200**,
    gerçek ürün listesi.
  - Elle oluşturulmuş bir masa + ürün + gerçek token ile:
    `POST https://nfc.localhost:8443/api/v1/qr/sessions` → gerçek oturum
    token'ı; `GET .../api/v1/qr/menu` (oturum başlığıyla) → gerçek ürün
    listesi — **QR'ın tam akışı, gerçek Caddy LAN vhost'u üzerinden ilk kez
    doğrulandı**, yalnızca loopback değil.
  - `https://nfc.localhost:8443/api/v1/terminals/.../catalog`: **404**
    (düzeltmeden önce **200**, boş gövde — bkz. yukarıdaki bulgu).
  - `https://nfc.localhost:8443/whatever` (eşleşmeyen rastgele yol): **404**.
  - Ana vhost regresyon kontrolü: `https://localhost:8443/api/v1/nfc/...`:
    hâlâ **404**; `https://localhost:8443/`: hâlâ **200**.
  - Dev overlay (`compose.dev.yaml`) ile `http://localhost:8092/nfc/...`,
    `/qr/`, `/api/v1/nfc/...`: **200**;
    `http://localhost:8092/api/v1/terminals/...`: **404** — ayrıca
    doğrulandı, sonra prod-only konfigürasyona geri dönüldü.
  - Test verisi (masa, bölge, ürün, oturum, token, nonce) doğrulama sonrası
    veritabanından silindi.

## Handoff

- None
