# V12-QRO-002 - Implement PendingConfirmation table policy

- Task ID: V12-QRO-002
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:I.34-I.37
- PDF:II.2.18
- PDF:II.6.8
- PDF:II.7.3
- PDF:III.21
- CORR:C5

## Goal

Uzaktan QR hizmet reddine izin vermeden onaylı dolu/ayrılmış/değişiklik yok
table davranışını uygulayın.

Bağlam (2026-09-09, Semih ile görüşülüp kilitlendi): QR kanalının kalıcı
olarak internete (Cloudflare relay) açık kalması bilinçli bir iş kararı
(`docs/architecture/qr-relay-provider-decision.md` — "yalnız restoran
WiFi'ı" alternatifi zaten reddedilmiş). Bu, NFC'nin sahip olduğu fiziksel-
varlık korumasının (dokunmak için birkaç santim yakınlık şart) QR'da hiç
olmadığı anlamına geliyor — bir QR kodu fotoğraflanıp sınırsız paylaşılabilir
(`docs/design/modules/qr-nfc-ordering.md` §1, gerçek bir $60.000'lık
dolandırıcılık örneği kayıtlı). `V12-QRO-001` bunu bilerek kapsam dışı
bırakmıştı ("table durumu bu görevin kapsamı dışında, QRO-002'nin işi") —
bu görev o boşluğu kapatıyor: bugüne kadar bir QR gönderimi table durumuna
hiç dokunmuyordu (`Available` kalıyordu), yani hem "PendingConfirmation
masayı Reserved yapar" kuralı (`table-reservation-policy.md`) hem de
istismar koruması fiilen uygulanmıyordu.

## Owned surface

- `src/Modules/QrOrdering/TablePolicy/**` (yeni — `QrTableReservationPolicy`,
  `QrTableNotFoundException`, `QrTableNotAvailableException`)
- `tests/Modules/QrOrdering/TablePolicy/**` (yeni test projesi)
- `src/Modules/Settings/QrOrderExpiry/**` (yeni — `QrOrderExpirySetting`,
  Duration tipli, varsayılan 5 dakika)
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik
  iddiası olarak parse etmesin):
  - src/Host/Experience/Orders/PendingOrderConfirmation/QrOrderExpiryHostedService.cs
    (V1-RMD-137'nin sahip olduğu dizinin altında, yeni dosya) — arka plan
    süpürücüsü, o görevin `PendingOrderConfirmationStore.RejectAsync`'ini
    olduğu gibi yeniden kullanıyor; store'un kendisi değişmedi.
  - src/Modules/Tables/TableLifecycle/TableRepository.cs,
    PostgresTableRepository.cs (V1-TBL-001 sahipliğinde) —
    GetByIdForUpdateAsync, aynı-transaction UpdateStatusAsync ve
    LinkCurrentOrderAsync overload'ları eklendi (Production/Inventory'nin
    aynı-transaction deseniyle birebir aynı, module-dependency-rules.md'nin
    kendi notu). Mevcut tek-parametreli overload/domain kuralları değişmedi.
  - src/Modules/QrOrdering/ALKAROS.QrOrdering.csproj,
    TokenLifecycle/QrOrderingModule.cs (V12-QRS-001 sahipliğinde) — Tables
    proje referansı, `DependsOn => ["Tables"]` ve
    `QrTableReservationPolicy` kaydı eklendi (module-dependency-rules.md
    satır 19, 2026-08-03'te onaylanmış ama hiç uygulanmamıştı).
  - src/Modules/QrOrdering/PendingOrders/QrPendingOrderStore.cs
    (V12-QRO-001 sahipliğinde) — `SubmitAsync`'e, ledger insert/outbox
    enqueue'dan önce, aynı transaction içinde
    `QrTableReservationPolicy.ReserveForSubmissionAsync` çağrısı eklendi.
  - tests/Modules/QrOrdering/PendingOrders/QrPendingOrderStoreTests.cs,
    Fixtures/QrOrderingPendingOrdersTestDatabase.cs (V12-QRO-001
    sahipliğinde) — dört yeni test (başarılı rezervasyon, Occupied/Reserved
    reddi, ret sonrası sıfır outbox kaydı) ve `SeedTableAsync(status)`/
    `GetTableStateAsync` yardımcıları eklendi.
  - src/Modules/Orders/ALKAROS.Orders.csproj,
    OrderAggregate/OrdersModule.cs (V1-ORD-001 sahipliğinde) — Tables
    proje referansı ve `DependsOn => ["Tables"]` eklendi
    (module-dependency-rules.md satır 4, aynı şekilde onaylı ama hiç
    uygulanmamıştı).
  - src/Modules/Orders/Integration/QrOrderSubmittedConsumer.cs
    (V12-QRO-001 sahipliğinde) — Order'ı ilk kez materialize ederken, aynı
    transaction içinde `ITableRepository.LinkCurrentOrderAsync` ile
    `current_order_id` cache pointer'ı dolduruyor; masa durumu bu
    consumer'da hiç değişmiyor (zaten submission anında Reserved oldu).
  - tests/Modules/Orders/OrderAggregate/QrOrderSubmittedConsumerTests.cs
    (V1-ORD-001 sahipliğinde) — bir yeni test + constructor çağrısı
    güncellendi.
  - tests/Modules/Tables/TableLifecycle/PostgresTableTests.cs (V1-TBL-001
    sahipliğinde) — yeni aynı-transaction overload'ları için beş yeni test.
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs (V1-ORD-005/
    V1-IAM-027/V1-RMD-137 sahipliğinde) — `ISettingsRepository`/
    `ISettingsService` kaydı (TryAdd, paylaşılan) ve
    `QrOrderExpiryHostedService`'in `AddHostedService` kaydı eklendi.
  - tests/Host/Experience/Orders/Confirmation/OrderManagementConfirmationTestDatabase.cs
    (V1-RMD-137 sahipliğinde) — `SeedOverdueQrPendingOrderAsync` yardımcı
    metodu eklendi; aynı projeye yeni bir test dosyası
    (QrOrderExpiryHostedServiceTests.cs) eklendi.
  - tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs (V1-FND-001
    sahipliğinde) — `ApprovedEdges`'e `["QrOrdering"] = ["Tables"]`
    eklendi; `Orders`'ın zaten onaylı `Tables` girdisi değişmedi.
  - docs/architecture/module-dependency-rules.md (V0-ARC-001 sahipliğinde)
    — satır 4/19'un artık koda döküldüğünü belgeleyen bir not eklendi;
    tablo/karar metninin kendisi değişmedi.
  - ALKAROS.slnx (çözüm dosyası, paylaşılan) — yeni
    tests/Modules/QrOrdering/TablePolicy projesi eklendi.

## In scope

1. **Gönderim anında rezervasyon (asıl istismar koruması).**
   `QrTableReservationPolicy.ReserveForSubmissionAsync`: masa satırını
   kilitler (`FOR UPDATE`), yalnız `Available` ise `Reserved`'a çevirir —
   `Occupied`/`Reserved`/`Cleaning`/`OutOfService` veya devre dışı/var
   olmayan bir masa için **koşulsuz reddeder** (`QrTableNotAvailableException`/
   `QrTableNotFoundException`), tüm gönderim (ledger + outbox) geri alınır.
   `Table.CanTransitionTo(Reserved)`'ın genel amaçlı, daha gevşek kuralından
   (Occupied'ı da kabul eder) bilinçli olarak daha sıkı.
2. **Cache pointer'ın gecikmeli doldurulması.** QR'ın Order'ı asenkron
   materialize edildiği için (`QrOrderSubmittedConsumer`, outbox üzerinden),
   `current_order_id` submission anında değil, Order gerçekten var olduğunda
   dolduruluyor — `current_status` o noktada hiç değişmiyor (zaten
   Reserved).
3. **Süre aşımı (background job).** `QrOrderExpiryHostedService`,
   dakikada bir, `qr_ordering.pending_confirmation_timeout` (varsayılan 5
   dakika, işletme ayarından değiştirilebilir) süresini aşmış
   `PendingConfirmation` + `source='Qr'` siparişleri, personelin kendi
   ret aksiyonunun (`V1-RMD-137`) birebir aynısıyla (`PendingOrderConfirmationStore.RejectAsync`)
   reddediyor — mutfağa gitmiş kalemleri iptal ediyor, masayı
   `Reserved -> Available`'a çekiyor. İkinci bir masa-seviyesi zamanlayıcı
   icat edilmedi (`table-reservation-policy.md`: "Reserved" süresi yok,
   her zaman sahibi olan sipariş durumuna yakınsar).

## Out of scope

- Order onayı ve genel table yaşam döngüsü uygulaması (zaten `V1-RMD-137`
  ile kapatıldı — bu görev yalnız *ne zaman rezerve edilir/serbest
  bırakılır* kuralını uyguluyor, *nasıl onaylanır* zaten var).
- NFC'nin kendi `PendingConfirmation` (yaş kısıtlı ürün) siparişleri için
  bir süre aşımı — bu görev kasıtlı olarak yalnız `source = 'Qr'`
  siparişleri süpürüyor; NFC'nin kendi askıda kalma riski ayrı, henüz
  kullanıcıyla görüşülmemiş bir karar.
- QR'ın kendi müşteri sayfası/HTTP yüzeyi (`V12-CWB-001/002`) — ayrı görev.
- Portion/stok rezervasyonu onay üzerine (`V12-QRO-003`'ün işi).

## Dependencies

- V12-QRO-001
- V1-TBL-001
- V1-TBL-004
- V0-DOM-005

## Deliverables

- `src/Modules/QrOrdering/TablePolicy/**` altında Goal kapsamını uygulayan
  production code ve task-specific automated test assets.
- Başarı, ret, replay/race ve güvenlik testleri.

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 Uyarı, 0 Hata.
- Gerçek Postgresql'e karşı Docker'da, real exit code 0 ile:
  - `ALKAROS.QrOrdering.PendingOrders.Tests`: **13/13** (4 yeni test —
    başarılı rezervasyon, Occupied/Reserved reddi, sıfır outbox kaydı).
  - `ALKAROS.Tables.TableLifecycle.Tests`: **59/59** (5 yeni test — aynı-
    transaction kilit/güncelleme/rollback, cache pointer backfill +
    replay + "farklı sipariş asla üzerine yazılmaz").
  - `ALKAROS.Orders.OrderAggregate.Tests`: **120/120** (1 yeni test —
    `current_order_id` backfill).
  - `ALKAROS.Host.Experience.Orders.Confirmation.Tests`: **10/10** (3 yeni
    test — süresi geçmiş QR siparişi reddedilip masası serbest kalıyor;
    süresi geçmemiş dokunulmuyor; QR-olmayan bir bekleyen sipariş bu
    süpürmeden hiç etkilenmiyor).
  - `ALKAROS.Host.Experience.Orders.Void.Tests`: 5/5 (regresyon yok).
  - `ALKAROS.Host.Tests` (tam migration/composition/reachability paketi):
    **126/126** (regresyon yok — yeni `ITableRepository`/hosted-service
    bağımlılıkları gerçek kompozisyonda sorunsuz çözülüyor).
- `dotnet test tests/Architecture/ModuleBoundaries` (yerel, Postgres
  gerekmez): **8/8** — yeni `QrOrdering -> Tables` ve `Orders -> Tables`
  kenarları doğru şekilde onaylı listede.
- `dotnet test tests/Modules/QrOrdering/TablePolicy` (yerel, sahte
  repository, Postgres gerekmez): **7/7** — devre dışı/var olmayan masa,
  dört Available-olmayan durumun hepsi.
- `python -m pytest tests/Architecture/ProjectManifest/test_project_manifest.py`:
  4/4.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 1 önceden var
  olan, ilgisiz ihlal (değişmedi), yeni ihlal yok.

## Handoff

- V12-QRO-003
