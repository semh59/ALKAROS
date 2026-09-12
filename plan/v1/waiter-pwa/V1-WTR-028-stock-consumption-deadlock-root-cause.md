# V1-WTR-028 - Eşzamanlı stok düşümü deadlock'unun kök nedeni

- Task ID: V1-WTR-028
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in talimatıyla ("Biz şimdiden tüm en küçük hatalar bile kök
nedeniyle çöz", 2026-09-12): V1-WTR-027'nin retry'ı deadlock'un
SEMPTOMUNU kapatmıştı, KÖK NEDENİNİ değil — bu görev kök nedeni gerçekten
kapatıyor.

**İlk yanlış hipotez (bu görev sırasında elendi):** Önce
`PostgresStockBalanceRepository`'ye satır bazlı bir
`pg_advisory_xact_lock` eklendi (aynı `(stock_item_id, stock_location_id)`
satırına yazan eşzamanlı yazarları serileştirsin diye). Bu YETMEDİ — yük
testi retry kapatılıp (`maxAttempts = 1`) 5 kez çalıştırıldığında 3/5
koşu yine başarısız oldu; bu sefer deadlock `INSERT`'in içinde değil, YENİ
eklenen kilit alma çağrısının kendi içinde oluşuyordu (Host loglarında
doğrulandı).

**Gerçek kök neden:** `OrderStockConsumptionService.ConsumeItemsAsync`,
bir siparişin her kalemini (ve her kalemin modifiyerlerini) TEK TEK,
`order.Items`'ın kendi `created_at` sırasıyla dolaşıp, o an rastladığı
stok kalemini kilitliyordu. Bu sıra siparişten siparişe FARKLI — iki
eşzamanlı sipariş aynı iki stok kalemine (ör. bir ürün + "ekstra peynir"
modifiyeri) dokunuyor ama onlara ZIT sırada ulaşıyorsa (A siparişi
X'i kilitleyip Y'yi beklerken, B siparişi Y'yi kilitleyip X'i bekliyor),
klasik bir AB-BA kilit sırası deadlock'u oluşur — advisory lock'lar da
satır kilitleri gibi Postgres'in kendi deadlock tespitine katılıyor,
tek satırlık kilit bunu hiç engellemiyor.

**Çözüm:** `ConsumeItemsAsync`, artık gerçek tüketim döngüsünden ÖNCE, bu
çağrının dokunacağı TÜM `(stok kalemi, konum)` çiftlerini (ürünler +
modifiyerler) önceden topluyor (`CollectRequiredLocksAsync`), sabit ve
her işlemde AYNI global sırayla (stok kalemi id'si, sonra konum id'si)
sıralayıp hepsini baştan kilitliyor — hangi sırayla dolaşıldığından
bağımsız. `IStockBalanceRepository.AcquireOnHandLockAsync` bu amaçla yeni
eklendi (var olan private kilit yardımcısının kamuya açık hali);
`ApplyOnHandDeltaAsync`/`TryApplyGuardedOnHandDeltaAsync`'in kendi
kilitleri aynı transaction içinde aynı anahtarı tekrar almaya devam
ediyor — bu zararsız (pg_advisory_xact_lock aynı sahip için yeniden
girişli, transaction bitince otomatik serbest kalıyor).

V1-WTR-027'nin retry'ı KALDIRILMADI — artık gerçek kök nedenden değil,
başka geçici serileştirme hatalarına karşı savunma derinliği olarak
duruyor; `maxAttempts` doğrulama için geçici olarak 1'e indirilip
tekrar 5'e geri alındı.

## Owned surface

- Sınırlı ek:
  - src/Host/Experience/Orders/OrderStockConsumption/OrderStockConsumptionService.cs
    (Host sahipliğinde) — `ConsumeItemsAsync`'e ön-kilitleme adımı,
    yeni `CollectRequiredLocksAsync`.
  - src/Modules/Inventory/BalanceProjection/IStockBalanceRepository.cs,
    PostgresStockBalanceRepository.cs (Inventory sahipliğinde) — yeni
    `AcquireOnHandLockAsync` (var olan private kilit yardımcısının kamuya
    açık hali).
  - src/Modules/Orders/SubmitOrder/SubmitOrderHandler.cs (Orders
    sahipliğinde) — V1-WTR-027'nin retry yorumunun düzeltilmesi (kök
    neden artık burada değil, kilit sırasındaydı); `maxAttempts` 5'te
    sabit kaldı.
  - tests/Modules/Inventory/BalanceProjection/StockBalanceDomainTests.cs,
    ManualAdjustments/ManualAdjustmentDomainTests.cs,
    MovementReversal/StockMovementReversalDomainTests.cs,
    PortionReservations/CancellationEffects/PortionReservationCancellationEffectsDomainTests.cs,
    WasteRecording/WasteRecordingDomainTests.cs (ilgili test sahipliğinde)
    — her birinin kendi `FakeStockBalanceRepository`'sine arayüzün yeni
    üyesinin no-op uygulaması eklendi (arayüz genişlemesi, davranış
    değişikliği değil).

