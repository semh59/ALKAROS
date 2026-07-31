# V0-QRG-001 - Validate QR relay threat model and feasibility

- Task ID: V0-QRG-001
- Status: Blocked
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Planned

## Source basis

- PDF:I.6
- PDF:I.6.5

## Goal

Public QR trafiğinin local POS'a inbound LAN erişimi açmadan taşınabileceğini kanıtlamak.

## Owned surface

- `evidence/v0/integrations/V0-QRG-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Outbound connector, authentication, token rotation, replay, rate limit, outage queue ve revocation.

## Out of scope

- QR ordering UI ve Order aggregate.

## Dependencies

- V0-ARC-009
- V0-ARC-003

## Blocker

`V0-ARC-009` onaylı topology kararı ile adlandırılmış non-production relay/domain, TLS kimliği, credentials ve test
erişimi mevcut değildir. Görev ancak karar `Done` olduğunda ve bu erişimler sağlandığında `Planned` olabilir; gerçek
outage/replay/revocation transkriptleri `Done` acceptance kanıtıdır.

## Deliverables

- V0-QRG-001 için tarihli ve kaynakları belirtilmiş evidence package.
- Başarı ve en az bir gerçek hata/edge-case çıktısı.
- Doğrulanamayan maddeler için açık blocker kaydı; varsayımla kapatma yok.

## Acceptance evidence

- Threat modelde açık critical risk yok ve local network'e public inbound port açmadan çalışan proof mevcut.

## Handoff

- V14-QRT-001
- V14-QRS-001
- V14-QRS-002
