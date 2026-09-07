# V12-QRT-001 - Implement approved public QR relay transport

- Task ID: V12-QRT-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.6.5
- PDF:I.34-I.35
- PDF:II.7.3
- CORR:C22

## Goal

`V0-ARC-009` tarafından seçilen public gateway, local outbound connector ve durable outage queue topology'sini,
`V12-QRS-002` security contract'ını tekrar uygulamadan transport katmanına bağlamak. Provider ve restoran-başına
onboarding modeli `V12-QRT-002` ile Cloudflare Tunnel ve ALKAROS'un tek wildcard domain'i olarak somutlaştırıldı;
local connector implementasyonu `cloudflared` servis kaydı ve Cloudflare Tunnel API entegrasyonudur.

## Owned surface

- `src/Integrations/QrRelay/PublicGateway/**`, `src/Integrations/QrRelay/LocalConnector/**`
- `tests/Integrations/QrRelay/**`, `deploy/qr-relay/**`, `database/migrations/V12/V12-QRT-001/**`
- Sınırlı ek (var olan başka task'ların dosyalarına küçük, tanımlayıcı dokunuşlar — sahiplik iddiası değil): src/Host/ALKAROS.Host.csproj (yeni proje referansı), src/Host/Experience/RelaySettings alanı (V12-QRT-003; account/zone/domain alanları, provision uç noktası ve connector durumu eklendi), src/Clients/PosTerminal/src altında api.ts, contracts.ts, routes/RelaySettings.tsx, routes/RelaySettings.test.tsx (V12-QRT-003), tests/Host/Experience/RelaySettings alanı (V12-QRT-003), ALKAROS.slnx, database/MigrationComposition/order.json, src/Host/Composition/Migrations/MigrationManifest.cs, tests/Host/MigrationComposition/Manifest/ManifestTests.cs, docs/operations/qr-relay-setup-guide.md (V12-QRT-004; adım 2/3 şu ana kadar gerçekten teslim edilenle güncellendi), Directory.Packages.props (Microsoft.Extensions.Hosting.Abstractions/Logging.Abstractions sürüm kayıtları eklendi), deploy/docker/Dockerfile (paylaşılan dağıtım dosyası, tek bir görevin sahipliğinde değil — `api` aşamasına `cloudflared` ikili dosyası eklendi, sürüm+checksum ile sabitlendi; Alpine/musl üzerinde değişiklik yapılmadan çalıştığı doğrulandı).
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- TLS termination/wiring, outbound connection lifecycle, durable queue, reconnect, idempotent delivery, health/metrics,
  selected deployment assets ve `V12-QRS-002` control integration'ı.

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
  inbound port açmaz.
- `V12-QRS-002` replay/expired/revoked kararları transport tarafından aynen uygulanır; transport bu kuralları ikinci
  kez tanımlamaz. Outage queue crash/retry sonrasında mesajı kaybetmez veya ikinci kez uygulamaz.

## Handoff

- V20-INT-006
- V20-INS-001
- V20-SEC-001
