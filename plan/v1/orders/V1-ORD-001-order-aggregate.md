# V1-ORD-001 - Implement the channel-independent Order aggregate

- Task ID: V1-ORD-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.7-I.10
- PDF:II.2.4
- PDF:II.3.2
- PDF:II.5.1
- PDF:III.6

## Goal

Order ve OrderItem lifecycle, price snapshot, modifier ve Table/customer context davranışını uygulamak.

## Owned surface

- `src/Modules/Orders/OrderAggregate/**`, `tests/Modules/Orders/OrderAggregate/**`,
  `database/migrations/V1/V1-ORD-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Draft mutasyonu, ön koşulları gönderme, öğe iptali, anlık görüntüler ve standart geçiş uygulaması.

## Out of scope

- Envanter rezervasyonu, mutfak bileti oluşturma ve payment.

## Dependencies

- V1-FND-001
- V1-TBL-001
- V1-CAT-001
- V1-CAT-002
- V0-DOM-001

## Deliverables

- `src/Modules/Orders/OrderAggregate/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Order durum geçişleri contract ile eşleşir; katalog düzenlemelerinden sonra geçmiş fiyat/ad anlık görüntüleri
  değişmeden kalır.

## Handoff

- V1-ORD-002
- V1-KIT-001
- V1-BIL-001
