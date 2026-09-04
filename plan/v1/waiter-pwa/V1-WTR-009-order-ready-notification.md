# V1-WTR-009 - Order item ready push notification

- Task ID: V1-WTR-009
- Status: Blocked
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

`V1-IAM-026`'nın kaydettiği kümenin bildirim halkası: bir sipariş kalemi
mutfakta "hazır" olduğunda, o siparişi alan garsonun WaiterPwa'sına anlık
bildirim gider — `kitchen.live_sync_enabled` açıkken (`V1-SET-002`) ve
`V1-KIT-005`'in gerçek "Ready" geçişi yazdığı an. Bu proje zaten müşteri
ekranı için anlık-güncelleme altyapısına (SignalR, `CustomerDisplayHub`)
sahip; aynı deseni garson tarafına uzatır, sıfırdan kurmaz.

## Owned surface

- `plan/v1/waiter-pwa/V1-WTR-009-order-ready-notification.md`
- `src/Host/WaiterNotificationHub.cs` (yeni — `CustomerDisplayHub` deseni)
- `src/Clients/WaiterPwa/OrderReadyNotification/**` (yeni)
- `tests/Clients/WaiterPwa/OrderReadyNotification/**` (yeni)
- `evidence/V1-WTR-009/**`
- Bu görev, başka bir task'in owned surface alanını başka şekilde
  değiştiremez.

## In scope

- `V1-KIT-005`'in yazdığı `Ready` geçişini dinleyip, siparişi alan garsonun
  (order'ın serving user'ı) bağlı cihazına bir SignalR olayı yayınlamak.
- WaiterPwa tarafında bu olayı dinleyip bir bildirim/rozet göstermek.
- Garson uygulaması açık değilse (bağlantı yoksa) olay kaybolur — bu, "asenkron
  bildirim" için kabul edilebilir; kalıcı bir bildirim kuyruğu bu görevin
  kapsamında değildir.

## Out of scope

- Push notification (tarayıcı kapalıyken bildirim) — yalnızca uygulama açıkken
  anlık bağlantı.
- Mutfak/void tarafı davranışı.

## Dependencies

- V1-KIT-005

## Blocker

- `CustomerDisplayHub`'ın tam deseni (bağlantı yönetimi, grup/oda modeli)
  incelenip garson tarafına nasıl uzatılacağı implementasyon sırasında
  netleşir; dokunulacak paylaşılan dosyalar (`DualScreenApplication.cs` gibi)
  için sınırlı-ek notları o zaman eklenir. Ancak bu netleşip `validate` temiz
  kaldığında görev `Planned` yapılabilir.

## Acceptance evidence

- (implementasyon tamamlandıktan sonra doldurulur.)

## Handoff

- V1-IAM-027
