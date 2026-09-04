# V1-WTR-009 - Order item ready push notification

- Task ID: V1-WTR-009
- Status: Done
- Assignee: claude-session-01XKRazppo9sW452rdbCZsgy
- Work type: implementation
- Surface state: Existing

## Goal

`V1-IAM-026`'nın kaydettiği kümenin bildirim halkası: bir sipariş kalemi
mutfakta "hazır" olduğunda, bağlı garson cihazlarına anlık bildirim gider —
`kitchen.live_sync_enabled` açıkken (`V1-SET-002`) ve `V1-KIT-005`'in gerçek
"Ready" geçişi yazdığı an. Bu proje zaten müşteri ekranı için anlık-güncelleme
altyapısına (SignalR, `CustomerDisplayHub`) sahip; aynı deseni garson
tarafına uzattı, sıfırdan kurmadı.

**Kapsam kararı (implementasyon sırasında bulunan gerçek engel):** Sistemde
hiçbir yerde "hangi garson hangi masaya/siparişe bakıyor" diye bir atama
kaydı yok (`Order` kaydı bile bunu tutmuyor). Bu yüzden bildirim, belirli bir
garsona değil, **bağlı her garson cihazına** düz bir yayın olarak gider;
gövde (`tableId`, `orderId`, `orderItemId`, ürün adı) taşır ki istemci kendi
bağlamına göre gösterip göstermeyeceğine karar verebilsin. Hedefli teslimat,
böyle bir atama modeli var olduğunda ayrı bir görev olarak eklenebilir.

## Owned surface

- `plan/v1/waiter-pwa/V1-WTR-009-order-ready-notification.md`
- `src/Host/Experience/WaiterNotifications/**`
- `tests/Host/Experience/WaiterNotifications/**`
- `src/Clients/WaiterPwa/wwwroot/vendor/**` (yeni — vendörlenmiş
  `@microsoft/signalr` 10.0.0 tarayıcı paketi; bu proje bundler kullanmıyor,
  CDN yerine yereldeki `node_modules`'tan kopyalandı, teslim-durumu felsefesi
  gereği)
- `evidence/V1-WTR-009/**`
- Paylaşılan dosyalarda sınırlı ek (V1-RMD-089/9. dalga deseni — sahiplik
  ilgili görevde kalır):
  `src/Host/DualScreen/DualScreenApplication.cs` (`V1-IAM-024` sahipliğinde
  kalır) — composition kökünde `AddWaiterNotificationsExperience()` /
  `MapWaiterNotificationsApi()` iki satırı (C3/OfflineReconciliation
  deseniyle aynı).
  `src/Host/Experience/KitchenOperations/KitchenOperationsStore.cs` ve
  `KitchenOperationsEndpoints.cs` (`V1-RMD-082` sahipliğinde kalır) —
  kurucuya `IOrderRepository orders, IHubContext<WaiterOrderStatusHub> waiterHub`
  eklendi; kalem "Ready" olduğunda (ve anahtar açıksa) yayın yapan özel bir
  yardımcı metot eklendi; DI kaydına `IOrderRepository` + Settings/OutboxStore
  ile aynı desende `AddWaiterNotificationsExperience()` eklendi.
  `src/Clients/WaiterPwa/wwwroot/waiter-app.js`, `waiter-app.css`,
  `index.html`, `sw.js` (`V1-WTR-006`/`V1-WTR-008` sahipliğinde kalır) —
  `connectOrderReadyHub`/`showOrderReadyBanner` fonksiyonları, bir CSS
  bildirim rozeti, vendörlenmiş SignalR script etiketi, servis işçisi önbellek
  listesine yeni dosya + önbellek sürümü artışı (`v1` → `v2`); mevcut hiçbir
  akış değiştirilmedi.

## In scope

- Yeni `WaiterOrderStatusHub` (SignalR, `/hubs/waiter-order-status`):
  bağlantı, `CustomerDisplayHub`'ın aynı deseniyle kasiyer çerezi +
  `terminalId` sorgu parametresiyle doğrulanır (`DualScreenStore.AuthenticateCashierAsync`);
  doğrulanamayan bağlantı reddedilir.
- `KitchenOperationsStore.TransitionItemAsync`, kalem `Ready` olduğunda (ve
  `kitchen.live_sync_enabled` açıksa) `OrderItemReady` olayını bağlı her
  istemciye yayınlar; siparişi yükleyip `TableId`'yi ekler (sipariş
  bulunamazsa bildirim sessizce atlanır — bildirim en-iyi-çaba, geçiş işlemini
  asla başarısız kılmaz).
- WaiterPwa istemcisi: girişten sonra (hem mevcut oturum hem yeni giriş
  yolunda) huba bağlanır; `OrderItemReady` geldiğinde ekranda 8 saniyelik bir
  rozet gösterir ve tarayıcı bildirim izni verilmişse bir `Notification` de
  tetikler (izin `default` ise ister).

## Out of scope

- Hedefli teslimat (belirli bir garson/masaya) — atama modeli yok, kapsam
  dışı (yukarıdaki not).
- Mutfak/void tarafı davranışı.

## Dependencies

- V1-KIT-005

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release` ve `-c Debug`: 0 uyarı / 0 hata.
- `dotnet test` (yerel Postgres 18): `ALKAROS.Host.Experience.WaiterNotifications.Tests`
  1/1 (yeni proje — hub rotası doğru yayımlanıyor);
  `ALKAROS.Host.Experience.KitchenOperations.Tests` 4/4,
  `ALKAROS.Host.Experience.Composition.Tests` 4/4,
  `ALKAROS.Architecture.Tests` 8/8 — hepsi regresyonsuz.
- `python tools/project-manifest/project_manifest_tool.py`: VALID.
- `python tools/consistency-audit/consistency_audit.py`: temiz.
- `node --check waiter-app.js` ve `node --check sw.js`: sözdizimi temiz.
  `python -m pytest tests/Clients/WaiterPwa/Frontend/test_waiter_pwa_frontend.py`:
  6/6 (mevcut yapısal testler, regresyon yok).
- **Ortam istisnası:** Bu oturumda gerçek bir tarayıcı/telefon üzerinde
  uçtan uca bildirim akışı (hub bağlantısı kurulup rozetin/`Notification`'ın
  gerçekten göründüğü) doğrulanmadı — WaiterPwa'nın kendisi zaten hiçbir
  JS/tarayıcı test koşucusuna sahip değil (yalnız Python yapısal testleri
  var), bu görev de o örüntüyü değiştirmedi. Sunucu tarafı (hub, yayın
  tetikleyicisi, rota) gerçek DI/composition testleriyle doğrulandı; istemci
  tarafı sözdizimi + kod incelemesiyle sınırlı.

## Handoff

- V1-IAM-027
