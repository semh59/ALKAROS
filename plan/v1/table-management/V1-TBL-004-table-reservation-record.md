# V1-TBL-004 - Implement Table reservation records

- Task ID: V1-TBL-004
- Status: Done
- Assignee: Antigravity-v1-tbl-004
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.2.3
- PDF:II.3.16
- PDF:II.5.15
- PDF:III.5

## Goal

`Table.Reserved` arkasındaki onaylı actor, reason ve expiry modelini kalıcılaştırmak.

## Owned surface

- `src/Modules/Tables/Reservations/**`, `tests/Modules/Tables/Reservations/**`,
  `database/migrations/V1/V1-TBL-004/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Rezervasyon oluşturma, iptal etme ve süre sonlandırma ile atomik Table status projeksiyonu.

## Out of scope

- Rezervasyon rezervasyonu UI ve QR politikası beklemede.

## Dependencies

- V1-TBL-001
- V0-DOM-005

## Deliverables

- `src/Modules/Tables/Reservations/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Public API/event contract varsa contract testleri dahil otomatik testler.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Süresi dolan/iptal edilen rezervasyon yalnızca kendi table sürümünü yayınlar; eşzamanlı doluluk durumunun üzerine
  yazılmaz.

## Handoff

- V14-QRO-002