## Out of scope

- `OrderStockConsumptionService.CollectRequiredLocksAsync`'in kendisi bir
  eksik eşleme/stok kalemi bulursa sessizce atlıyor — doğru tipte
  istisnayı (ör. `ProductStockNotConfiguredException`) fırlatmak gerçek
  tüketim döngüsünün işi olarak bırakıldı, iki geçişin "geçersiz" tanımı
  hiç ayrışmasın diye. Bu bilinçli bir tasarım, eksik değil.
- Kilitleme artık siparişin TÜM kalemlerini önceden topluyor — bu, çok
  sayıda farklı stok kalemine sahip anormal derecede büyük bir siparişte
  (bugün karşılığı olmayan bir senaryo) ek bir ön-tarama sorgu turu
  anlamına gelir; ölçüm yapılmadı, gerçek bir performans sorunuysa ayrı
  bir görev olur.
- E2E paketinin TAMAMI arka arkaya koşulduğunda (yalnız 05 değil) yük
  testinin login/masa-ızgarası adımı bazen 15 saniyelik istemci zaman
  aşımını aşıyor — sunucu tarafında hiçbir hata yok (Host logları temiz),
  makine/CPU rekabeti (aynı Node sürecinde art arda birçok Chromium
  bağlamı). Yalnız 05 tek başına koşulduğunda 8/8 temiz. Bu, bu görevin
  düzelttiği deadlock'tan TAMAMEN AYRI, bilinen bir test-altyapısı
  kırılganlığı (README'nin "tek yeşil koşu güçlü kanıt değildir" notuyla
  aynı sınıf) — gerçek CI'a bağlanmadan önce ayrı bir görevde ele
  alınmalı, bu görevin kapsamına alınmadı.

## Dependencies

- V1-WTR-027

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres dotnet test`:
  - `tests/Modules/Orders/SubmitOrder` → 16/16.
  - `tests/Host/Experience/Orders/TableDraft` → 66/66.
  - `tests/Modules/Inventory/BalanceProjection` → 12/12.
  - `tests/Modules/Inventory/ManualAdjustments` → 9/9.
  - `tests/Modules/Inventory/MovementLedger` → 18/18.
  - `tests/Modules/Inventory/MovementReversal` → 16/16.
  - `tests/Modules/Inventory/PortionReservations/CancellationEffects` → 9/9.
  - `tests/Modules/Inventory/ReservationBalanceProjection` → 13/13.
  - `tests/Modules/Inventory/StockMaster` → 14/14.
  - `tests/Modules/Inventory/WasteRecording` → 11/11.
  - `tests/Modules/Kitchen/TicketLifecycle` → 21/21.
  - `tests/Host/Experience/Inventory` → 11/11.
  - Hepsi regresyon; hiçbiri bu görevde değişmedi, hepsi hâlâ yeşil.
- `tests/E2E/WaiterPwa/specs/05-load-and-timing.spec.js` (gerçek Chrome +
  gerçek Postgres + gerçek Host):
  - Yanlış hipotez (yalnız satır kilidi) doğrulanırken: retry kapalıyken
    (`maxAttempts = 1`) 5 koşudan 3'ü başarısız — deadlock artık kilit
    alma çağrısının kendi içinde oluşuyordu.
  - Gerçek kök neden düzeltmesinden (global sıralı ön-kilitleme) SONRA,
    retry HÂLÂ kapalıyken (`maxAttempts = 1`), 8 koşu üst üste her
    seferinde 6/6 başarı — kilit sırası düzeltmesi TEK BAŞINA deadlock'u
    kapatıyor, retry olmadan bile.
  - `maxAttempts` 5'e geri alındıktan sonra tam paket (`--reporter=list`,
    tüm spec'ler) 3 kez koşuldu: 17/18 test her seferinde geçti, tek
    başarısız olan yine 05'in yük testiydi — ama bu sefer sunucu
    tarafında hiçbir hata/istisna yoktu (Host logları temiz), başarısızlık
    girişten sonraki masalar ızgarasının 15 saniyede görünür olmasını
    bekleyen İSTEMCİ TARAFI zaman aşımıydı. Tek başına koşulduğunda
    (yalnız 05) aynı koşum 8/8 temiz geçiyor. Bu, tüm paketin art arda
    aynı Node sürecinde birçok Chromium bağlamı açmasından kaynaklanan
    makine/CPU rekabetine işaret ediyor (gerçek bir Postgres/sunucu
    hatası değil) — README'nin zaten belirttiği "tek yeşil koşu güçlü
    kanıt değildir" notuyla aynı sınıftan bilinen bir test-altyapısı
    kırılganlığı, bu görevin kapsamı dışında bırakıldı (ayrı bir görev:
    ya paket çapında daha yüksek zaman aşımı ya da paralel worker
    sınırlaması).
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.

## Handoff

- None
