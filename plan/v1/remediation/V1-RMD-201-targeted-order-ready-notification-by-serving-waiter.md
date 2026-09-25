# V1-RMD-201 - "Sipariş hazır" bildirimi artık atanmış garsona hedefleniyor

- Task ID: V1-RMD-201
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`WaiterOrderStatusHub`'ın kendi doc-comment'i ve `WebPushSender.BroadcastAsync`'in
kendi doc-comment'i, ikisi de aynı gerekçeyle ("sistemde hangi garsonun hangi
masaya baktığı hiç kaydedilmiyor") her "sipariş hazır" bildirimini TÜM bağlı
garson cihazlarına yayınlıyordu. Bu gerekçe artık yanlış: `Order.ServingUserId`
(V1-RMD-111, 2026-09-06'dan beri `Done`) siparişi oluşturan garsonu zaten
damgalıyor ve yalnızca açık bir devirle değişiyor. `WaiterOrderStatusHub`'ın
kendi yorumu bunu zaten öngörmüştü: "Targeted delivery is a natural follow-on
once/if a waiter-table assignment model exists." Bu görev o takip işini yapar:
bir siparişin atanmış bir garsonu varsa (`ServingUserId != null`), "hazır"
bildirimi hem SignalR hem web push kanalında yalnız o garsona gider; atanmış
garson yoksa (ör. eski/terminal-geneli sipariş) önceki yayın davranışı
korunur.

## Owned surface

Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır (yollar
geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası olarak
parse etmesin, V1-RMD-111 emsali):

- src/Host/Experience/WaiterNotifications/WaiterOrderStatusHub.cs
  (V1-WTR-009 sahipliğinde) — OnConnectedAsync artık kimlik doğrulanan
  kullanıcıyı kendi UserId'sine ait bir SignalR grubuna (GroupName(userId))
  katıyor.
- src/Host/Experience/KitchenOperations/KitchenOperationsStore.cs
  (paylaşılan dosya) — NotifyWaitersItemIsReadyAsync artık
  order.ServingUserId varsa hedefli SignalR grubu + hedefli push kullanan
  yeni public static DispatchItemReadyNotificationAsync'e devrediyor
  (public, çünkü bu Host projesinde InternalsVisibleTo hiç kullanılmıyor —
  OrderDtoAssembler'ın aynı emsali); yoksa eski Clients.All/BroadcastAsync
  davranışı aynen kalıyor.
- src/Host/Experience/WebPush/WebPushSender.cs (V1-WTR-011 sahipliğinde) —
  yeni SendToUserAsync(message, userId, ct), BroadcastAsync ile aynı
  gönderim/hata-yutma mantığını (ortak SendToSubscriptionsAsync'e
  çıkarılarak) tek bir kullanıcının aboneliklerine uygular.
- src/Host/Experience/WebPush/PushSubscriptionStore.cs (V1-WTR-011
  sahipliğinde) — yeni GetByUserAsync(userId, ct).
- tests/Host/Experience/WaiterNotifications/** (V1-WTR-009 test
  sahipliğinde) — yeni WaiterOrderStatusHubGroupTests.cs (elle yazılmış
  IGroupManager/HubCallerContext sahteleriyle, gerçek bir kimlik-doğrulanmış
  cookie/terminal çifti kullanarak OnConnectedAsync'in doğru gruba
  katıldığını doğrular), yeni WaiterNotificationsTestDatabase.cs
  (WebPushTestDatabase.SeedCashierSessionAsync'in aynı deseniyle bu test
  projesine özel bir cashier-oturumu seed yardımcısı), ve
  ALKAROS.Host.Experience.WaiterNotifications.Tests.csproj'a Postgres
  bağımlılığı için paket referansları (mevcut diğer Host.Experience test
  projeleriyle aynı desen).
- tests/Host/Experience/WebPush/** (V1-WTR-011 test sahipliğinde) —
  WebPushTestDatabase.cs'e yeni SeedUserAsync(displayName) (yalnız
  identity.users'a bir satır ekleyip id'sini döndürüyor — yeni testlerin
  gerçek bir user_id FK'sine ihtiyacı var, cihaz oturumuna değil), yeni
  PushSubscriptionStoreTests.cs (GetByUserAsync'in yalnız o kullanıcının
  kayıtlarını döndürdüğünü doğrular), yeni WebPushSenderTests.cs (sahte bir
  HttpMessageHandler ile SendToUserAsync'in yalnız hedef kullanıcının
  endpoint'ine POST attığını, diğer kullanıcının aboneliğine hiç
  dokunmadığını kanıtlar).
- tests/Host/Experience/KitchenOperations/** (V1-RMD-082 test sahipliğinde)
  — yeni KitchenOperationsStore.NotificationDispatchTests.cs, elle yazılmış
  bir `IHubContext<WaiterOrderStatusHub>` sahtesiyle
  DispatchItemReadyNotificationAsync'in ServingUserId varken
  Clients.Group(GroupName(id))'i, yokken Clients.All'ı çağırdığını doğrudan
  doğrular (Postgres/tam store kurulumu gerektirmez — yalnız ilgili statik
  metodu çağırır).

## In scope

1. `WaiterOrderStatusHub.OnConnectedAsync`: kimlik doğrulanan bağlantı,
   `Groups.AddToGroupAsync(Context.ConnectionId, GroupName(principal.UserId))`
   ile kendi kullanıcı grubuna katılır.
2. `KitchenOperationsStore`'un "sipariş hazır" bildirimi: `ServingUserId`
   doluysa yalnız o gruba/kullanıcıya, boşsa (ör. `ServingUserId == null`)
   önceki gibi herkese.
3. `WebPushSender.SendToUserAsync`: `BroadcastAsync`'in aynı best-effort/
   ölü-abonelik-temizleme sözleşmesini tek kullanıcıya uygular.
4. `OrderPendingConfirmation` olayı bilinçli olarak DOKUNULMADI — bir
   siparişin henüz onaylanmamış/`PendingConfirmation` durumunda olması
   tanım gereği henüz kimseye atanmadığı anlamına gelir (`ServingUserId`
   bu noktada zaten null), bu yüzden yayın davranışı zaten doğru ve
   değişmiyor.

## Out of scope

- `waiter.max_active_tables` ayarı, yük dengeleme, yeni masa açılışında
  otomatik garson önerisi — ayrı görev(ler).
- PosTerminal/Cashier tarafında herhangi bir istemci değişikliği — bu
  görev yalnız sunucu tarafı dağıtım hedeflemesini değiştiriyor, mevcut
  istemcilerin (WaiterPwa) hiçbiri hangi kanaldan geldiğini ayırt etmiyor,
  değişiklik şeffaf.
- Kalıcı garson-masa "atama" kavramı (bir garson bir masaya "atanır" ama
  henüz sipariş girmemiş olabilir) — bu görev yalnız var olan
  `Order.ServingUserId`'yi tüketiyor, yeni bir atama modeli kurmuyor.

## Dependencies

- V1-RMD-111

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `ALKAROS_TEST_PG_PORT=<port> ALKAROS_TEST_PG_PASSWORD=postgres dotnet test`:
  - `tests/Host/Experience/WaiterNotifications/*.csproj` → tüm testler
    yeşil (yeni grup-katılım testi dahil).
  - `tests/Host/Experience/WebPush/*.csproj` → tüm testler yeşil (yeni
    `PushSubscriptionStoreTests`, `WebPushSenderTests` dahil).
  - `tests/Host/Experience/KitchenOperations/*.csproj` → tüm testler yeşil
    (yeni `NotificationDispatchTests` dahil).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal.
- Semih'in elle deneyebileceği senaryo: iki garson hesabıyla (V1-RMD-110/111
  senaryosundaki gibi) iki ayrı masaya sipariş aç, ikisinin de WaiterPwa'sını
  aynı anda açık tut; mutfakta bir kalemi Ready'ye al — yalnız o siparişin
  garsonunun cihazında bildirim çıkar, diğerinde çıkmaz. `ServingUserId`
  taşımayan (ör. hızlı-satış) bir siparişte eskisi gibi her iki cihazda da
  bildirim çıkar.

## Handoff

- None
