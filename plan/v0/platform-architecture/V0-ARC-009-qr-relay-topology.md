# V0-ARC-009 - Define QR relay production topology

- Task ID: V0-ARC-009
- Status: Blocked
- Assignee: codex-v0-arc-009
- Work type: decision
- Surface state: Existing

## Source basis

- PDF:I.6.5
- PDF:I.34-I.35
- PDF:II.7.3
- CORR:C22

## Goal

Public QR ingress ile local POS arasındaki production relay topology'sini inbound LAN erişimi açmadan tek bağlayıcı
kararla tanımlamak.

## Owned surface

- `docs/architecture/qr-relay-topology.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Public ingress, local outbound connector, managed/self-hosted ownership, TLS/domain, authentication/key custody,
  durable queue/retention, deployment boundary, monitoring ve outage sorumluluğu.

## Out of scope

- Relay implementation, cloud sağlayıcı satın alma, QR UI ve Order business logic.

## Dependencies

- V0-ARC-001
- V0-ARC-003
- V0-ARC-005
- V0-SEC-001

## Blocker

- Candidate evidence, `V0-ARC-001` `Done` olmadan kabul edilemez; ancak tam
  dependency zinciri kapatılıp acceptance yeniden doğrulanınca görev `Planned` olur.

## Deliverables

- Tek decision record: kaynaklar, erişim tarihi, onaylayan, seçilen topology, reddedilen alternatifler ve etkilenen task
  kimlikleri.
- Trust boundary, data-flow, key-custody, queue-retention ve deployment ownership matrisi.

## Acceptance evidence

- Karar, public request'ten authenticated local dispatch'e kadar her hop'un sahibi, protocol'ü ve failure davranışını
  adlandırır; local network için public inbound port tanımlamaz.
- Seçilmeyen topology veya sağlayıcı için production kodu/projesi oluşturulamaz.

## Handoff

- V0-QRG-001
- V1-FND-001
- V14-QRT-001
