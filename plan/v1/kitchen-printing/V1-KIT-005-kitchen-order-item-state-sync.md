# V1-KIT-005 - Kitchen ticket item state sync to order items

- Task ID: V1-KIT-005
- Status: Blocked
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

`OrderItem.KitchenState` (`Orders/OrderAggregate/OrderEnums.cs`), üretimde
hiçbir zaman `NotSent`'in ötesine geçmiyor: `KitchenTicketItem`
(`Kitchen/TicketLifecycle/**`) `Queued/Preparing/Ready/Served/Cancelled`
durumlarını tamamen kendi tarafında takip ediyor, bunu yansıttığı
`OrderItem`'a geri yazan hiçbir orkestrasyon yok. Bu görev, **yalnız
`V1-SET-002`'nin `kitchen.live_sync_enabled` anahtarı açıkken**, bu
senkronizasyonu kurar; böylece `V1-WTR-009` (hazır bildirimi) ve `V1-IAM-027`
(gönderildi-ama-servis-edilmedi kalemi iptal) her zaman `NotSent` dönen sahte
bir alan yerine gerçek bir veriye bakabilir.

## Owned surface

- `plan/v1/kitchen-printing/V1-KIT-005-kitchen-order-item-state-sync.md`
- `src/Modules/Kitchen/OrderItemStateSync/**` (yeni, `TicketLifecycle`'a
  komşu ayrı bir alt dizin — bu proje modüller arası senkronizasyonu zaten
  outbox/integration-event deseniyle yapıyor, masa taşıma/birleştirmede
  kullanılan yöntemle aynı; `TicketLifecycle`'ın kendi dosyalarına
  dokunulmaz, yalnız onun genel arayüzleri tüketilir)
- `tests/Modules/Kitchen/OrderItemStateSync/**` (yeni)
- `evidence/V1-KIT-005/**`
- Bu görev, başka bir task'in owned surface alanını başka şekilde
  değiştiremez.

## In scope

- `KitchenTicketItem.TransitionTo` bir durum değişikliği yaptığında
  (`Queued→Preparing→Ready→Served` veya `→Cancelled`), ve
  `kitchen.live_sync_enabled` açıksa, ilgili `OrderItem.KitchenState`'i
  gerçekten günceller (`IOrderRepository` üzerinden, optimistic concurrency
  ile).
- Anahtar kapalıyken hiçbir davranış eklenmez (mevcut durum korunur).
- `OrderItem.KitchenState` alanı `Sent/Preparing/Ready` değerlerini artık
  gerçekten taşıyabilir hale gelir (enum zaten bu değerleri tanımlıyordu,
  yalnız hiç yazılmıyordu).

## Out of scope

- Bildirim (`V1-WTR-009`'un kapsamında).
- Void/waste akışı (`V1-IAM-027`'nin kapsamında).

## Dependencies

- V1-SET-002

## Blocker

- Kitchen ve Orders modülleri arasındaki senkronizasyon mekanizmasının tam
  şekli (hangi outbox/integration-event deseni, hangi arayüz çağrıları)
  mevcut masa taşıma/birleştirme senkronizasyonu incelenerek implementasyon
  sırasında netleşir; `IKitchenTicketRepository` / `IOrderRepository`
  arayüzlerini tüketmek dışında `TicketLifecycle`/`OrderAggregate`'in
  dosyalarına dokunulmayacağı için custody devri beklenmiyor. Ancak bu
  netleştiğinde ve `validate` temiz kaldığında görev `Planned` yapılabilir.

## Acceptance evidence

- (implementasyon tamamlandıktan sonra doldurulur.)

## Handoff

- V1-WTR-009
- V1-IAM-027
