# V12-ONL-002 - Implement OnlineExternalOrder normalization

- Task ID: V12-ONL-002
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

Kabul edilen webhook verisini tek provider kaydına ve tek dahili Accepted Order'a idempotent olarak bağlamak.

## Owned surface

- `src/Modules/OnlineOrdering/Yemeksepeti/OrderNormalization/**`,
  `tests/Modules/OnlineOrdering/Yemeksepeti/OrderNormalization/**`, `database/migrations/V12/V12-ONL-002/**`
- `src/Host/Experience/OnlineOrdering/YemeksepetiOrderIntakeService.cs` ve
  `src/Host/Experience/OnlineOrdering/YemeksepetiInboxProcessingHostedService.cs` — bu görevle oluşturulan yeni
  dosyalar (aşağıdaki yol notuna bakın).
- `evidence/V12-ONL-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Düzeltilen yol notu: Accepted Order, hold ve mutfak fişinin tek transaction'da yazılması Order, Kitchen ve
  Inventory'yi birlikte gerektirir. Online Ordering modülü ise doğrudan yalnız Catalog'u çağırabilir
  (module-dependency-rules.md satır 20). Bu yüzden orkestrasyon, NFC'nin güvenilir kanal akışıyla aynı biçimde
  Host'ta yaşar; modül tarafı yük ayrıştırma, eşleme çözümlemesi ve inbox işleme defteridir. Semih 2026-09-25'te
  "Sınırlı ek + yol notu" kararını verdi.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/OnlineOrdering/Yemeksepeti/WebhookInbox/YemeksepetiWebhookInbox.cs (V12-ONL-001 sahipliğinde) —
    yalnız `OpenPayload`; zarfı açabilen tek bileşen hâlâ inbox'tır.
  - src/Modules/OnlineOrdering/OnlineOrderingModule.cs (V12-MAP-001 sahipliğinde) — normalizer kaydı.
  - src/Host/Experience/OnlineOrdering/YemeksepetiWebhookEndpoints.cs ve tests/Host/Experience/OnlineOrdering/
    (V12-ONL-001 sahipliğinde) — işleme servisleri, hosted service ve katalog deposu TryAdd kayıtları; intake Host
    testleri, test fixture'ı ve migration fixture listesi.
  - database/MigrationComposition/order.json, src/Host/Composition/Migrations/MigrationManifest.cs ve
    tests/Host/MigrationComposition/Manifest/ManifestTests.cs — 146 numaralı migration konumu.
  - tools/consistency-audit/unreachable_services_allowlist.json (V1-RMD-273 sahipliğinde) — eşleme servisi artık
    çalışma zamanında çağrıldığı için V12-MAP-001'in iki satırı silindi.
  - ALKAROS.slnx ve `dotnet restore`'un ürettiği packages.lock.json.
- Tasarım notu: online siparişin hold'u kabulde tüketilmez, `Reserved` kalır. Böylece sağlayıcı iptali V11-RSV-003
  üzerinden tam bir Release veya Waste'e dönüşebilir (V12-ONL-003). Müşteri adı, telefon ve adres siparişe
  taşınmaz; yalnız şifreli webhook yükünde kalır. Sağlayıcı yük şekli herkese açık Partner API v2.0.2 belgesine
  dayanan doğrulanmamış taslaktır (V0-YSP-001 `Blocked`).

## In scope

- External identity, normalized customer/order alanları ve Accepted Order ile stock reservation'ın atomik oluşturulması.

## Out of scope

- Provider status eşleme, iptal aktarımı, ürün yapılandırması ve reconciliation case persistence.

## Dependencies

- V12-ONL-001
- V1-ORD-001
- V12-MAP-001
- V12-STK-001
- V1-FND-005

## Deliverables

- `src/Modules/OnlineOrdering/Yemeksepeti/OrderNormalization/**` altında Goal kapsamını uygulayan production code ve
  task-specific automated test assets.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Aynı harici order ID, bir dahili Order ile eşleşir; desteklenmeyen veri yükü kısmi Order yerine typed ret veya
  divergence evidence üretir.
- Son porsiyon yarışında Accepted Order ve reservation birlikte commit edilir; OutOfStock veya provider/local divergence
  typed evidence üretir ve kısmi Order bırakmaz.
- Kapanış kanıtı (2026-09-26, gerçek PostgreSQL 18 UTF8, port 56433): normalizer modül testleri 18/18, Host
  testleri 14/14 (5 webhook + 9 intake) yeşil. Yeni sağlayıcı siparişi tek bir `Accepted` Online siparişe dönüşür;
  aynı transaction'da `Online` kanal etiketli `Reserved` hold ve mutfak fişi yazılır, inbox olayı `OrderCreated`
  olur. Aynı sağlayıcı siparişi iki farklı olayla gelirse de tek sipariş oluşur: dört paralel işleyicide bile ikinci
  olay `OrderAlreadyExists` olur. Son porsiyonu başka kanal tutuyorsa sipariş, hold ve mutfak fişi yazılmaz; olay
  `Diverged` olur ve `OutOfStock` ayrıntısını taşır. Aynı son porsiyon için yarışan iki sağlayıcı siparişinden biri
  `OrderCreated`, diğeri `Diverged` olur. Eşlenmemiş SKU'da olay `Rejected`/`UnmappedSku` olur ve hiçbir şey yazılmaz.
  No-op ve bilinmeyen durumlar siparişe dokunmaz; bilinmeyen durumun kanıt kimliği olay kaydında durur. İptaller
  V12-ONL-003'ün işlemesi için beklemede kalır. Bozuk bir olay en fazla 5 kez denenip `Failed` olarak kapanır ve
  arkasındaki olayı engellemez. Migration 146 geri alınıp yeniden uygulanabilir.
- Mutasyon kontrolü (geri alındı, dosyalar birebir eşleşti; Host mutasyonları ikişer kez koşuldu): sipariş başına
  kilit kaldırılınca 1, hold reddi yok sayılınca 2, iptaller de talep edilince 1 Host testi; ilk eşlenemeyen satırda
  reddetme yerine atlama yapılınca 3, kesirli miktara izin verilince 1 normalizer testi kırmızıya döndü. Normalizer
  testleri gerçek bir hata da buldu: sayı olmayan bir JSON değeri `TryGetDecimal`'ı exception'a düşürüyordu; tipli
  ret olarak düzeltildi.
- Kanıt: `evidence/V12-ONL-002/`.

## Handoff

- V12-ONL-003
- V12-REC-001
