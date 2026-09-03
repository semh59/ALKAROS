# V1-RMD-098 - Docker sıfırdan: arayüz/backend ayrımı (A1-full) ve yüzey sahipliği

- Task ID: V1-RMD-098
- Status: Done
- Assignee: /root/rmd098_docker_frontend_backend_split
- Work type: implementation
- Surface state: Existing

## Goal

Konteyner katmanını "arayüz ve backend servislerine göre" sıfırdan kurmak: tek `ALKAROS.Host`
sürecinin hem statik istemci paketlerini hem de JSON API + SignalR hub'larını sunması yerine, Caddi tabanlı
bir `web` imajı üç istemci paketini (PosTerminal SPA, WaiterPwa, Cashier) doğrudan sunar ve TLS'i sonlandırır;
`api` imajı `ALKAROS.Host serve --api-only` ile yalnız API + hub'ları düz HTTP `:5080` üzerinde çalıştırır
(deep-analysis A1-full). Dağınık `Dockerfile` + `compose.yaml` düzeni yeniden düzenlenir, giriş noktası
tekrarı giderilir, operatör bakım servisleri ayrı bir overlay'e taşınır. Semih onayıyla (2026-09-03) alınan
kararla `V1-RMD-096` Host-terminated HTTPS yedeği (E1, `8444`) bu ayrımla birlikte kaldırılır: TLS artık
yalnız `web` (Caddy) üzerinde sonlanır.

## Owned surface

- `plan/v1/remediation/V1-RMD-098-docker-frontend-backend-split-and-surface-custody.md`
- `deploy/docker/Dockerfile`
- `compose.yaml`
- `compose.ops.yaml`
- `compose.dev.yaml`
- `deploy/docker/Caddyfile`
- `deploy/docker/Caddyfile.dev`
- `deploy/docker/README.md`
- `.dockerignore`
- `src/Host/DualScreen/DualScreenOptions.cs`
- `src/Host/DualScreen/DualScreenApplication.cs`
- `tests/Host/MigrationComposition/DualScreen/DualScreenOptionsTests.cs`
- `tests/Host/MigrationComposition/DualScreen/DualScreenHostTests.cs`
- `docs/operations/production-go-live-checklist.md`
- `docs/recovery/backup-restore-runbook.md`
- `docs/recovery/rpo-rto-targets.md`
- `docs/operations/data-housekeeping.md`
- `docs/performance/infra-tuning.md`
- `docs/audit/V1_DESKTOP_POS_AND_CONTAINER_ACCEPTANCE_2026-08-28.md`
- `evidence/V1-RMD-098/**`

## In scope

- `DualScreenOptions` içine `--api-only` ve `--customer-display-origin-header <token>` bayrakları:
  `--api-only` verildiğinde `--web-root` zorunlu değildir (ve verilirse fail-closed reddedilir);
  `--customer-display-origin-header` yalnız `--api-only` ile geçerlidir ve `--customer-display-urls` ile
  karşılıklı dışlayıcıdır.
- `DualScreenApplication.Build`: `--api-only` modunda statik dosya boru hattı (`UseDefaultFiles` /
  `UseStaticFiles`) ve SPA fallback kaydedilmez, fallback JSON 404'tür. Müşteri ekranı origin izolasyonu
  (finding B-4) port sinyaline ek olarak, güvenilir ters proxy'nin ayarladığı başlık üzerinden çalışır:
  `context.Request.IsHttps` (yalnız `--trusted-proxy` / `--trusted-network` eşi için forwarded-proto onurlandırılır)
  ve başlık değeri `display` ise istek müşteri ekranı origin'i sayılır.
