# V11-RSV-003 - Implement cancellation release versus waste decision

- Task ID: V11-RSV-003
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.21-I.25
- PDF:II.2.12
- PDF:II.3.9
- PDF:II.5.6
- PDF:II.5.14
- PDF:III.14

## Goal

Açık mutfak durumunu kullanarak mutfak öncesi iptali Release'ye ve hazırlık sonrası iptali Waste'a çevirin.

## Owned surface

- `src/Modules/Inventory/PortionReservations/CancellationEffects/**`,
  `tests/Modules/Inventory/PortionReservations/CancellationEffects/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Durum araması, release/atık kararı, denetim nedeni ve tek seferlik yürütme; hareket kaydı V11-INV-006 sözleşmesi
  üzerinden yazılır (bu görev hareketi doğrudan üretmez).

## Out of scope

- Payment iade ve mutfak eşyası iptal uygulaması.

## Dependencies

- V11-RSV-001
- V11-INV-001
- V11-INV-006
- V11-INV-007
- V1-KIT-001

## Deliverables

- `src/Modules/Inventory/PortionReservations/CancellationEffects/**` altında Goal kapsamını uygulayan production code ve
  task-specific automated test assets.
- Aynı davranışın otomatik başarı, ret ve concurrency/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Hazırlık, kullanılabilirliği yeniden sağlamadan önce iptal edilir; taahhüt edilen hazırlıktan sonra iptal edilmez;
  retry her iki etkiyi de kopyalamaz.
- Her Release/Waste sonucu `V11-INV-007` reserved/available projection'ında tam bir kez görünür ve full rebuild ile
  eşleşir.

## Handoff

- V11-MNU-002
- V14-STK-001
- V12-ALC-003
