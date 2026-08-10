# V14-QRO-003 - Implement QR confirmation and portion reservation

- Task ID: V14-QRO-003
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

Bekleyen bir QR order'yi onaylayın veya reddedin ve bölümleri yalnızca başarılı kabul üzerine ayırın.

## Owned surface

- `src/Modules/QrOrdering/Confirmation/**`, `tests/Modules/QrOrdering/Confirmation/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- İzin, satır sürümü, atomik Order geçişi, table politikası ve rezervasyon komutu.

## Out of scope

- Röle güvenliği ve mutfak hazırlama davranışı.

## Dependencies

- V14-QRO-001
- V14-QRO-002
- V11-RSV-002
- V1-IAM-002
- V14-STK-001
- V1-FND-005

## Deliverables

- `src/Modules/QrOrdering/Confirmation/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Onay, bir Kabul Edilen Order ve atomik olarak rezervasyon oluşturur; ret/stok kaybı hiçbir rezervasyon veya kısmi
  table durumu bırakmaz.

## Handoff

- V20-UAT-001
