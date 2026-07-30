# V14-STK-001 - Implement cross-channel last-portion arbitration

- Task ID: V14-STK-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.34-I.37
- PDF:II.2.19
- PDF:II.7.4
- PDF:III.22

## Goal

Cashier, waiter, QR ve online channel için tek channel-neutral reservation command ve ortak last-portion arbitration
sonucu sağlamak.

## Owned surface

- `src/Modules/Inventory/CrossChannelReservation/**`, `tests/Modules/Inventory/CrossChannelReservation/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Kanaldan bağımsız komut, eşzamanlılık sonuç eşlemesi ve provider reddetme telafisi.

## Out of scope

- Rezervasyon yaşam döngüsü dahili bileşenleri ve provider status aktarımı.

## Dependencies

- V11-RSV-002
- V11-RSV-003

## Deliverables

- `src/Modules/Inventory/CrossChannelReservation/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Tek bölümlü paralel dört kanallı test, bir rezervasyon ve üç açık OutOfStock/red sonucu verir.
- Mutfak başlamadan iptal tam olarak bir Release, hazırlık başladıktan sonra iptal tam olarak bir Waste üretir;
  crash/retry aynı stok etkisini ikinci kez oluşturmaz.

## Handoff

- V14-QRO-003
- V14-ONL-002
