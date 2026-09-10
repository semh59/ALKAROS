# V1-RMD-144 - Garson/kasa siparişi hiç stok düşürmüyor

- Task ID: V1-RMD-144
- Status: Done
- Assignee: Claude Opus 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in kararı (2026-09-10): "Eğer siparişi kendi alıyorsa stok düşmeli, qr
ile girildiyse onayı sonrası düşmeli, nfc direk düşmeli." V1-RMD-143 stok
tüketimini `Accepted` durumuna bağladı, ama `Accepted`'a yalnız QR ve NFC
siparişleri ulaşıyor: `OrderState.Accepted` yalnız `PendingConfirmation`'dan
geçilebiliyor ve oraya yalnız `QrOrderSubmittedConsumer` ile
`NfcOrderingStore` giriyor. Garson/kasa siparişi `submit-draft` sonrası
`Submitted`'da kalıyor, bu yüzden bugün hiç stok düşmüyor. Bu görev stok
tüketimini kanalın kendi "sipariş kesinleşti" anına bağlar: Cashier/Waiter
için gönderim (Submit), QR/NFC için Accept (değişmiyor).

## Owned surface

- `plan/v1/remediation/V1-RMD-144-channel-scoped-stock-consumption.md` (yeni)
- `src/Host/Experience/Orders/SubmissionStockConsumption/**` (yeni) —
  `OrderSubmissionStockDispatcher` (`IOrderSubmissionDispatcher`
  implementasyonu) ve `CompositeOrderSubmissionDispatcher`.
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır (yollar
  geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası olarak
  parse etmesin):
  - tests/Host/Experience/Orders/TableDraft/** (V1-RMD-113/123 sahipliğinde)
    — garson gönderim hattının kendi test yeri; bu görevin senaryoları
    oraya eklenir ve gönderim artık stok tükettiği için mevcut üç
    submit-draft testinin kurulumu gerçek ürün-stok eşlemesi taşıyacak
    şekilde güncellenir. Ayrı bir test projesi açmak yalnız aynı
    migration/fixture listesini ikinci kez kurardı.
  - src/Host/Experience/Orders/OrderStockConsumption/OrderStockConsumptionService.cs
    (V1-RMD-143 sahipliğinde) — yalnız belirli kalemleri tüketen bir aşırı
    yükleme eklenir; mevcut `ConsumeForAcceptedOrderAsync` davranışı
    değişmez.
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs,
    src/Host/DualScreen/DualScreenApplication.cs (ilgili görevlerin
    sahipliğinde) — `IOrderSubmissionDispatcher` kaydı composite üzerinden
    yapılır; Kitchen'ın kendi dispatcher'ı değişmeden kalır.
    `NfcOrderingEndpoints.cs`'e dokunulmadı: NFC kompozisyonundan geçen
    siparişin `Source`'u hiçbir zaman Cashier/Waiter olmadığı için orada
    composite'e gerek yok.

## In scope

1. **Kanal ayrımlı tüketim.** `OrderSubmissionStockDispatcher`, submit
   transaction'ının içinde (`IOrderSubmissionDispatcher.DispatchAsync`,
   `SaveAsync`'ten sonra, commit'ten önce) çalışır ve yalnız
   `OrderSource.Cashier` ile `OrderSource.Waiter` siparişlerinde stok
   tüketir. `Qr`, `Nfc` ve `Online` için hiçbir şey yapmaz — onlar
   `PendingOrderConfirmationStore.AcceptAsync` /
   `NfcOrderingStore.TryConsumeStockAndAcceptAsync` üzerinden tüketmeye
   devam eder. Aynı `SubmitOrderHandler`'dan geçen dört kanalın çifte
   tüketmemesini sağlayan tek koşul budur.
2. **Aynı kalemi iki kez tüketmeme.** Uygulama sırasında doğrulandı: bu
   zaten üç bağımsız mekanizmayla garanti altında, dördüncü bir koruma
   ulaşılamaz kod olurdu. (a) `Order.Submit()` yalnız `Draft` bir siparişten
   çağrılabiliyor, yani aynı sipariş iki kez gönderilemiyor.
   (b) `OrderManagementStore.GetActiveOrderByTableIdInternalAsync` yalnız
   `status = 'Draft'` eşliyor, bu yüzden gönderilmiş bir siparişi olan masaya
   eklenen ikinci tur eski kalemleri hiç taşımayan YENİ bir sipariş açıyor.
   (c) `SubmitOrderHandler` idempotent replay'de kayıtlı yanıtı döndürüyor ve
   dispatcher'ı hiç çağırmıyor. Dispatcher bu yüzden yalnız `IsActive`
   kalemleri tüketiyor (iptal edilmiş kalem mutfağa gitmediği için stok da
   tüketmez, Accept yolundaki gerekçenin aynısı) ve ayrı bir "zaten
   tüketildi mi" sorgusu taşımıyor. (b) maddesi
   `ASecondRoundOfItemsOnlyConsumesTheNewLine` testiyle sabitlendi.
3. **Yetersiz stok veya eşlenmemiş ürün gönderimi tamamen reddeder.**
   V1-RMD-143'ün Accept yolundaki davranışının aynısı: tek transaction,
   kısmi düşüm yok, sipariş `Draft`'ta kalır, garson Türkçe hata görür.
   `OrderManagementExceptionFilter`'ın mevcut 409 haritalaması
   (`INSUFFICIENT_STOCK`, `PRODUCT_STOCK_NOT_CONFIGURED`) yeniden kullanılır.
   Semih'in kararı (2026-09-10, uygulama sırasında açıkça soruldu):
   eşlemesi olmayan ürün garson siparişini de reddeder — Accept yoluyla
   birebir aynı katılık, "eşleme yok = stok takibi yok" gibi yumuşak bir
   yorum yok. Pratik sonucu kabul edildi: bir ürün satılabilir olmadan
   önce yöneticinin onu bir stok kalemine eşlemesi gerekir.
4. **Composite dispatcher.** `SubmitOrderHandler` tek bir
   `IOrderSubmissionDispatcher?` alıyor ve bugün oraya Kitchen'ın kendi
   dispatcher'ı kayıtlı. `CompositeOrderSubmissionDispatcher` ikisini sırayla
   aynı transaction'da çağırır (önce stok, sonra mutfak bileti: stok
   yetersizse mutfağa hiç bilet düşmez).

## Out of scope

- `OrderItemModifier`'ların stok tüketmemesi — V1-RMD-143'ün kendi "Out of
  scope" kaydıyla aynı gerekçe, Semih'in ayrı kararını bekliyor.
- `PortionReservationLifecycleService`'in `on_hand_quantity`'e hiç
  dokunmaması (V11-RSV-001/002/003, hâlâ sıfır çağıranı var).
- Gönderilmiş bir siparişin `Accepted`'a taşınması veya `OrderState` durum
  makinesinin değiştirilmesi — bu görev durum geçişlerine hiç dokunmaz,
  yalnız tüketimin tetiklendiği anı kanala göre ayırır.
- `OrderManagementStore.CreateOrUpdateTableDraftAsync`'in `orders_order_number_key`
  yarış koşulu (V1-RMD-143 §4'te raporlandı, hâlâ açık, ayrı Task ID gerekir).

## Dependencies

- V1-RMD-143

## Acceptance evidence

- `dotnet build ALKAROS.slnx`: 0 Uyarı, 0 Hata.
- Gerçek Postgres'e karşı (`alkaros-test-pg`, port 55432), ayrı ayrı, gerçek
  çıkış koduyla:
  - `ALKAROS.Host.Experience.Orders.TableDraft.Tests`: **19/19** (14'ten),
    3 ardışık çalıştırmada kararlı. Beş yeni senaryo: garson gönderimi
    stoğu 5'ten 3'e gerçekten düşürüyor ve kalem kimliğine bağlı bir
    `Consumption` hareketi yazıyor; ikinci tur yalnız yeni kalemi tüketiyor
    (10 → 8 → 7, ilk turu tekrar düşürmüyor); aynı `OperationId` ile
    yinelenen gönderim ikinci kez tüketmiyor; kalan stoktan fazlası
    istendiğinde gönderim 409 ile tamamen reddediliyor, bakiye değişmiyor
    ve mutfak bileti hiç yazılmıyor; eşlenmemiş üründe aynı ret. Mevcut üç
    submit-draft testinin kurulumu gerçek ürün-stok eşlemesi taşıyacak
    şekilde güncellendi (eşlemesiz ürün artık gönderimi reddettiği için
    kırmızıya dönmüşlerdi — bu, kararın gerçekten uygulandığının kanıtı).
  - `ALKAROS.Host.Experience.NfcOrdering.Tests`: 17/17 — NFC aynı
    `SubmitOrderHandler`'dan geçtiği hâlde çifte tüketmiyor.
  - `ALKAROS.Host.Experience.Orders.Confirmation.Tests`: 17/17 — QR/Accept
    yolu değişmedi.
  - `ALKAROS.Host.Experience.Orders.VoidSent.Tests`: 12/12,
    `...Comp.Tests`: 9/9, `...Void.Tests`: 5/5 — Accept sonrası stok iadesi
    ve iptal/ikram yolları etkilenmedi.
  - `ALKAROS.Host.Tests` (Manifest + Reachability + Composition): 133/133 —
    composite dispatcher kaydı her modülü çözmeye devam ediyor.
- Migration yok.
- Uygulama sırasında bir tasarım varsayımı çürütüldü ve kod küçültüldü:
  ilk taslakta dispatcher kalem başına "bu zaten tüketildi mi" diye
  `IStockMovementRepository.GetBySourceAsync` sorgusu yapıyordu. Deneyle
  (koruma geçici olarak devre dışı bırakılıp
  `ASecondRoundOfItemsOnlyConsumesTheNewLine` yine yeşil kalarak) bu yolun
  hiç tetiklenmediği görüldü — In scope §2'deki üç mekanizma zaten yeterli.
  AGENTS.md'nin ulaşılamaz kod yasağı gereği sorgu ve onunla birlikte gelen
  `IStockMovementRepository` bağımlılığı kaldırıldı.
- Semih'in elle deneyebileceği senaryo: bir ürünü stok kalemine eşle ve
  bakiyesini 5 yap; garson PWA'dan o üründen 2 adet sipariş verip mutfağa
  gönder; `GET /api/v1/management/inventory/...` ile bakiyenin 3'e düştüğünü
  gör; aynı masaya 1 adet daha ekleyip gönder, bakiyenin 2'ye düştüğünü (eski
  kalemlerin tekrar düşmediğini) gör; kalan 2'den fazlasını istemeye çalış ve
  siparişin Türkçe "yeterli stok yok" hatasıyla reddedildiğini, stoğun 2'de
  değişmeden kaldığını gör.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 1 önceden var olan
  ihlal (`InventoryAdjustmentService.cs:96`, V1-RMD-143'ün de aynı şekilde
  raporladığı, bu görevden bağımsız), yeni ihlal yok.

## Handoff

- None
