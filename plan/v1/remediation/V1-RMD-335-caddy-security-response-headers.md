# V1-RMD-335 - Caddy artık her cevaba temel güvenlik başlıklarını ekliyor

- Task ID: V1-RMD-335
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) orta seviye bulgusu: "Caddy'de hiçbir güvenlik başlığı yok"
(`deploy/docker/Caddyfile:20-131`). Doğrulandı: üç production vhost'un (`{$ALKAROS_PROXY_HOST}`, `display.*`,
`nfc.*`) hiçbirinde `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy` veya benzeri tek bir güvenlik
başlığı ayarlanmıyordu — ne statik dosya sunumunda ne de `api`'ye ters-proxy edilen yanıtlarda.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): deploy/docker/Caddyfile
- `plan/v1/remediation/V1-RMD-335-caddy-security-response-headers.md`

## In scope

1. Yeni bir paylaşılan `(security_headers)` snippet'i: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`,
   `Referrer-Policy: same-origin`, `Permissions-Policy: geolocation=(), microphone=(), camera=()`. Site
   seviyesinde tanımlı, böylece hem statik dosya sunumunu hem `api`'ye ters-proxy edilen her yanıtı (hata
   yanıtları dahil) kapsıyor.
2. Her üç production vhost'a (`{$ALKAROS_PROXY_HOST}`/`localhost`, `display.*`, `nfc.*`) `import security_headers`
   eklendi.
3. Gerçek Caddy ikilisiyle doğrulama: `caddy validate` ile sözdizimi, `caddy fmt` ile biçim, ve gerçek bir
   konteynerde canlı bir HTTP isteğine karşı `curl` ile dört başlığın de fiilen döndüğü doğrulandı (kanıta bkz.).

## Out of scope

1. `Content-Security-Policy` — SPA paketlerinin (Cashier/Waiter/CustomerDisplay/NFC/QR) gerçekte hangi
   inline script/style'ları kullandığının önce denetlenmesini gerektirir; körlemesine eklemek uygulamayı sessizce
   kırma riski taşır — bu bulgunun kendi kapsamının dışında, ayrı bir görev.
2. `Strict-Transport-Security` — bu dağıtımın TLS'i `tls internal` (her cihaza kurulan bir dahili kök CA, bir LAN
   üzerinde). CA kurulumu doğrulanmadan bir cihazı HTTPS-only'e sabitlemek, başlığın kendi yokluğundan daha kötü
   bir sonuç (cihazın uygulamadan tamamen kilitlenmesi) riski taşıyor.
3. `deploy/docker/Caddyfile.dev` — kendi başlık yorumunun açıkça belirttiği gibi yalnızca yerel geliştirme içindir
   ("NEVER use this outside local development"), denetimin isim verdiği dosya değil.

## Dependencies

- None

## Acceptance evidence

- `docker run --rm caddy:2 caddy validate --config Caddyfile --adapter caddyfile`: `Valid configuration`.
- `docker run --rm caddy:2 caddy fmt --overwrite Caddyfile`: tek, ilgisiz bir ön-var olan biçim tutarsızlığını
  (dosyanın en üstündeki gereksiz boş satır) düzeltti; bu görevin kendi eklediği satırlarda hiçbir değişiklik
  yapmadı.
- GERÇEK bir konteynerde canlı doğrulama: Caddy'nin kendi ikilisiyle gerçek `Caddyfile` bind-mount edilip
  `:443`'e yönlendirildi, `curl -k https://localhost:18443/` ile alınan gerçek HTTP yanıtı (404, çünkü test
  kurulumunda gerçek `api` upstream'i yoktu, ama başlıklar zaten yanıt seviyesinde uygulanıyor) dört başlığın
  TAMAMINI içeriyordu: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: same-origin`,
  `Permissions-Policy: geolocation=(), microphone=(), camera=()`. Test konteyneri ve geçici dosyalar sonrasında
  silindi.

## Handoff

- None