- `deploy/docker/Dockerfile` (kök `./Dockerfile`'dan taşındı): adlandırılmış dört aşama
  `backend-build` / `frontend-build` / `api` / `web`; `frontend-build` üç istemci paketini tek statik ağaca
  toplar; `api` aşamasında statik varlık yoktur ve TLS dinleyicisi yoktur (`EXPOSE 5080`); `web` aşaması
  `caddy:2.9-alpine` üzerine statik ağacı ve `Caddyfile`'ı gömer.
- `deploy/docker/Caddyfile` yeniden yazıldı: ana vhost statikleri sunar + `/api` `/hubs` `/health`'i
  `api:5080`'e proxy'ler ve gelen `X-Alkaros-Origin` başlığını siler; `display.` vhost aynı SPA kabuğunu
  sunar + tüm `/api` `/hubs`'ı `X-Alkaros-Origin: display` ekleyerek `api:5080`'e proxy'ler (izin/ret kararı
  tek yerde, api'de).
- `compose.yaml` çekirdek yığın: `postgres` + `migrate` + `provision` + `api` (eski `host`) + `web`
  (eski `proxy`, artık `web` build hedefiyle). `8444` yayını ve `alkaros-host-tls` hacmi kaldırıldı.
  Operatör araçları (`backup` / `basebackup` / `housekeeping`) `compose.ops.yaml`'a taşındı ve
  `docker compose -f compose.yaml -f compose.ops.yaml run --rm <svc>` ile çağrılır.
- `compose.dev.yaml` + `deploy/docker/Caddyfile.dev`: yerel geliştirme için `web`'i düz HTTP ile
  bind-mount edilmiş dev Caddyfile ile çalıştırır (`auto_https off`, sertifika yok,
  `X-Forwarded-Proto: https` yukarı akışa zorlanır ki api'nin HTTPS geçidi geçsin). İki port: `8090:80`
  ana origin, `8091:81` müşteri ekranı origin'i — tarayıcı bunları ayrı origin sayar, `*.localhost` DNS'e
  gerek kalmadan B-4 izolasyonu korunur (`display.localhost` bazı çözücülerde loopback yerine ağ
  geçidine çözülüyordu). Dev Caddyfile ayrıca dönüş yolunda `Set-Cookie`'den `; Secure`'u siler
  (`header_down`), yoksa düz HTTP sayfası oturum çerezini saklayamıyor ve giriş sonrası ekran
  ilerlemiyordu. `postgres` 5433, `api` 5080 host'a yayınlanır. `http://localhost` tarayıcı güvenli
  bağlamı olduğundan WaiterPwa service worker / çevrimdışı kuyruğu burada çalışır; LAN IP üzerinden
  erişim çalışmaz (saha testi için CA kurulu HTTPS 8443 yolu). Bu overlay asla deploy edilmez.
- `.dockerignore` daraltıldı (`plan/`, `docs/`, `evidence/`, `tools/`, `.github/`, `*.md` vs. build
  bağlamından çıkarıldı; `.git` build provenance için tutuldu).
- E1 kararı: `V1-RMD-096` Host self-signed `8444` yedeği kaldırıldı. `docs/operations/
  production-go-live-checklist.md`, `deploy/docker/README.md` ve `docs/audit/...ACCEPTANCE...md` bu kararı
  ve operasyonel sonucunu (Caddy düşerse yığın düşer; HA için standby proxy) kaydeder.
- `tests/Deployment/test_container_contract.py` yeni sözleşmeye göre yeniden yazıldı (Caddyfile drift'i
  de düzeltildi). `docs/recovery/*` ve `docs/operations/*` içindeki `--profile ops` çağrıları overlay
  formuna, `host`/`proxy` servis adları `api`/`web`'e güncellendi.
- `plan/AUDIT_REPORT.md` ve `plan/AUDIT_MANIFEST.json` bütünlük kayıtları mevcut ağaca göre yeniden üretildi.
- Yüzey devirleri (`V1-RMD-096` → `V1-RMD-098`): `src/Host/DualScreen/DualScreenOptions.cs`,
  `src/Host/DualScreen/DualScreenApplication.cs`, `deploy/docker/README.md`,
  `tests/Host/MigrationComposition/DualScreen/DualScreenOptionsTests.cs`,
  `tests/Host/MigrationComposition/DualScreen/DualScreenHostTests.cs`. Kök `./Dockerfile` kaldırıldı,
  yerine `deploy/docker/Dockerfile`. `tests/Deployment/test_container_contract.py` yeniden yazıldı ancak
  `V1-RMD-033`'ün `tests/Deployment` dizin yüzeyi altında kalır.
- B-4 takip düzeltmesi (müşteri ekranı linki doğru origin'i açsın): `runtime-configuration` endpoint'i
  `ALKAROS_CUSTOMER_DISPLAY_ORIGIN_URL` env değişkeni ayarlıysa `customerDisplayUrl` döndürür
  (`src/Host/DualScreen/DualScreenApplication.cs` sabit, `.Endpoints.cs` handler — ikincisi `V1-RMD-097`
  yüzeyi, çakışma yok). PosTerminal `api.runtimeConfig` ile bunu okur ve "Müşteri ekranı" butonu/linki
  `<origin>/display` açar (`src/Clients/PosTerminal/src/{api.ts,contracts.ts}` — `V1-RMD-031` yüzeyi;
  `src/Clients/PosTerminal/src/routes/{Cashier.tsx,workspace.tsx}` — `V1-RMD-097` yüzeyi). Ayarlı değilse
  link göreli `/display` kalır (tek-origin/legacy). `compose.yaml` `api` env'i
  `https://display.${ALKAROS_PROXY_HOST}:8443`, `compose.dev.yaml` `http://localhost:8091` verir. Bu
  dosyaların custody'si mevcut sahiplerinde kalır; ayrı devir kaydı gerekmez (`validate` çakışmasız).
- `GATE-V1-EXIT` bu iş için yeniden açılmıştır; resmi reseal ayrı bir governance görevine bırakılır.

## Out of scope

- `V1-RMD-096`, `V1-RMD-079`, `V1-RMD-038` gibi tarihsel `Done` görevlerin metnini yeniden yazmak
  (`./Dockerfile` yüzey kayıtları oldukları hâlde bırakılır; dosya artık `deploy/docker/Dockerfile`).
- `src/Host/DualScreen/DualScreenTls.cs` içeriğini değiştirmek — konteyner artık kullanmasa da bağımsız
  `docker run` / `--tls-cert` senaryosu için korunur.
- Yeni işlevsel davranış üretmek; ayrım dışında API sözleşmesi, hub'lar veya modül kompozisyonu değişmez.

## Dependencies

- V1-RMD-096

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release --no-restore` ve `-c Debug` 0 uyarı / 0 hata verir.
- `dotnet test` DualScreen filtresinde yeni testler geçer: `DualScreenApiOnlyOriginTests` 5/5 (headless
  build web-root'suz; ana origin display rotasını reddeder; display origin display-dışı API'yi reddeder;
  display origin display API'sini geçirir; güvenilmeyen eş display origin'i taklit edemez),
  `DualScreenOptionsTests` +3 (`--api-only` web-root reddi; header `--api-only` gerektirir;
  `--api-only` + `--customer-display-urls` reddi).
- Uçtan uca canlı test (`evidence/V1-RMD-098/compose-transcript.txt`, Docker 29.7.2 / Compose v5.3.1):
  `docker compose config` üç overlay kombinasyonunda geçerli; `up --build --wait` exit 0
  (postgres healthy → migrate 41 migration exit 0 → provision exit 0 → api healthy → web healthy);
  `/health/ready` `{"status":"Ready"}`; `/`, `/waiter/`, `/cashier/`, SPA deep-link, hashed asset
  HTTPS üzerinden 200; B-4: display rotası ana vhost'ta 404 "yalnızca ayrı origin", display vhost'ta
  handler'a ulaşır (401), display-dışı API display vhost'ta 404 "müşteri ekranı origin'inde sunulmuyor";
  `api:5080` düz HTTP forwarded-proto'suz 400 `HTTPS_REQUIRED`, loopback readiness muaf;
  `-f compose.yaml -f compose.ops.yaml run --rm backup` artefakt + sha256 üretir; `restart api` sonrası
  sağlıklı, migration sayısı 41; `down --volumes` exit 0.
- Dev HTTP overlay (`-f compose.yaml -f compose.dev.yaml up -d --wait`): `web` `:8090` (ana) + `:8091`
  (müşteri ekranı origin'i) düz HTTP'de sağlıklı; `/`, `/waiter/`, `/cashier/`, SPA deep-link,
  `/health/ready` 200; giriş 200 → çerez `Secure` bayrağı olmadan saklanır → `/auth/session`,
  `/catalog`, `/orders/active` 200; `runtime-configuration` `customerDisplayUrl: http://localhost:8091`
  döndürür (bundle `runtime-configuration` çağrısını içerir); pairing-create `:8091`'de 201, `:8090`'da
  404 "yalnızca ayrı origin" (B-4 sağlam).
- `python -B tools/plan-audit/plan_audit_tool.py validate` ve `verify-manifest` exit 0.
- `python -m pytest tests/Deployment tests/Architecture` 0 hata.
- `tools/consistency-audit/consistency_audit.py` temiz.
