# V1-RMD-113 - submit-draft kitchen dispatch, idempotency, and customer-display notification

- Task ID: V1-RMD-113
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: implementation
- Surface state: Existing

## Goal

Semih onayıyla ("Düzeltme planı yapalım ve sırayla yapalım... bana
sormadan bitir", 2026-09-06), Dalga 2/N: `docs/audit/INDEPENDENT_DEEP_AUDIT_2026-09-06.md`'nin
bizzat doğruladığım en ağır Backend API bulgusunu kapatır — WaiterPwa ve
Cashier'ın masa siparişi göndermek için kullandığı **tek** yol
(`table-draft` + `submit-draft`) sipariş durumunu `Submitted` yapıyordu
ama:

1. Hiçbir mutfak bileti oluşturmuyordu — terminal-geneli hızlı-satış
   `/submit` yolunun kullandığı `KitchenOrderSubmissionDispatcher`'a hiç
   bağlı değildi.
2. `SubmitTableOrderRequest.OperationId` alanı vardı ama okunup çöpe
   atılıyordu — hiçbir idempotency kontrolü yoktu.
3. Müşteri ekranına hiçbir SignalR bildirimi göndermiyordu.

Bu üçü, terminal-geneli `/submit` yolunun `SubmitOrderHandler`
aracılığıyla baştan beri sahip olduğu üç garanti — bu görev
`OrderManagementStore.SubmitOrderAsync`'i o AYNI, olgun, test edilmiş
sınıfı kullanacak şekilde yeniden yazdı, ayrı ve daha ince bir yeniden
uygulama yerine.

Bu düzeltmeyi doğrularken ikinci, bağımsız bir kök bulgu ortaya çıktı:
`SubmitOrderRequestHash.Compute` `SubmittedAt`'i hash'e dahil ediyordu —
her çağıran taze bir `DateTimeOffset.UtcNow` geçtiği için, GERÇEK bir
istemci tekrarı (bağlantı koptuktan sonra) her zaman ilk denemeden
FARKLI bir zaman damgası taşır ve sahte bir `IDEMPOTENCY_KEY_CONFLICT`
üretirdi. Bu, terminal-geneli `/submit` yolunu da (hiç fark edilmeden,
çünkü onu hiçbir otomatik test uçtan uca tekrar denemiyordu) etkileyen
paylaşılan bir kusurdu; bu görevin kendi regresyon testi bunu bulan ilk
gerçek çağrı oldu.

## Owned surface

- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik
  iddiası olarak parse etmesin):
  - src/Host/Experience/Orders/OrderManagementStore.cs,
    OrderManagementEndpoints.cs, OrderManagementContracts.cs (V1-ORD-005
    sahipliğinde) — SubmitOrderAsync artık SubmitOrderHandler'a devrediyor
    (terminalId, operationId, actorId parametreleri eklendi);
    SubmitTableOrderRequest.ClientId kaldırıldı (sunucu türetiyor),
    OperationId artık zorunlu; submit-draft uç noktası CustomerDisplayHub'a
    bildirim gönderiyor; exception filter'a 4 yeni eşleme eklendi
    (OrderNotFoundException, StaleOrderVersionException,
    SubmitOrderIdempotencyConflictException, OrderSubmissionDispatchException).
  - src/Modules/Orders/SubmitOrder/SubmitOrderRequestHash.cs ve
    tests/Modules/Orders/SubmitOrder/SubmitOrderTests.cs (V1-ORD-002
    sahipliğinde) — SubmittedAt hash'ten çıkarıldı, 1 yeni test.
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js,
    src/Clients/Cashier/wwwroot/cashier-app.js (ilgili istemci görevleri
    sahipliğinde) — submit-draft isteğine belirleyici (rastgele değil)
    bir operationId eklendi (`{orderId}:submit`), böylece aynı siparişin
    gerçek bir tekrarı aynı idempotency anahtarını yeniden kullanır.
  - tests/Host/Experience/Orders/TableDraft/** (V1-RMD-106 sahipliğinde)
    — kitchen_tickets ve idempotency_keys migrasyonları fixture'a
    eklendi, ALKAROS_KITCHEN_STATION_ID test ortamı için ayarlandı,
    2 yeni test.

## In scope

1. `OrderManagementStore.SubmitOrderAsync` artık
   `SubmitOrderHandler.HandleAsync` çağırıyor (aynı sınıf, terminal-geneli
   `/submit` yolunun kullandığı) — kitchen ticket dispatch, idempotency
   ve optimistic concurrency kontrolünü bedavaya kazanıyor.
2. `AddOrderManagementExperience()`: `SubmitOrderHandler`,
   `IOrderSubmissionDispatcher` (yönlendirme olmadan, tek istasyona tek
   bilet — dispatcher'ın kendi belgelenmiş varsayılan davranışı) ve
   `AddSignalR()` kaydı eklendi.
3. `submit-draft` uç noktası artık `CustomerDisplayHub.SnapshotChanged`
   bildirimini terminal-geneli yolla birebir aynı şekilde gönderiyor.
4. `SubmitOrderRequestHash.Compute`'tan `SubmittedAt` çıkarıldı — gerçek
   bir tekrar artık temiz bir replay ile sonuçlanıyor, sahte bir
   çakışmayla değil.
5. İki istemci de artık belirleyici bir `operationId` gönderiyor.

## Out of scope

- Raporun geri kalan bulguları (Bearer token tutarsızlığı,
  Production/Purchasing'in Inventory şemasına kaçak yazması, 5 modülün
  mimariye kaydı, arayüz bulguları) — sıradaki dalgalarda.
- Kitchen.Routing'in per-item yönlendirme yeteneği (`IKitchenPrinterRouter`)
  — bilinçli olarak dışarıda bırakıldı, dispatcher'ın router'sız
  varsayılan davranışı (tek istasyon) bu düzeltmenin kapsamı için yeterli;
  ayrı bir zenginleştirme kararı.

## Dependencies

- V1-RMD-112

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 uyarı / 0 hata.
- `docker compose -f compose.yaml -f compose.test.yaml run --rm test`:
  **79/79 test projesi, sıfır başarısız** (`ALKAROS.Orders.SubmitOrder.Tests`
  16/16, 1 yeni; `ALKAROS.Host.Experience.Orders.TableDraft.Tests` 11/11,
  2 yeni — biri gerçek bir mutfak bileti oluştuğunu, diğeri aynı
  operationId ile tekrar denemenin ikinci bir bilet açmadığını kanıtlıyor).
- `npx vitest run` (tests/Clients/StaticApps): 7/7 — istemci tarafı
  değişiklik URL/yöntem tabanlı testleri bozmadı.
- Revert-and-confirm: `SubmitOrderAsync` geçici olarak eski (mutfak
  bileti oluşturmayan) davranışına döndürülüp
  `SubmitDraftMovesTheOrderToSubmittedStatus` çalıştırıldı — beklendiği
  gibi bilet sayısı 0 ile başarısız oldu; kod geri yüklenip tam süit
  yeniden yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate`,
  `validate-coverage`: sıfır hata.
- `python tools/consistency-audit/consistency_audit.py`: bu görevin
  değiştirdiği hiçbir dosyada ihlal yok.

## Handoff

- V1-GOV-103
