# V12-ONL-003 - Implement online status and cancellation synchronization

- Task ID: V12-ONL-003
- Status: Done
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.34-I.37
- PDF:II.2.19
- PDF:II.7.4
- PDF:III.22

## Goal

Provider status/cancellation değişikliklerini race-safe local transition ile işlemek ve çözümlenemeyen divergence
evidence event'i üretmek.

## Owned surface

- `src/Modules/OnlineOrdering/Yemeksepeti/StatusSync/**`, `tests/Modules/OnlineOrdering/Yemeksepeti/StatusSync/**`
- `src/Host/Experience/OnlineOrdering/YemeksepetiStatusSyncService.cs` — bu görevle oluşturulan yeni dosya (aşağıdaki
  yol notuna bakın).
- `evidence/V12-ONL-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Düzeltilen yol notu: bir iptalin veya teslimin yerel etkisi Order, Kitchen ve Inventory'yi birlikte gerektirir;
  Online Ordering modülü yalnız Catalog'u doğrudan çağırabildiği için bu orkestrasyon Host'ta yaşar (V12-ONL-002 ile
  aynı gerekçe). Modül tarafında dışa giden durum olayı, taslak sağlayıcı istemcisi ve outbox tüketicisi bulunur.
  Semih 2026-09-25'te "Sınırlı ek + yol notu" kararını verdi.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/OnlineOrdering/YemeksepetiOrderIntakeService.cs (V12-ONL-002 sahipliğinde) — iptaller artık
    burada talep edilip durum senkronizasyonuna verilir; aynı sipariş kilidi paylaşılır. Sağlayıcı tarafından
    iptal edilmiş bir sipariş sonradan gelen yeni sipariş olayıyla oluşturulmaz. Teslimat türü sonuç kaydına yazılır.
    Stok ayrılamayan veya ürünleri satılamayan sipariş için sağlayıcıya ITEM_UNAVAILABLE iptali kuyruğa alınır;
    yapısal olarak bozuk yük ise incelemeye bırakılır.
  - src/Modules/OnlineOrdering/Yemeksepeti/OrderNormalization/YemeksepetiInboxProcessingStore.cs (V12-ONL-002
    sahipliğinde) — dört yeni işleme sonucu: OrderCancelled, AlreadyCancelled, CancelledBeforeOrder,
    SkippedCancelledOrder.
  - src/Modules/OnlineOrdering/ALKAROS.OnlineOrdering.csproj ve OnlineOrderingModule.cs (V12-MAP-001 sahipliğinde) —
    Messaging/IntegrationContracts referansları, sağlayıcı istemcisi ve tüketici kaydı.
  - src/Host/Experience/OnlineOrdering/YemeksepetiWebhookEndpoints.cs ve tests/Host/Experience/OnlineOrdering/
    (V12-ONL-001 sahipliğinde) — durum senkronizasyon servisinin TryAdd kaydı; yeni Host testleri, fixture
    yardımcıları, outbox migration fixture satırları ve bir intake testinin yeni davranışa göre güncellenmesi.
  - ALKAROS.slnx ve `dotnet restore --force-evaluate`'in güncellediği packages.lock.json dosyaları.
- Doğrulanmamış taslak notu: sağlayıcı istemcisi yalnız kayıtlı kaynak olan developer.yemeksepeti.com/api-specifications
  sayfasına dayanır: token isteği form-urlencoded; güncelleme `PUT /v2/chains/{chain_id}/orders/{order_id}`;
  iptal gerekçesi `cancellation.reason`. Belgenin S3'teki ikinci herkese açık kopyası adres, yol ve `cancellation`
  biçimi konusunda çelişiyor; bu çelişki çözülmedi ve kanıtta kayıtlı. Hiçbir çağrı sandbox'a veya üretime yapılmadı
  (V0-YSP-001 `Blocked`).

## In scope

- Outbound idempotency, late cancellation, already-preparing policy, retry ve typed divergence event.

## Out of scope

- Webhook intake, product mapping ve ReconciliationCase oluşturma.

## Dependencies

- V12-ONL-001
- V12-ONL-002
- V12-MAP-002
- V0-YSP-001
- V1-SEC-001
- V1-SEC-002
- V11-RSV-003

## Deliverables

- `src/Modules/OnlineOrdering/Yemeksepeti/StatusSync/**` altında Goal kapsamını uygulayan production code ve
  task-specific automated test assets.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Duplicate status tek etki üretir; cancellation race deterministik kapanır; çözümlenemeyen fark aynı evidence event'i
  idempotent olarak üretir.
- Mutfak başlamadan provider iptali V11-RSV-003'ün tam olarak bir Release sonucuna, hazırlık başladıktan sonra aynı
  görevin tam olarak bir Waste sonucuna bağlanır; crash/retry stok etkisini tekrarlamaz ve bu görev ReconciliationCase
  oluşturmaz.
- Kapanış kanıtı (2026-09-26, gerçek PostgreSQL 18 UTF8, port 56433): Host testleri 26/26 (12 yeni durum
  senkronizasyonu testi), StatusSync modül testleri 13/13 yeşil. Sonuçlar:
  - Mutfak başlamadan gelen sağlayıcı iptali tam bir Release üretir; ikinci iptal olayı `AlreadyCancelled` olur ve
    stoğa ikinci kez dokunmaz.
  - Hazırlık başladıktan sonra gelen iptal tam bir Waste üretir.
  - Önceki deneme tazmini yapıp çöktükten sonraki tekrar yine tek Release bırakır.
  - Siparişini geçen bir iptal (`CancelledBeforeOrder`), sonradan gelen sipariş olayının siparişi hiç
    oluşturmamasını sağlar (`SkippedCancelledOrder`).
  - Teslimden sonra gelen iptal, sipariş `Served` kalırken tipli `Diverged` kanıtı üretir; kanıt kimliği tekrarlarda
    aynıdır.
  - Teslim hold'u tüketir, siparişi `Served` yapar ve sağlayıcıya tek bir READY_FOR_PICKUP veya DISPATCHED
    kuyruğa alır; tekrarı hiçbir şey yapmaz.
  - Restoran iptali Release yapar ve TOO_BUSY gerekçesiyle tek bir iptal kuyruğa alır. Teslim edilmiş sipariş
    restoranca iptal edilemez.
  - Stoksuz ve eşlenmemiş siparişler için ITEM_UNAVAILABLE iptali kuyruğa alınır; bozuk yük için alınmaz.
  - Teslim ile sağlayıcı iptali aynı anda yarıştığında sipariş beş turun hepsinde tek bir tutarlı sonuçla kapanır.
  - Hiçbir yol ReconciliationCase oluşturmaz.
- Mutasyon kontrolü (dosyalar yedekten geri yüklenip `cmp` ile doğrulandı): tazmin mutfak iptalinden sonraya
  alınınca 1, iptal-önce-sipariş kontrolü kapatılınca 1, teslimde tüketim kaldırılınca 3, kanıt kimliği rastgele
  yapılınca 1, token önbelleği kapatılınca 1 test kırmızıya döndü.
- Süreç notu: ilk mutasyon turunda geri yükleme için `git checkout` kullanılınca intake dosyasındaki commit
  edilmemiş ekler silindi. Ekler scratchpad'deki düzenleme scriptinden yeniden uygulandı, geride kalan mutasyonlar
  geri alındı ve tüm paketler yeniden yeşile döndükten sonra mutasyonlar yedek/`cmp` yöntemiyle yeniden koşuldu.
- Kanıt: `evidence/V12-ONL-003/`.

## Handoff

- V12-REC-001
- V12-STK-001
