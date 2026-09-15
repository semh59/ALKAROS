# V1-RMD-203 - Hedefli bildirim, ulaşılamayan hedefte broadcast'e düşüyor

- Task ID: V1-RMD-203
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in isteğiyle ("Düzelt ve sonra... doğrula") bulunan gerçek bir
regresyon: `WaiterOrderStatusHub`'a (SignalR) ve push aboneliğine
**yalnız WaiterPwa** bağlanıyor — `src/Clients/Cashier` ve
`src/Clients/PosTerminal` bu hub'a hiç bağlanmıyor, push aboneliği hiç
kaydetmiyor (kod tabanında ikisine de sıfır referans). V1-RMD-111'in
tasarımı gereği bir masayı kim açtıysa `Order.ServingUserId` o olur —
Cashier/PosTerminal'den açılan bir masada bu, kasiyerin kendi UserId'si
demek. V1-RMD-201/202'nin hedefleme mantığı o UserId'ye hem SignalR hem
push gönderiyor, ama kasiyerin ikisi de yok — bildirim kimseye ulaşmıyor.
Önceki (yayın) davranış en azından bağlı garson cihazlarına ulaşıyordu;
hedefleme eklenirken bu senaryo (garson-dışı bir istemciden açılan masa)
kaçırıldı.

Düzeltme: hedeflemeden önce hedefin GERÇEKTEN ulaşılabilir olup
olmadığını kanala göre ayrı ayrı kontrol et. SignalR için "şu an bağlı
mı" — yeni bir canlı bağlantı sayacı (`WaiterPresenceTracker`) gerekiyor,
çünkü SignalR grup gönderimi üye sayısı hakkında geri bildirim vermiyor.
Push için zaten sorgulanabilir bir sinyal var (`push_subscriptions`
tablosunda satırı var mı). İkisi de "hayır" derse o kanalda eskisi gibi
herkese yayın yapılır — hiçbir bildirim sessizce kaybolmaz.

## Owned surface

Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır (yollar
geri-tik olmadan yazıldı, V1-RMD-111 emsali):

- src/Host/Experience/WaiterNotifications/** (V1-WTR-009 sahipliğinde) —
  yeni WaiterPresenceTracker.cs (bağlantı başına artan/azalan, thread-safe
  bir sayaç; IsConnected(userId)); WaiterOrderStatusHub.cs artık
  OnConnectedAsync'te sayacı artırıyor ve bağlanan kullanıcıyı
  Context.Items'a yazıyor, yeni override edilen OnDisconnectedAsync
  bağlantı kapanınca sayacı azaltıyor; WaiterNotificationsExperience.cs
  WaiterPresenceTracker'ı singleton olarak kaydediyor.
- src/Host/Experience/WebPush/WebPushSender.cs (V1-WTR-011 sahipliğinde)
  — yeni HasAnySubscriptionAsync(userId, ct).
- src/Host/Experience/KitchenOperations/KitchenOperationsStore.cs
  (paylaşılan dosya) — DispatchItemReadyNotificationAsync artık
  WaiterPresenceTracker parametresi alıyor; SignalR kanalı yalnız
  presence.IsConnected(id) true ise hedefleniyor, push kanalı yalnız
  HasAnySubscriptionAsync true ise hedefleniyor — ikisi ayrı ayrı karar
  veriyor, biri broadcast'e düşerken diğeri hedefli kalabilir.
- src/Host/Experience/PendingOrderNotifications/SignalRPendingOrderAnnouncer.cs
  (V1-RMD-149 sahipliğinde) — aynı iki-kanal-ayrı-karar deseni.
- tests/Host/Experience/WaiterNotifications/** (V1-WTR-009 test
  sahipliğinde) — WaiterPresenceTracker'ın kendi birim testleri, hub'ın
  disconnect'te sayacı azalttığını doğrulayan yeni test.
- tests/Host/Experience/WebPush/** (V1-WTR-011 test sahipliğinde) —
  HasAnySubscriptionAsync testi.
- tests/Host/Experience/KitchenOperations/** (V1-RMD-082 test
  sahipliğinde) — NotificationDispatchTests'e presence/push
  ulaşılabilirlik senaryoları eklendi.
- `tests/Host/Experience/PendingOrderNotifications/PendingOrderNotificationsTestDatabase.cs`,
  `tests/Host/Experience/PendingOrderNotifications/SignalRPendingOrderAnnouncerTests.cs`
  — V1-RMD-202 bu dosyaları kendi Owned surface'inde geri-tik olmadan
  ("yeni test dosyaları") yazdığı için münhasır sahiplik hiç tescil
  edilmemişti; bu görev onları devralıp aynı ulaşılabilirlik senaryolarını
  ekliyor.

## In scope

1. `WaiterPresenceTracker`: `Connected(userId)`/`Disconnected(userId)`/
   `IsConnected(userId)` — birden fazla cihaz/sekme aynı kullanıcıyla
   bağlanabildiği için sayaç (bool bayrak değil), 0'a inince kayıt
   siliniyor (sınırsız büyüme yok).
2. `WaiterOrderStatusHub`: bağlantı kapanınca (uygulama kapatılsa da,
   ağ kesilse de) `OnDisconnectedAsync` sayacı azaltıyor.
3. `WebPushSender.HasAnySubscriptionAsync`: `PushSubscriptionStore
   .GetByUserAsync`'in boş olup olmadığına bakıyor.
4. `KitchenOperationsStore`/`SignalRPendingOrderAnnouncer`: SignalR ve
   push kanalları artık BAĞIMSIZ karar veriyor — biri hedefli, diğeri
   broadcast olabilir (ör. kullanıcının push'u yok ama hub'a bağlı).

## Out of scope

- Cashier/PosTerminal'e bu bildirim kanallarından birini eklemek —
  ayrı, çok daha büyük bir görev (yeni istemci kablolaması); bu görev
  yalnız var olan hedefleme mantığının güvenli şekilde geri düşmesini
  sağlıyor.
- `waiter.max_active_tables` ayarı, kasiyer/garson elle masa açarken
  öneri — ayrı görevler.

## Dependencies

- V1-RMD-201
- V1-RMD-202

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- Gerçek Postgres'e karşı: `WaiterNotifications`, `WebPush`,
  `KitchenOperations`, `PendingOrderNotifications` test projelerinin
  tümü yeşil (yeni ulaşılabilirlik senaryoları dahil); revert-and-confirm
  ile en az bir yeni test gerçekten kırılıp doğrulanır.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal.
- Semih'in elle deneyebileceği senaryo: bir masayı Cashier/PosTerminal'den
  aç (kasiyer `ServingUserId` olur), mutfakta bir kalemi Ready'ye al —
  bağlı garson cihazları eskisi gibi bildirimi görür (artık sessizce
  kaybolmuyor). Aynı masayı bir garson WaiterPwa'dan açarsa, bildirim
  yalnız o garsona gider (V1-RMD-201'in davranışı bozulmadı).

## Handoff

- None
