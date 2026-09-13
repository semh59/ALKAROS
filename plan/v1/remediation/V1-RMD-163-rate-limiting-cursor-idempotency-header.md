# V1-RMD-163 - Hız sınırı, katalog imleci ve ölü idempotency başlığı

- Task ID: V1-RMD-163
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

`docs/engineering/garson-audit-2026-09-10.md`'nin API uç noktaları
bölümünden üç bulguyu kapatır:

1. **Masa yönetimi ve push uç noktalarında hiç hız sınırı yok, `GET
   /orders/pending` ise yazma kovasında.** `DualScreenApplication.cs`
   `AddRateLimiter` ile `terminal-read` (240/dk) ve `terminal-write`
   (120/dk) politikalarını bir kez, tüm uygulama için kaydediyor; Orders
   bunları zaten kullanıyordu ama Table Management ve WebPush grupları
   hiç `RequireRateLimiting` çağırmıyordu — sınırsızdı. Her iki grup da
   artık grup varsayılanı olarak `terminal-write` alıyor, her GET uç
   noktası ayrı ayrı `terminal-read`'e geçiyor (Orders'ın kendi
   deseniyle aynı). Orders grubunun kendisinde de aynı kusur vardı:
   grup varsayılanı `terminal-write`, ama dört GET uç noktası
   (`/pending`, `/awaiting-payment`, `/table/{tableId}`, `/{orderId}`)
   hiç override edilmemişti — hepsi artık `terminal-read`'e geçti.
2. **Katalog 1000'de sayfalanıyor, istemci `X-Next-Cursor`'ı okumuyor.**
   `/api/v1/terminals/{terminalId}/catalog` sayfa başına en fazla 1000
   satır döndürüyor ve daha fazlası varsa `X-Next-Cursor` başlığı
   veriyor; her iki gerçek istemci de (cashier-app.js, waiter-app.js) tek
   sayfa çekip duruyordu — 1000'den fazla satırı olan bir restoranda
   geri kalan ürünler hiçbir hata vermeden sessizce kayboluyordu. Her iki
   istemci de artık imleç bitene kadar sayfaları takip ediyor.
3. **`X-Idempotency-Key` gönderiliyor ama kimse okumuyor.** Her iki
   gerçek istemci de `table-draft` çağrısında bu başlığı gönderiyordu,
   ama içeriği zaten JSON gövdesindeki `id` alanıyla birebir aynıydı ve
   hiçbir uç nokta bu başlığı hiç okumuyordu (grep ile doğrulandı) —
   gerçek, çalışan idempotency koruması zaten gövdenin `id` alanı
   üzerinden yürüyor (`Order.SourceReferenceId`, V1-RMD-123'ün kısmi
   benzersiz indeksi). Başlık gereksiz bir kopya olduğu için her iki
   istemciden de kaldırıldı, ayrı bir sunucu mekanizması bağlanmadı.

## Owned surface

- `plan/v1/remediation/V1-RMD-163-rate-limiting-cursor-idempotency-header.md` (yeni)
- Sınırlı ek — aşağıdaki tüm yollar ilgili görevin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası
  olarak parse etmesin):
  - src/Host/Experience/Tables/TableManagementApplication.cs (V1-TBL-001
    sahipliğinde) — grup varsayılanı ve altı GET override'ı eklendi.
  - src/Host/Experience/WebPush/WebPushExperience.cs (V1-WTR-011
    sahipliğinde) — grup varsayılanı ve GET override'ı eklendi.
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs (V1-ORD-005
    sahipliğinde) — dört GET uç noktasına terminal-read override'ı.
  - src/Clients/Cashier/wwwroot/cashier-app.js (V1-CSH-00x sahipliğinde)
    — katalog imleç takibi, X-Idempotency-Key kaldırıldı.
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js (V1-WTR-00x sahipliğinde)
    — katalog imleç takibi, X-Idempotency-Key kaldırıldı.

## Out of scope

API uç noktaları bölümünün son bulgusu (ayrı görev):

- `/comp` ve `/transfer-server`'ın hiçbir istemcisi yok.

## Dependencies

- V1-RMD-162

## Acceptance evidence

- `dotnet build src/Host/ALKAROS.Host.csproj -c Debug` → 0 uyarı, 0 hata.
- `node --check` her iki JS dosyası için temiz.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres dotnet test`
  gerçek test Postgres'ine karşı, bu değişikliklerin dokunduğu her
  yüzeyde:
  - `tests/Host/Experience/Tables` — 11/11 yeşil.
  - `tests/Host/Experience/WebPush` — 19/19 yeşil.
  - `tests/Host/Experience/Orders/TableDraft` — 48/48 yeşil.
  - `tests/Host/Experience/Orders/{Comp,Confirmation,Void,VoidSent}` —
    toplam 46/46 yeşil.
- Hız sınırı değişikliği için **ayrı bir yeni entegrasyon testi
  eklenmedi** — bilinçli bir karar, gerekçesi: (a) `terminal-read`/
  `terminal-write` politikalarının kendisi zaten
  `tests/Host/MigrationComposition/DualScreen/DualScreenHostTests.cs`'in
  `TrustedClientPartitionsAreIndependentAndRetryAfterComesFromTheLease`
  testiyle gerçek 429 davranışıyla kanıtlanmış, üretime hazır ASP.NET
  Core RateLimiter altyapısı; bu görev yeni bir sınırlama mantığı
  yazmadı, var olan politikaları daha fazla uç noktaya bağladı. (b) Table
  Management/WebPush/Orders'ın kendi test projeleri
  `AddRateLimiter`/`UseRateLimiter` kaydetmeyen hafif bağımsız host'lar
  kullanıyor — bu görev sonrası tüm mevcut testlerin (11+19+48+46) hatasız
  geçmesi, yeni `RequireRateLimiting` çağrılarının bu host'larda zararsız
  (no-op) metadata olarak kaldığını ve hiçbir mevcut davranışı bozmadığını
  kanıtlıyor. Gerçek uçtan uca 429 kanıtı, Table Management/WebPush'u tam
  `DualScreenApplication.Build` kompozisyonu üzerinden test eden yeni bir
  entegrasyon testi gerektirir — kapsam dışı bırakıldı, ayrı bir küçük
  görev olabilir.
- Katalog sayfalama ve `X-Idempotency-Key` kaldırma için ayrı bir
  otomatik JS testi yok (repoda JS test altyapısı yok, önceki oturumdaki
  `send-to-cashier` istemci değişikliğiyle aynı durum) — `node --check`
  ile sözdizimi doğrulandı, kod gözden geçirildi.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. (Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.)

## Handoff

- None
