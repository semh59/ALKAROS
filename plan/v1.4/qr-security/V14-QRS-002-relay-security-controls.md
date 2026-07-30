# V14-QRS-002 - Implement QR relay authentication and abuse controls

- Task ID: V14-QRS-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.34-I.37
- PDF:II.2.18
- PDF:II.6.8
- PDF:II.7.3
- PDF:III.21

## Goal

Relay message authentication yapmak ve local command dispatch öncesi replay, rate-limit ve payload-size kontrollerini
uygulamak.

## Owned surface

- `src/Modules/QrOrdering/RelaySecurity/**`, `tests/Modules/QrOrdering/RelaySecurity/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- İmza/anahtar rotasyonu, tek seferlik, zaman damgası penceresi, jeton başına/IP limitleri ve güvenli reddetme.

## Out of scope

- QR order iş doğrulaması ve yerel ağ dağıtımı.

## Dependencies

- V14-QRS-001
- V0-QRG-001
- V1-FND-002

## Deliverables

- `src/Modules/QrOrdering/RelaySecurity/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Tekrar oynatma ve değiştirilmiş veriler reddedilir; oran sınırı Order üretmez; Yerel hizmet, genel gelen uç noktayı
  göstermez.

## Handoff

- V14-QRO-001
