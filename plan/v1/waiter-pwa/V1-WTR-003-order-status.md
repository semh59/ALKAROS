# V1-WTR-003 - Implement Waiter PWA Order status view

- Task ID: V1-WTR-003
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.8
- PDF:I.16

## Goal

Server-authoritative Order ve KitchenTicketItem progress durumunu reconnect-safe refresh ile göstermek.

## Owned surface

- `src/Clients/WaiterPwa/OrderStatus/**`, `tests/Clients/WaiterPwa/OrderStatus/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- SignalR/yeniden bağlanma geri dönüşü, durum etiketleri, iptal edilen öğeler ve eski göstergesi.

## Out of scope

- Mutfak status mutasyonu ve kasiyer payment durumu.

## Dependencies

- V1-WTR-001
- V1-KIT-001
- V1-OBS-001
- V0-CMP-005

## Deliverables

- `src/Clients/WaiterPwa/OrderStatus/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Public API/event contract varsa contract testleri dahil otomatik testler.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Bağlantının kesilmesi UI verilerin eski olduğunu gösterir; yeniden bağlanma kaynak durumuna yakınlaşır; Garson, mutfak
  durumunu görünüm aracılığıyla değiştiremez.
- Status görünümü `V0-CMP-005` kararındaki waiter success criteria listesini karşılar.

## Handoff

- None
