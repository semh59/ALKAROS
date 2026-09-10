# V1-RMD-149 - Onay bekleyen QR siparişini kimse duymuyor

- Task ID: V1-RMD-149
- Status: Done
- Assignee: Claude Opus 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in sorusu (2026-09-10): "müşteri sipariş girdi, onay nasıl olacak? Bir
bildirim yok." Doğru — bir QR siparişi `PendingConfirmation`'a düşüyor ve
orada duruyor. `WaiterOrderStatusHub` tek bir olay taşıyor
(`OrderItemReady`) ve onu yalnız mutfak yayınlıyor; bekleyen siparişleri
listeleyen bir uç nokta da yok. Garson ancak o masanın siparişini elle
açarsa görebiliyor, yani misafir sipariş verir ve kimse fark etmez. Bu
görev iki eksiği birlikte kapatır: geldiği anda duyurmak ve duyuru kaçarsa
geri dönüp bakabilmek. Biri olmadan diğeri eksik kalır.

## Owned surface

- `plan/v1/remediation/V1-RMD-149-pending-qr-order-invisible-to-waiter.md` (yeni)
- `src/Modules/Orders/Integration/IPendingOrderAnnouncer.cs` (yeni) —
  Orders'ın kendi arayüzü; modül SignalR'ı bilmez.
- `src/Host/Experience/PendingOrderNotifications/**` (yeni) — arayüzün Host
  tarafındaki tek implementasyonu. Mevcut `WaiterNotifications` klasörü
  V1-WTR-009'un sahipliğinde olduğu için oraya dosya eklenmedi; hub'ın
  kendisi aşağıdaki sınırlı ek listesinde.
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır (yollar
  geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası olarak
  parse etmesin):
  - src/Host/Experience/WaiterNotifications/WaiterOrderStatusHub.cs
    (V1-WTR-009 sahipliğinde) — ikinci olay adı ve yükü.
  - src/Modules/Orders/Integration/QrOrderSubmittedConsumer.cs (V12-QRO-001
    sahipliğinde) — geçişten sonra duyuru.
  - src/Host/Experience/NfcOrdering/NfcOrderingStore.cs (V12-NFC-00x
    sahipliğinde) — yaş sınırlı NFC'nin kendi bekleme yolu.
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs,
    src/Host/Experience/Orders/OrderManagementStore.cs,
    src/Host/Experience/Orders/OrderManagementContracts.cs (Orders Management
    sahipliğinde) — bekleyen siparişleri listeleyen uç nokta.
  - src/Host/Experience/WaiterNotifications/WaiterNotificationsExperience.cs
    (V1-WTR-009 sahipliğinde) — duyurucunun DI kaydı. `DualScreenApplication.cs`
    zaten bu uzantıyı çağırdığı için ana kompozisyona dokunulmadı.
  - tests/Host/Experience/Orders/Confirmation/** (V1-RMD-1xx Confirmation
    sahipliğinde) — bekleyen liste uç noktasının kendi testleri.

## In scope

1. **Geldiği anda duyurulur.** `PendingConfirmation`'a geçen her sipariş
   `WaiterOrderStatusHub` üzerinden `OrderPendingConfirmation` olayıyla
   duyurulur; yük masa kimliğini, masa numarasını, kalem sayısını ve tutarı
   taşır ki istemci banner'ı ikinci bir çağrı yapmadan yazabilsin.
2. **Modül SignalR'ı bilmez.** Duyuru, Orders'ın kendi tanımladığı
   `IPendingOrderAnnouncer` arayüzünden geçer; Host onu SignalR'a bağlar.
   `IOrderSubmissionDispatcher`'ın (V1-ORD-002) aynı deseni — böylece
   `QrOrderSubmittedConsumer` bir Host tipine bağımlı olmaz. Arayüz
   opsiyoneldir: kayıtlı değilse duyuru sessizce atlanır, sipariş akışı
   etkilenmez.
3. **Duyuru kaçarsa liste var.** `GET /api/v1/terminals/{terminalId}/orders/pending`
   `PendingConfirmation` durumundaki siparişleri döndürür. Push kaçmış
   olabilir (uygulama kapalıydı, ağ gitti, cihaz yeni bağlandı) — açılışta
   ve yeniden bağlanmada banner'ın doğru görünmesi buna bağlı.
4. **İki kanal da aynı yoldan geçer.** QR (`QrOrderSubmittedConsumer`) ve
   yaş sınırlı NFC (`NfcOrderingStore`) `PendingConfirmation`'a giren tek
   iki yol; ikisi de aynı duyurucuyu çağırır.

## Out of scope

- Bildirimin belirli bir garsona hedeflenmesi: sistemde garson-masa
  ataması yok, bu yüzden `WaiterOrderStatusHub`'ın mevcut yayını gibi bu da
  bağlı her cihaza gider (hub'ın kendi doküman notu).
- Web Push (uygulama kapalıyken bildirim): ayrı bir altyapı; bu görev
  yalnız açık uygulamaya canlı duyuru yapar.
- Garson arayüzünün banner'ı çizmesi: ekranlar Faz 1'de baştan yazılıyor,
  bu görev sözleşmeyi ve olayı sağlar.

## Dependencies

- V1-RMD-146

## Acceptance evidence

- `dotnet build ALKAROS.slnx`: 0 Uyarı, 0 Hata.
- Gerçek Postgres'e karşı (`alkaros-test-pg`, port 55432), ayrı ayrı,
  gerçek çıkış koduyla:
  - `ALKAROS.Host.Experience.Orders.Confirmation.Tests`: **19/19** (17'den).
    İki yeni senaryo: bekleyen liste siparişi kalem sayısı ve tutarıyla
    döndürüyor, onaylandıktan sonra listeden düşüyor; oturumsuz çağrı 401.
  - `ALKAROS.Host.Experience.Orders.TableDraft.Tests`: 26/26,
    `...VoidSent.Tests`: 12/12, `ALKAROS.Host.Tests`: 134/134 — duyurucunun
    DI'da gerçekten çözüldüğü Composition testleri dahil.
  - `ALKAROS.Host.Experience.NfcOrdering.Tests`: 16/17 bir koşuda,
    diğerlerinde 17/17. Kırılan test
    `ConcurrentIdenticalFirstSubmissionsResolveToTheSameOrder` ve bu
    görevden bağımsız: `git stash` ile bu görevin TÜM değişiklikleri geri
    alınıp temel hâlde 5 kez çalıştırıldı, orada da 5 koşudan 1'i aynı
    şekilde kırıldı. V1-RMD-143 §4'ün TableDraft için raporladığı
    `orders_order_number_key` yarışının aynısı — hâlâ ayrı bir Task ID
    bekliyor.
- Migration yok.
- Semih'in elle deneyebileceği senaryo: bir masanın QR menüsünden sipariş
  ver; garson terminali açıkken bekleyen sipariş bildiriminin geldiğini,
  uygulamayı kapatıp açtığında ise
  `GET /api/v1/terminals/{terminalId}/orders/pending` çağrısının aynı
  siparişi hâlâ listelediğini, onayladıktan sonra listeden düştüğünü gör.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 1 önceden var olan
  ihlal (`InventoryAdjustmentService.cs:96`, bu görevden bağımsız), yeni
  ihlal yok.

## Handoff

- None
