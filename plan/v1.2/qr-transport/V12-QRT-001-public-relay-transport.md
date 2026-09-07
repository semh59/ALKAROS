# V12-QRT-001 - Implement approved public QR relay transport

- Task ID: V12-QRT-001
- Status: Done
- Assignee: claude-session-01KUpNDVPb45EwMYysqeu1wc
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:I.6.5
- PDF:I.34-I.35
- PDF:II.7.3
- CORR:C22

## Goal

`V0-ARC-009` tarafından seçilen public gateway ve local outbound connector topology'sini, `V12-QRS-002` security
contract'ını tekrar uygulamadan transport katmanına bağlamak. Provider ve restoran-başına onboarding modeli
`V12-QRT-002` ile Cloudflare Tunnel ve ALKAROS'un tek wildcard domain'i olarak somutlaştırıldı; local connector
implementasyonu `cloudflared`'ı Host'un kendi alt süreci olarak yönetmek ve Cloudflare Tunnel API entegrasyonudur.
Cloudflare Tunnel'ın kendisi durable bir outage kuyruğu sağlamaz (bkz. Acceptance evidence) — dayanıklılık
istemci tarafı idempotent retry ile sağlanır, transport katmanında ayrı bir kuyruk implementasyonu yoktur.

## Owned surface

- `src/Integrations/QrRelay/PublicGateway/**`, `src/Integrations/QrRelay/LocalConnector/**`
- `tests/Integrations/QrRelay/**`, `deploy/qr-relay/**`, `database/migrations/V12/V12-QRT-001/**`
- `src/Clients/PosTerminal/src/api.test.ts` (yeni — `withIdempotentRetry`'nin kendi testi).
- Sınırlı ek (var olan başka task'ların dosyalarına küçük, tanımlayıcı dokunuşlar — sahiplik iddiası değil): src/Host/ALKAROS.Host.csproj (yeni proje referansı), src/Host/Experience/RelaySettings alanı (V12-QRT-003; account/zone/domain alanları, provision uç noktası ve connector durumu eklendi), src/Clients/PosTerminal/src altında api.ts, contracts.ts, routes/RelaySettings.tsx, routes/RelaySettings.test.tsx (V12-QRT-003), tests/Host/Experience/RelaySettings alanı (V12-QRT-003), ALKAROS.slnx, database/MigrationComposition/order.json, src/Host/Composition/Migrations/MigrationManifest.cs, tests/Host/MigrationComposition/Manifest/ManifestTests.cs, docs/operations/qr-relay-setup-guide.md (V12-QRT-004; adım 2/3 şu ana kadar gerçekten teslim edilenle güncellendi), Directory.Packages.props (Microsoft.Extensions.Hosting.Abstractions/Logging.Abstractions sürüm kayıtları eklendi), deploy/docker/Dockerfile (paylaşılan dağıtım dosyası, tek bir görevin sahipliğinde değil — `api` aşamasına `cloudflared` ikili dosyası eklendi, sürüm+checksum ile sabitlendi; Alpine/musl üzerinde değişiklik yapılmadan çalıştığı doğrulandı).
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- TLS termination/wiring, outbound connection lifecycle, reconnect, application-layer idempotent delivery (istemci
  tarafı retry + sunucu tarafı submission-id dedupe — bkz. Acceptance evidence), health/metrics, selected
  deployment assets ve `V12-QRS-002` control integration'ı.

## Out of scope

- Order business validation, QR UI, topology/provider seçimi ve local LAN için public inbound listener.

## Dependencies

- V0-ARC-009
- V12-QRT-002
- V0-QRG-001
- V1-FND-002
- V1-FND-005
- V1-FND-006
- V1-SEC-001
- V1-SEC-002
- V12-QRS-002

## Deliverables

- Seçilen topology'ye ait gateway, local connector, deployment assets ve task-specific automated testler.
- Failure-injection, security-contract integration, outage/reconnect ve duplicate-delivery test kanıtları.

## Acceptance evidence

- Public request relay'e, oradan yalnız authenticated outbound local connection üzerinden dispatch edilir; LAN public
  inbound port açmaz. — Gerçek Cloudflare hesabına karşı doğrulandı: `cloudflared`, LocalConnector tarafından
  yönetilen bir alt süreç olarak yalnız outbound QUIC bağlantısı kurar (4 aktif bağlantı, Cloudflare API'sinde
  `status: healthy` görüldü); host'ta hiçbir inbound port açılmaz.
- `V12-QRS-002` replay/expired/revoked kararları transport tarafından aynen uygulanır; transport bu kuralları ikinci
  kez tanımlamaz. — `SetTunnelConfigurationAsync` ile kurulan ingress kuralı isteği doğrudan `localhost:5080`'deki
  aynı API sürecine yönlendirir; transport hiçbir isteği kendi başına yorumlamaz veya doğrulamaz.
- Outage crash/retry sonrasında mesajı kaybetmez veya ikinci kez uygulamaz — **2026-09-08'de netleştirildi:**
  Cloudflare Tunnel'ın kendisi durable bir kuyruk sağlamaz (bağlayıcı kapalıyken gelen istek anında 502 alır,
  hiçbir yerde tutulmaz); bu, seçilen provider'ın (`V12-QRT-002`) mimari bir gerçeğidir, eksik bir implementasyon
  değil. Dayanıklılık bunun yerine **application katmanında, istemci tarafı idempotent retry** ile sağlanır:
  `V12-NFC-001`'in zaten üretimde olan deseni (istemci tarafından üretilen, tek bir gönderim denemesi boyunca
  sabit kalan `submissionId` + `ux_orders_table_submission` benzersiz kısıtı + çakışmada var olan siparişi
  döndürme) tam olarak bu garantiyi veriyor: bir istek kaybolup istemci (otomatik olarak, sınırlı sayıda ve
  geri-çekilmeli — `src/Clients/PosTerminal/src/api.ts`'teki `withIdempotentRetry`, yalnız 502/503/504/ağ hatası
  için, asla kesin bir reddetme için) tekrar denediğinde, sunucu ya siparişi ilk kez oluşturur ya da zaten var
  olanı aynen döndürür — asla ikinci bir sipariş yaratmaz. Bu desen, henüz yazılmamış QR sipariş uç noktaları
  (`V12-QRO-001/002/003`) için de aynı şekilde uygulanmalıdır.
