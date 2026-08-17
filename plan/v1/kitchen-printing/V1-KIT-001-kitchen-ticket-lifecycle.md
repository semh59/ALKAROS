# V1-KIT-001 - Implement KitchenTicket and KitchenTicketItem lifecycles

- Task ID: V1-KIT-001
- Status: Done
- Assignee: Antigravity-v1-kit-001
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.16-I.20
- PDF:II.2.13
- PDF:II.3.13-II.3.14
- PDF:II.5.7-II.5.8
- PDF:II.8
- PDF:III.16

## Goal

Accepted Order'lardan station-scoped KitchenTicket üretmek ve KitchenTicketItem status'lerini bağımsız korumak.

## Owned surface

- `src/Modules/Kitchen/TicketLifecycle/**`, `tests/Modules/Kitchen/TicketLifecycle/**`,
  `database/migrations/V1/V1-KIT-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Bilet oluşturma, öğe durumu komutları, ebeveyne hazır toplama ve iptal kuralları.

## Out of scope

- Yazıcı yönlendirme ve fiziksel yazdırma işleri.

## Dependencies

- V1-ORD-001
- V0-DOM-001

## Deliverables

- `src/Modules/Kitchen/TicketLifecycle/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Karışık Hazırlanıyor/Hazır öğe durumları geçerlidir; ana Hazır yalnızca iptal edilmeyen her öğe Hazır veya Sunuldu
  olduğunda gerçekleşir.

## Handoff

- V1-KIT-002
- V1-KIT-003
