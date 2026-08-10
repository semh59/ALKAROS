# V14-QRO-002 - Implement PendingConfirmation table policy

- Task ID: V14-QRO-002
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
- CORR:C5

## Goal

Uzaktan QR hizmet reddine izin vermeden onaylı dolu/ayrılmış/değişiklik yok table davranışını uygulayın.

## Owned surface

- `src/Modules/QrOrdering/TablePolicy/**`, `tests/Modules/QrOrdering/TablePolicy/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Personel oturma kanıtları, sürenin dolmasını bekleme, iyimser eşzamanlılık ve reddedilme geri dönüşü.

## Out of scope

- Order onayı ve genel table yaşam döngüsü uygulaması.

## Dependencies

- V14-QRO-001
- V1-TBL-001
- V0-DOM-005

## Deliverables

- `src/Modules/QrOrdering/TablePolicy/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Eski/mükerrer QR tek başına ücretsiz bir table'yi süresiz olarak alamaz; eşzamanlı personel durumu değişikliği
  kazanır veya açık bir çatışmaya neden olur.

## Handoff

- V14-QRO-003