- **Gerçek failure-injection (2026-09-07/08, canlı Cloudflare hesabına karşı):** çalışan `alkaros-api-1`
  container'ı içinde gerçek `cloudflared` süreci `pkill -9` ile öldürüldü (exit code 137); supervisor bunu bir
  sonraki poll'da tespit edip logladı ("cloudflared exited unexpectedly (exit code 137); restarting after
  backoff."), 5 saniyelik geri-çekilme sonrası yeniden başlattı; yeni süreç 4 yeni QUIC bağlantısı kaydetti ve
  Cloudflare API'si tüneli tekrar `status: healthy` olarak raporladı — hiçbir elle müdahale olmadan.
- `dotnet build ALKAROS.slnx`: 0 hata/0 uyarı. Yeni/genişletilen test projeleri: `ALKAROS.QrRelay.LocalConnector.Tests`
  5/5, `ALKAROS.QrRelay.PublicGateway.Tests` 18/18, `ALKAROS.Host.Experience.RelaySettings.Tests` 9/9 — hepsi hem
  yerel Postgres'e (55432) hem de tam docker compose test suite'ine karşı (121 testlik MigrationComposition dahil,
  container exit code 0, `docker inspect` ile doğrulandı). PosTerminal: `vitest run` 136/136 (yeni `api.test.ts`
  dahil, sahte zamanlayıcıyla gerçek gecikme olmadan), `tsc --noEmit` ve `vite build` temiz.
- Migration 083 (`qr_ordering.relay_tunnel`): boş bir veritabanında hem `up.sql` (şema+tablo oluşturuldu,
  `\dt` ile doğrulandı) hem `down.sql` (tablo temiz şekilde kaldırıldı) gerçek Postgres'e karşı elle doğrulandı.
- Semih'in elle deneyebileceği gerçek senaryo (bugün bizzat yapıldı): `/settings/relay`'e gerçek Cloudflare
  bilgilerini gir, Kaydet'e bas, bir alt-alan adı yazıp Bağlantıyı Etkinleştir'e bas — birkaç saniye içinde
  Cloudflare panelinde (Zero Trust > Networks > Tunnels) tünelin "Healthy" ve bağlı göründüğünü doğrula.

## Handoff

- V20-INT-006
- V20-INS-001
- V20-SEC-001
