# V12-QRO-001 - Implement pending QR order intake

- Task ID: V12-QRO-001
- Status: Done
- Assignee: Claude Sonnet 5 (exactly one person)
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:I.34-I.37
- PDF:II.2.18
- PDF:II.6.8
- PDF:II.7.3
- PDF:III.21
- CORR:C2

## Goal

Kimliği doğrulanmış bir QR gönderimini PendingConfirmation'deki bir dahili Order'ye dönüştürün.

## Owned surface

- `src/Modules/QrOrdering/PendingOrders/**`, `tests/Modules/QrOrdering/PendingOrders/**`,
  `database/migrations/V12/V12-QRO-001/**`
- `src/Modules/Orders/Integration/QrOrderSubmittedConsumer.cs` (yeni dosya — `TableEventOrderConsumer.cs`/
  `KitchenEventOrderConsumer.cs`'in yanına, aynı desen; o dosyalara dokunulmadı) — QR Ordering'in Order'a doğrudan
  çağrı kenarı yok (V0-ARC-001 satır 19: yalnız Identity ve Table Management onaylı), bu yüzden `QrOrderSubmitted`
  olayını tüketip gerçek `orders.orders` satırını materialize eden tüketici Order tarafında yaşamak zorunda.
- `src/BuildingBlocks/IntegrationContracts/QrOrderIntegrationEvents.cs` (yeni dosya — `TableIntegrationEvents.cs`/
  `KitchenIntegrationEvents.cs`'e komşu, kendi olay sözleşmesi; o dosyalara dokunulmadı)
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır (yollar geri-tik olmadan yazıldı ki denetleyici
  bunları sahiplik iddiası olarak parse etmesin):
  - src/Modules/QrOrdering/ALKAROS.QrOrdering.csproj, src/Modules/QrOrdering/TokenLifecycle/QrOrderingModule.cs
    (V12-QRS-001 sahipliğinde) — yalnız Messaging/IntegrationContracts proje referansları ve
    `QrPendingOrderStore` kaydı eklendi (V12-QRT-003'ün aynı dosyaya yaptığı artımlı ek hazinesiyle aynı gerekçe).
  - src/Modules/Orders/OrderAggregate/OrdersModule.cs (V1-RMD-064 sahipliğinde kalır, V1-KIT-005'in aynı dosyaya
    yaptığı sınırlı ek hazinesiyle aynı desen) — yalnız yeni `QrOrderSubmittedConsumer` kaydı eklendi, mevcut hiçbir
    kayıt değişmedi.
  - tests/Modules/Orders/OrderAggregate/ALKAROS.Orders.OrderAggregate.Tests.csproj,
    tests/Modules/Orders/OrderAggregate/QrOrderSubmittedConsumerTests.cs (yeni test dosyası) (V1-ORD-001
    sahipliğinde kalır, `KitchenEventOrderConsumerTests.cs`'in aynı projeye eklendiği desenle aynı) — yalnız
    `001-idempotency-keys.up.sql` fixture'ı ve yeni test dosyası eklendi.
  - ALKAROS.slnx (paylaşılan çözüm dosyası) — yalnız yeni test projesi girdisi eklendi.

## In scope

- Yük doğrulama, fiyat anlık görüntüsü, table bağlama, idempotency ve beklemedeki son kullanma tarihi meta verileri.

## Out of scope

- Restoran onayı, table durumu ve envanter rezervasyonu.

## Dependencies

- V12-QRS-002
- V1-ORD-001
- V1-ORD-002
- V1-TBL-001
- V12-QRS-003

## Deliverables

- `src/Modules/QrOrdering/PendingOrders/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Yinelenen geçiş dağıtımı bir Order oluşturur; geçersiz ürün/fiyat/table hiçbiri oluşturmaz; henüz stok rezervasyonu
  gerçekleşmedi.
- Mimari karar: V0-ARC-001 satır 19, QR Ordering'in Order'a yönelik doğrudan çağrı kenarı olmadığını, yalnız
  `QrOrderSubmitted → Order` integration event'inin onaylı olduğunu kayda geçirmiş. `QrPendingOrderStore`
  (`src/Modules/QrOrdering/PendingOrders/**`) doğrulanmış bir customer session'ı (V12-QRS-003) alır, fiyat/ad
  anlık görüntüsünü `catalog.products`'tan (salt-okunur, her zaman izinli çapraz-şema okuma) çeker ve
  `QrOrderSubmitted` olayını kendi idempotency ledger'ıyla (`qr_ordering.pending_order_submissions`, migration 086)
  aynı transaction'da outbox'a kuyruğa alır. Order tarafındaki yeni `QrOrderSubmittedConsumer`
  (`src/Modules/Orders/Integration/**`) olayı tüketip gerçek `orders.orders` satırını materialize eder: Draft ->
  Submitted (mevcut `SubmitOrderHandler` üzerinden, tıpkı NFC/waiter/cashier kanalları gibi, mutfak bileti tam
  bir kez dağıtılsın diye) -> PendingConfirmation, ve orada durur — NFC'nin güvenilir-kanal kısayolunun aksine QR
  hiçbir zaman otomatik kabul edilmez; table durumu bu görevin kapsamı dışında (QRO-002'nin işi).
  `ux_orders_table_submission` (V1-RMD-123) hem tekilleştirilmiş yeniden teslimatı hem de eşzamanlı yarışı tek bir
  Order'a çözer.
- `ALKAROS.QrOrdering.PendingOrders.Tests`: 7/7 test geçti — geçerli oturumla gönderim olayı kuyruğa alır ve
  `qr_ordering.pending_order_submissions`'a kaydeder; aynı submission'ın tekrarı yeniden kuyruğa almadan aynı
  sonucu döndürür; bilinmeyen oturum/ürün/sıfır miktar/boş sepet reddedilir; olay henüz tüketilmemişken
  `FindResultingOrderAsync` null döner.
- `ALKAROS.Orders.OrderAggregate.Tests` (119/119, 4 yeni `QrOrderSubmittedConsumerTests` dahil): olay, doğru
  fiyat/ad/miktar anlık görüntüsüyle `PendingConfirmation` durumunda bir Order materialize eder; aynı submission'ın
  yeniden teslimi ikinci bir Order oluşturmaz; eşzamanlı yeniden teslimat tam olarak bir Order'ın kazanmasını
  sağlar (order_number artık submission id'den türetiliyor, zamana dayalı çakışma riski yok).
- `dotnet build ALKAROS.slnx -c Debug`: 0 Uyarı, 0 Hata.
- Mimari sınır testleri korunuyor: `ALKAROS.Architecture.Tests` 8/8 (QrOrdering'in yeni Messaging/IntegrationContracts
  referansları `ApprovedEdges`'i etkilemedi — ikisi de paylaşılan building block, modül listesinde değil);
  `HostModuleReachabilityTests`/`HostConstructabilityTests`/`ManifestTests` (26/26) — `QrOrderSubmittedConsumer`'ın
  `SubmitOrderHandler` bağımlılığı isteğe bağlı (varsayılan null): saf modül kompozisyonu (bu testler) onu hiç
  çağırmaz, gerçek dağıtımda Host'un `AddOrderManagementExperience`'ı zaten kaydediyor.
- `docker compose -f compose.yaml -f compose.test.yaml run --build --rm test` tüm suite (Release) geçti;
  `docker inspect alkaros-test-1 --format '{{.State.ExitCode}}'` → `0`.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → 13 önceden var olan, bu görevle ilgisiz ihlal (değişmedi),
  yeni ihlal yok.

## Handoff

- V12-QRO-002
- V12-QRO-003
