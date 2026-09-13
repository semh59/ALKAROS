# V1-RMD-160 - API uç noktaları bölümü, 1. parça: yetki, kapsam ve ölü alanlar

- Task ID: V1-RMD-160
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

`docs/engineering/garson-audit-2026-09-10.md`'nin API uç noktaları
bölümündeki 11 bulgudan dördünü kapatır (kalan yedisi V1-RMD-161'de):

1. **Sipariş okumada N+1** — bu zaten V1-RMD-156'nın
   `WithAvailableStockAsync` yeniden yazımıyla kapanmıştı (o görev
   Veritabanı raporunun bir bulgusunu çözerken aynı kod yolunu düzeltti);
   burada yalnız doğrulandı, yeni değişiklik yok.
2. **`GET /orders/{id}` yetki istemiyor ve terminale kapsanmamış** —
   `RequireCashierSessionAsync` (izin kontrolü yok) yerine
   `RequireCashierPermissionAsync` ile `OrdersCreate` izni gerektiriyor
   artık, `/pending`'in aynı gerekçesiyle ("kim bir siparişi çözebiliyorsa
   okuyabilir de"). "Terminale kapsanmamış" kısmı incelendi: `terminalId`
   bu store'da hiçbir yerde veri sınırı değil (yalnız oturum/idempotency
   bağlamı için kullanılıyor) — herhangi bir terminaldeki rutin personel
   zaten herhangi bir masanın siparişini görebilmeli, o yüzden eksik olan
   gerçek şey izin kontrolüydü, terminal filtresi değil.
3. **`waiterName`/`createdAt` yok sayılıyor** — `CreateTableDraftRequest`
   sözleşmesinde `WaiterName` alanı vardı ama sunucu tarafında hiçbir
   yerden okunmuyordu (grep ile doğrulandı); `Order.ServingUserId` zaten
   gerçek, kimliği doğrulanmış aktörü taşıyor — serbest metin bir alan bu
   konuda daha zayıf ve sahtelenebilir bir kopyaydı. Sözleşmeden ve her
   iki gerçek istemciden (cashier-app.js, waiter-app.js) kaldırıldı,
   bağlanmadı. `createdAt` zaten sözleşmede hiç yoktu (istemci gönderiyor,
   sunucu JSON deserialize sırasında yok sayıyordu) — istemcilerden
   kaldırıldı; sunucu zaten kendi `DateTimeOffset.UtcNow`'ını kullanıyor
   (doğru davranış, bu sistemde her yerde aynı desen).
4. **`DELETE /push/subscriptions` çağırana göre kapsanmamış** — herhangi
   bir kimliği doğrulanmış oturum, `endpoint` değerini bilerek/tahmin
   ederek BAŞKA bir cihazın aboneliğini silebiliyordu (sahiplik kontrolü
   yoktu). Yeni `PushSubscriptionStore.DeleteByEndpointForUserAsync`
   yalnız çağıranın kendi `user_id`'siyle eşleşen satırı siliyor;
   `DeleteByEndpointAsync` olduğu gibi kaldı çünkü `WebPushSender`'ın
   kendi RFC 8030 §7.3 temizliği (bir push servisi kalıcı hata verince)
   hiçbir kullanıcı bağlamına sahip değil ve kime ait olduğuna
   bakmaksızın ölü aboneliği temizlemesi gerekiyor.

## Owned surface

- `plan/v1/remediation/V1-RMD-160-api-endpoints-remainder-part1.md` (yeni)
- Sınırlı ek — aşağıdaki tüm yollar ilgili görevin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası
  olarak parse etmesin):
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs (V1-ORD-005
    sahipliğinde) — GET /{orderId} artık RequireCashierPermissionAsync +
    OrdersCreate kullanıyor.
  - src/Host/Experience/Orders/OrderManagementContracts.cs (V1-RMD-146/147
    sahipliğinde) — CreateTableDraftRequest'ten WaiterName kaldırıldı.
  - src/Clients/Cashier/wwwroot/cashier-app.js (V1-CSH-00x sahipliğinde) —
    waiterName kaldırıldı.
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js (V1-WTR-00x sahipliğinde)
    — waiterName ve createdAt kaldırıldı.
  - tests/Host/Experience/Orders/TableDraft/CheckLifecycleHttpTests.cs ve
    tests/Host/Experience/Orders/TableDraft/OrderManagementTableDraftHttpTests.cs
    (V1-ORD-006 sahipliğinde) — çağrıların üçüncü konumsal argümanı
    (WaiterName) mekanik olarak kaldırıldı, davranış değişmedi.
  - src/Host/Experience/WebPush/WebPushExperience.cs ve
    src/Host/Experience/WebPush/PushSubscriptionStore.cs (V1-WTR-011
    sahipliğinde) — yeni DeleteByEndpointForUserAsync eklendi, HTTP uç
    noktası buna geçti.
  - tests/Host/Experience/WebPush/WebPushHttpTests.cs (V1-WTR-011
    sahipliğinde) — yeni regresyon testi eklendi
    (DeletingAnotherUsersSubscriptionDoesNothing).

## Out of scope

Bu görev, API uç noktaları bölümünün kalan yedi bulgusunu kapsamaz (ayrı
görev, V1-RMD-161):

- Eklenti seçim kurallarının sunucuda zorlanması.
- Miktar/kalem sayısı/not uzunluğu sınırları.
- `PostgresException` → 503 eşlemesinin genişletilmesi.
- Masa yönetimi/push uç noktalarında hız sınırı; `/pending`'in kovası.
- Katalog sayfalama imleci.
- `X-Idempotency-Key` başlığının okunması.
- `/comp` ve `/transfer-server`'ın istemcisi.

## Dependencies

- V1-RMD-159

## Acceptance evidence

- `dotnet build src/Host/ALKAROS.Host.csproj -c Debug` → 0 uyarı, 0 hata.
- `dotnet build tests/Host/Experience/Orders/TableDraft/ALKAROS.Host.Experience.Orders.TableDraft.Tests.csproj -c Debug`
  → 0 uyarı, 0 hata (WaiterName kaldırıldıktan sonra 40 çağrı yeri hâlâ
  derleniyor).
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres dotnet test`
  gerçek test Postgres'ine karşı:
  - `tests/Host/Experience/Orders/TableDraft` — 42/42 yeşil.
  - `tests/Host/Experience/Orders/{Comp,Confirmation,Void,VoidSent}` —
    toplam 46/46 yeşil (GET /{orderId} izin değişikliğiyle etkilenen
    yollar dahil).
  - `tests/Host/Experience/WebPush` — 19/19 yeşil, yeni
    `DeletingAnotherUsersSubscriptionDoesNothing` testi dahil.
- Yeni WebPush testinin **vacuous olmadığı kanıtlandı**: `git stash` ile
  `PushSubscriptionStore.cs`/`WebPushExperience.cs` değişiklikleri geri
  alınıp yalnız o test çalıştırıldı → gerçekten düştü (`Expected: 1,
  Actual: 0` — başka kullanıcının aboneliği silinmişti). Değişiklikler
  geri yüklendi, tekrar 19/19 yeşil.
- `node --check` her iki JS dosyası için temiz.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. (Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.)

## Handoff

- None
