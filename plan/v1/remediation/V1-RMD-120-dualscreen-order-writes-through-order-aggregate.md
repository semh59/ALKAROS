# V1-RMD-120 - DualScreen order writes routed through the Order aggregate

- Task ID: V1-RMD-120
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: implementation
- Surface state: Existing

## Goal

Semih onayıyla ("Sırada Dalga 3 (Boundary) var. Devam et." → taze bir sınır
denetimi → "Tam düzelt"), bağımsız bir sınır (module boundary) denetiminde
bulunan gerçek bulguyu kapatır: `src/Host/DualScreen/DualScreenStore.Orders.cs`
(Cashier/PosTerminal kanalı) `orders.orders`/`orders.order_items`'a
`Order`/`OrderItem` aggregate'ini ve `IOrderRepository`'yi hiç kullanmadan,
tamamen kendi ham SQL'iyle yazıyordu — sipariş oluşturma, kalem ekleme,
miktar değiştirme, kalem silme ve net/vergi/brüt hesaplaması aynı bounded
context (Orders) içinde ikinci kez, bağımsız olarak elle yeniden yazılmıştı
(Waiter/masa kanalının `OrderManagementStore.cs`'si zaten aggregate'i
kullanıyordu). Bu, `OrderMath`/`Order`/`OrderItem`'da yapılacak gelecekteki
bir domain kuralı değişikliğinin (vergi, yuvarlama, durum koruması) bu kanala
hiç yansımamasına açık bırakıyordu.

## Owned surface

- `plan/v1/remediation/V1-RMD-120-dualscreen-order-writes-through-order-aggregate.md` (yeni)
- `src/Host/DualScreen/DualScreenStore.cs` (V1-RMD-090'dan devralındı) —
  kendi `NpgsqlDataSource`'undan kurduğu bir `PostgresOrderRepository` alanı
  (DI enjeksiyonu değil — bkz. Acceptance evidence'taki gerekçe: beş ayrı
  Experience alanı bu store'u yalnız oturum kimlik doğrulaması için kaydediyor,
  hiçbiri Orders modülünü kaydetmiyor); `MutateExistingItemAsync` artık
  `Order`/`OrderItem`/repository üzerinden yazıyor; artık kullanılmayan
  `RecalculateOrderAsync`, `BindAmounts`, `RoundCurrency` kaldırıldı.
- `src/Host/DualScreen/DualScreenStore.Orders.cs` (V1-RMD-097'den devralındı)
  — `StartOrderAsync`'in yeni-sipariş dalı ve `AddItemAsync` artık
  `Order`/`OrderItem`/`IOrderRepository` üzerinden yazıyor.
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası
  olarak parse etmesin):
  - src/Modules/Orders/OrderAggregate/OrderItem.cs (V1-RMD-064 sahipliğinde)
    — yeni `ChangeQuantity(decimal)` metodu.
  - src/Modules/Orders/OrderAggregate/Order.cs, IOrderRepository.cs,
    PostgresOrderRepository.cs (V1-ORD-001 sahipliğinde) — `Order`a yeni
    `ChangeItemQuantity(Guid, decimal)`/`RemoveItem(Guid)`; `IOrderRepository`/
    `PostgresOrderRepository`a `AddAsync`'in var olan `SaveAsync` deseniyle
    aynı connection/transaction-alan overload'u (Host'un kendi transaction'ı
    içinde atomik ekleme için).
  - tests/Modules/Orders/OrderAggregate/OrderDomainTests.cs,
    PostgresOrderTests.cs (ilgili görevler sahipliğinde) — yeni metodların ve
    overload'un regresyon testleri.
  - tests/Host/MigrationComposition/DualScreen/DualScreenStoreTests.cs
    (V1-RMD-090 sahipliğinde) — `DualScreenStore` kurucusuna
    `PostgresOrderRepository` argümanı eklendi (davranış testleri
    değişmedi — aynı senaryoları aynı sonuçlarla doğruluyorlar).

## In scope

1. **`OrderItem.ChangeQuantity`/`Order.ChangeItemQuantity`/`Order.RemoveItem`
   (yeni domain yetenekleri).** Aggregate'in daha önce hiç sahip olmadığı
   "bir Draft kalemin miktarını değiştir" ve "bir Draft kalemi tamamen kaldır"
   işlemleri — DualScreen'in sepet ekranının ihtiyaç duyduğu, `AddItem`/
   `CancelItem`'den ayrı iki işlem (`CancelItem` Active bir kalemi *voider*,
   geçmişte tutar; bunlar Draft bir kalemi sepetten düzenler/kaldırır).
   İkisi de yalnız Draft sipariş + Draft kalem üzerinde çalışır, aksi halde
   `InvalidOperationException`/`ArgumentException` fırlatır.
2. **`IOrderRepository.AddAsync`'in connection/transaction-alan overload'u.**
   `SaveAsync`'in zaten sahip olduğu desenin aynısı — Host'un kendi açık
   transaction'ı (terminal/masa pointer bağlamalarıyla aynı commit) içinde
   yeni sipariş grafiğini eklemek için.
