# V1-RMD-170 - Bildirim kurulumu artık takılmıyor, gönderilmemiş tur kalıcı

- Task ID: V1-RMD-170
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

`docs/engineering/garson-audit-2026-09-10.md`'nin Frontend bölümünden iki
Medium bulguyu kapatır:

1. **Service worker kaydı başarısızsa `enablePush` sessizce takılıyor.**
   `registerOfflineWorker()` başarısız olduğunda `state.offlineDisabled =
   true` set ediyor ama uygulama çalışmaya devam ediyor. `enablePush()`
   sonra `await navigator.serviceWorker.ready`'yi bekliyordu — hiçbir
   servis işçisi asla etkinleşmeyeceği için bu Promise SONSUZA KADAR
   çözülmüyordu: hata yok, toast yok, "bildirimleri aç" sayfası bir daha
   asla yanıt vermiyordu. İki katman düzeltme: `state.offlineDisabled`
   iken en baştan açık bir Türkçe mesajla reddediliyor; ayrıca (kayıt
   "başarılı" görünüp işçi gerçekten hiç etkinleşmeyen daha nadir bir
   durum için) `navigator.serviceWorker.ready` artık 10 saniyelik bir
   zaman aşımına karşı yarıştırılıyor — mevcut `catch` bloğu zaten genel
   bir Türkçe hata gösteriyor.
2. **Gönderilmemiş tur yalnız bellekte, yeniden yüklemede kayboluyor.**
   `state.draftsByTable` (bir masadan ayrılırken bırakılan gönderilmemiş
   turlar) yalnız bellekteydi — kazara sayfa yenileme, tarayıcının
   belleği geri alması veya bir çökme, yazılmış ama gönderilmemiş her şeyi
   sessizce siliyordu, ne bir uyarı ne bir geri alma. Artık
   `offlineQueue`/`failedOrders` ile aynı desende `localStorage`'a
   yazılıyor: `afterDraftChange()` (her taslak değişikliğinden sonra zaten
   çağrılan tek merkez nokta) artık aktif masanın turunu da aynı
   haritaya/localStorage'a aynalıyor, masa değiştirirken zaten var olan
   "sakla" mantığı da persist ediyor. Bir masaya dönüldüğünde tur zaten
   aynı yoldan (var olan `openTable` mantığı) geri yükleniyor — yeni bir
   kod yolu eklenmedi, mevcut olan artık kalıcı.

## Owned surface

- `plan/v1/remediation/V1-RMD-170-push-hang-and-draft-persistence.md` (yeni)
- Sınırlı ek:
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js (V1-WTR-010/V1-WTR-011
    sahipliğinde) — `enablePush()`'a erken çıkış + zaman aşımı;
    `loadDraftsByTable`/`persistDraftsByTable` eklendi,
    `afterDraftChange`/`openTable` bunları çağırıyor.

## Out of scope

Frontend'in kalan bulguları — ayrı görev/görevler.

## Dependencies

- V1-RMD-169

## Acceptance evidence

- `node --check src/Clients/WaiterPwa/wwwroot/waiter-app.js` → temiz.
- Bu görev için ayrı bir otomatik test eklenmedi (repoda bu dosya için JS
  test altyapısı yok, önceki istemci değişiklikleriyle aynı durum) — kod
  gözden geçirildi: `loadDraftsByTable`'ın bozuk/yabancı localStorage
  içeriğinde `try/catch` ile başlangıcı asla çökertmediği, `persistDraftsByTable`'ın
  `Map`'i `[...state.draftsByTable]` ile JSON'a güvenle çevirdiği ve geri
  okurken `new Map(JSON.parse(raw))` ile birebir aynı şekle döndüğü elle
  doğrulandı.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyada 0 ihlal. (Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.)

## Handoff

- None
