# V1-RMD-209 - Hedefli bildirim gönderimi tek yere toplandı, push TOCTOU kapatıldı

- Task ID: V1-RMD-209
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in isteğiyle çalıştırılan bağımsız `/code-review` denetiminin
(2026-09-15, base `69280f1e..HEAD`) bulduğu üç ilişkili gerçek bulgu:

1. **Tekrar eden kod:** V1-RMD-201/202/203'ün "hedefle, ulaşılamıyorsa
   yayına düş" mantığı `KitchenOperationsStore.
   DispatchItemReadyNotificationAsync` ve `SignalRPendingOrderAnnouncer.
   AnnounceAsync`'te birebir kopyalanmıştı — biri düzeltilip diğeri
   unutulma riski taşıyordu (zaten bir kopyada `push` kanalının
   ulaşılabilirlik kontrolü eksik kalmıştı, aşağıya bakın).
2. **Push'ta TOCTOU:** `HasAnySubscriptionAsync` ve `SendToUserAsync`
   ayrı ayrı sorgulanıyordu; ikisi arasında tek abonelik silinirse
   (ölü cihaz temizliği) `SendToUserAsync` sessizce sıfır cihaza
   gönderiyor, yayına hiç düşmüyordu.
3. **Gereksiz çift sorgu:** aynı kullanıcının abonelikleri her çağrıda
   iki kez okunuyordu (`HasAnySubscriptionAsync` + `SendToUserAsync`).

## Owned surface

Sınırlı ek (yollar geri-tik olmadan, V1-RMD-111 emsali):

- src/Host/Experience/WaiterNotifications/WaiterNotificationDispatch.cs
  (yeni dosya, V1-WTR-009 sahipliğindeki klasörde) — V1-RMD-201/202/203'ün
  hedefleme kararının tek, paylaşılan hâli: SignalR grup/All + push
  hedefli/yayın kararını tek yerde verir.
  - src/Host/Experience/WebPush/WebPushSender.cs (V1-WTR-011
    sahipliğinde) — `HasAnySubscriptionAsync`+`SendToUserAsync` yerine
    tek sorguyla karar veren yeni `SendToUserOrBroadcastAsync` (TOCTOU
    ve çift sorgu ikisi de kapanıyor).
  - src/Host/Experience/KitchenOperations/KitchenOperationsStore.cs
    (paylaşılan dosya) — `DispatchItemReadyNotificationAsync` artık
    `WaiterNotificationDispatch.SendAsync`'i çağırıyor, kendi kopyası
    silindi.
  - src/Host/Experience/PendingOrderNotifications/SignalRPendingOrderAnnouncer.cs
    (V1-RMD-149 sahipliğinde) — `AnnounceAsync` aynı şekilde.
  - tests/Host/Experience/KitchenOperations/**,
    tests/Host/Experience/PendingOrderNotifications/**,
    tests/Host/Experience/WebPush/** (ilgili görevler sahipliğinde) —
    davranış aynı kaldığı için mevcut testler değişmedi; yeni
    `SendToUserOrBroadcastAsync` için yeni testler eklendi.

## In scope

1. `WaiterNotificationDispatch.SendAsync`: SignalR kanalı `presence.
   IsConnected` ile, push kanalı `WebPushSender.
   SendToUserOrBroadcastAsync` ile — ikisi bağımsız karar veriyor,
   davranış V1-RMD-203'ten beri aynı, yalnız artık tek yerde.
2. `WebPushSender.SendToUserOrBroadcastAsync`: abonelikler TEK sorguyla
   okunur; doluysa o abonelere gönderilir, boşsa `BroadcastAsync`'e
   düşülür — okuma ile kullanım arasında pencere kalmıyor.
3. `HasAnySubscriptionAsync`/`SendToUserAsync` kaldırıldı (yalnız bu iki
   çağrı yeriydi, artık kullanılmıyor).

## Out of scope

- SignalR tarafındaki "bağlantı koptu ama henüz `OnDisconnectedAsync`
  tetiklenmedi" penceresi — SignalR'ın kendi bağlantı-kopma algılama
  gecikmesi, bu görevin kapsamında çözülebilecek bir şey değil (ayrı,
  çok daha büyük bir iş — sunucu tarafı heartbeat/ack mekanizması
  gerektirir).
- `IsValidWaiterAsync`'in yetkilendirme altyapısını yeniden kullanması —
  V1-RMD-210.
- Bölge sorgusunun tekilleştirilmesi, Cashier'ın paralel fetch'i —
  V1-RMD-211.

## Dependencies

- V1-RMD-203

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- Gerçek Postgres'e karşı: `KitchenOperations`, `PendingOrderNotifications`,
  `WebPush` test projelerinin tümü yeşil (davranış değişmedi, yalnız
  kod tekilleşti); yeni `SendToUserOrBroadcastAsync` testleri (tek
  abonelikli → ona gider, sıfır abonelikli → yayına düşer) eklendi;
  revert-and-confirm ile en az bir test gerçekten kırılıp doğrulanır.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal.
- Semih'in elle deneyebileceği senaryo: V1-RMD-201/202/203'ün elle
  senaryoları aynen geçerli — davranış değişmedi, yalnız kod tabanında
  tek bir yerden yönetiliyor.

## Handoff

- None