3. **`DualScreenStore.Orders.cs`'in üç yazma yolunun aggregate'e taşınması.**
   `StartOrderAsync`'in yeni-sipariş INSERT'i → `new Order(...)` +
   `_orderRepository.AddAsync(order, connection, transaction, ct)`.
   `AddItemAsync`'in FOR UPDATE + el ile net/tax/gross hesaplayan UPDATE/
   INSERT'i → `_orderRepository.GetByIdAsync` + `Order.AddItem`/
   `ChangeItemQuantity` + `_orderRepository.SaveAsync(..., connection,
   transaction, ct)` (item-satırı FOR UPDATE'i gereksizdi: `LockDraftOrderAsync`
   zaten aynı siparişin her mutasyonunu serileştiriyor — iki concurrency testi
   [`SequentialAddsCommit999...`, `ConcurrentAddsWithSameRevision...`] bunu
   doğruluyor). `MutateExistingItemAsync`'in DELETE/UPDATE + ayrı
   `RecalculateOrderAsync`'i → `Order.RemoveItem`/`ChangeItemQuantity` +
   `SaveAsync` (aggregate'in kendi `Subtotal`/`TaxTotal`/`Total` property'leri
   artık toplamı hesaplıyor, ayrı bir SQL SUM'a gerek kalmadı).
4. **Ölü kod temizliği.** `RecalculateOrderAsync`, `BindAmounts`,
   `RoundCurrency` artık hiçbir yerden çağrılmıyor; kaldırıldı.

## Out of scope

- **Diğer Host/Experience "Deneyim" store'larının (`TableManagementStore`,
  `CatalogManagementStore`, `BillingSplitStore`'un `table_mgmt.tables`
  pointer güncellemeleri dahil) kendi ham SQL desenleri.** Bunlar zaten
  `PostgresTablePointerProjector` ile mutabakatı sağlanan, kasıtlı bir
  "soft cache" deseni (V1-RMD-078/V1-TBL-007) — bu görevin bulduğu asıl
  defekt (bir bounded context'in domain mantığının tamamen ikinci kez
  yazılması) değil.
- **`tools/consistency-audit` rule 5'in `src/Host/**`'i taramaması.** Gerçek
  bir kör nokta ama Host katmanının meşru (Experience store'ların kendi
  bounded context'ine ham SQL yazması) ve gayrı meşru (bu görevin bulduğu)
  desenlerini ayıracak bir istisna modeli gerektiriyor — ayrı bir karar/dalga.
- **`identity.device_sessions`'a üç ayrı Host dosyasında (`RoleManagementEndpoints`,
  `AuthorizationDecisionEndpoints`, `CatalogManagementEndpoints`) birebir
  aynı SQL ile yazılması.** Gerçek bir kod tekrarı ama bir sınır ihlali değil
  (üçü de kendi Experience katmanının oturum doğrulaması) — ayrı bir
  sadeleştirme kararı.
- **Diğer denetim adayları** (`Program.cs`'deki ham SQL'ler, DualScreenStore'un
  masa/terminal bağlama akışının geri kalanı) — DualScreen'in Orders yazma
  yoluyla aynı sınıf/aynı dosya değişikliği olmayan, ayrı kararlar.

## Dependencies

- V1-RMD-119

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 uyarı / 0 hata.
- `docker compose -f compose.yaml -f compose.test.yaml up --build test`:
  `docker inspect alkaros-test-1 --format '{{.State.ExitCode}}'` ile gerçek
  container çıkış kodu 0 doğrulandı (piped/arka plan sarmalayıcının kendi
  exit code'una güvenilmedi). 80 test projesi, 1743 test, sıfır başarısız —
  `ALKAROS.Orders.OrderAggregate.Tests` 115/115 (yeni
  `OrderItemQuantityAndRemovalTests` ve `ConnectionScopedAddAsync...` dahil),
  `ALKAROS.Host.Tests` 121/121 (`DualScreenStoreTests` içindeki iki
  concurrency testi — `SequentialAddsCommit999...`,
  `ConcurrentAddsWithSameRevision...` — davranış değişmeden geçti),
  `ALKAROS.Host.Experience.Tables.Tests` 9/9,
  `ALKAROS.Host.Experience.OfflineReconciliation.Tests` 5/5.
  **İlk koşuda gerçek bir regresyon container'ın kendi exit code'uyla
  yakalandı** (7/9 ve 4/5 başarısız, 500 Internal Server Error): `DualScreenStore`'un
  yeni `IOrderRepository` bağımlılığı, onu yalnız oturum kimlik doğrulaması
  için kaydeden ve Orders modülünü hiç kaydetmeyen dört dar Experience test
  host'unda (Tables, OfflineReconciliation, ayrıca üretimde Billing/Kitchen'ın
  aynı deseni) çözülemiyordu. Düzeltme: `DualScreenStore` `IOrderRepository`'yi
  DI'dan almak yerine zaten sahip olduğu `NpgsqlDataSource`'tan kendi kuruyor
  (alan tipi concrete `PostgresOrderRepository` — CA1859 analyzer uyarısı da
  bunu istedi, DI kaydı hiçbir yerde gerekmiyor); ikinci koşuda tüm paket
  yeşil (`docker inspect` exit code 0).
- `python tools/consistency-audit/consistency_audit.py`: 13 ihlal, hepsi bu
  görevden önce de vardı, dokunulmayan dosyalarda.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata (bir
  `SURFACE_DUPLICATE` bulunup düzeltildi — `V1-RMD-090`/`V1-RMD-097`'nin
  `DualScreenStore.cs`/`DualScreenStore.Orders.cs` üzerindeki backtick'li
  eski iddiaları bu göreve devir cümlesine çevrildi).

## Handoff

- V1-GOV-117
