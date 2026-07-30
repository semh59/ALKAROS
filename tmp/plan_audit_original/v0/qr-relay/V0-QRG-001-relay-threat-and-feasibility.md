# V0-QRG-001 - Validate QR relay threat model and feasibility

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation

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

- V0-ARC-002,V0-ARC-003

## Deliverables

- V0-QRG-001 için tarihli ve kaynakları belirtilmiş evidence package.
- Başarı ve en az bir gerçek hata/edge-case çıktısı.
- Doğrulanamayan maddeler için açık blocker kaydı; varsayımla kapatma yok.

## Acceptance evidence

- Threat modelde açık critical risk yok ve local network'e public inbound port açmadan çalışan proof mevcut.

## Handoff

- V14-QRS-001 ve V14-QRS-002.

