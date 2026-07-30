# V14-QRT-001 - Implement approved public QR relay transport

- Task ID: V14-QRT-001
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
`V14-QRS-002` security contract'ını tekrar uygulamadan transport katmanına bağlamak.

## Owned surface

- `src/Integrations/QrRelay/PublicGateway/**`, `src/Integrations/QrRelay/LocalConnector/**`
- `tests/Integration/QrRelay/**`, `deploy/qr-relay/**`, `database/migrations/V14/V14-QRT-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- TLS termination/wiring, outbound connection lifecycle, durable queue, reconnect, idempotent delivery, health/metrics,
  selected deployment assets ve `V14-QRS-002` control integration'ı.

## Out of scope

- Order business validation, QR UI, topology/provider seçimi ve local LAN için public inbound listener.

## Dependencies

- V0-ARC-009
- V0-QRG-001
- V1-FND-002
- V1-FND-005
- V1-FND-006
- V1-SEC-001
- V1-SEC-002
- V14-QRS-002

## Deliverables

- Seçilen topology'ye ait gateway, local connector, deployment assets ve task-specific automated testler.
- Failure-injection, security-contract integration, outage/reconnect ve duplicate-delivery test kanıtları.

## Acceptance evidence

- Public request relay'e, oradan yalnız authenticated outbound local connection üzerinden dispatch edilir; LAN public
  inbound port açmaz.
- `V14-QRS-002` replay/expired/revoked kararları transport tarafından aynen uygulanır; transport bu kuralları ikinci
  kez tanımlamaz. Outage queue crash/retry sonrasında mesajı kaybetmez veya ikinci kez uygulamaz.

## Handoff

- V20-INT-006
- V20-INS-001
- V20-SEC-001
