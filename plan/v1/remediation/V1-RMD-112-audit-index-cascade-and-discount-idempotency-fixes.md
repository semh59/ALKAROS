# V1-RMD-112 - Inbox claim index, denial-events cascade, and discount idempotency

- Task ID: V1-RMD-112
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: implementation
- Surface state: Existing

## Goal

Semih onayıyla ("Düzeltme planı yapalım ve sırayla yapalım... bana
sormadan bitir", 2026-09-06), `docs/audit/INDEPENDENT_DEEP_AUDIT_2026-09-06.md`'nin
raporladığı ve bizzat doğruladığım bulgulardan en küçük, en cerrahi
üçünü kapatır (Dalga 1/N — kalan dalgalar ayrı görevlerde):

1. `inbox_messages`'ın kendi claim sorgusunu (`InboxStore.cs`) destekleyen
   hiçbir indeksi yoktu — her poll sequential scan yapıyordu.
2. `identity.denial_events` (güvenlik reddi denetim günlüğü) kullanıcı
   FK'sinde `ON DELETE CASCADE` taşıyordu — bir kullanıcı silinirse
   denetim izi sessizce yok olurdu (bugün hiçbir kod yolu
   `identity.users`'ı silmiyor — KVKK saklama anonimleştirir, silmez — ama
   yanlış bir invaryanttı).
3. `billing.bill_adjustments`'ta hiçbir idempotency koruması yoktu: bir
   tekrarlanan `POST .../bills/{billId}/discount` isteği (ağ zaman aşımı,
   çift gönderim) aynı isteğe ikinci bir indirim satırı ekliyordu.
   `/comp` ve `/void-sent`'in aksine (kendi `ExpectedRowVersion`
   iyimser eşzamanlılık kontrolleriyle "kazara" korunuyorlar),
   `ApplyBillDiscountRequestV1` hiç row version taşımıyordu ve her
   çağrıda çağıranın idempotency anahtarından bağımsız taze bir
   `Guid.NewGuid()` kullanılıyordu.

## Owned surface

- `database/migrations/V1/V1-RMD-112/**` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik
  iddiası olarak parse etmesin):
  - database/MigrationComposition/order.json,
    src/Host/Composition/Migrations/MigrationManifest.cs (V1-RMD-103
    sahipliğinde) — migration 074/075 eklendi, PhaseBMax "073" -> "075".
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs (V1-FND-004
    sahipliğinde) — 74 pozisyonluk yeni sayım/son giriş/aralık-dışı örnek.
  - src/Modules/Billing/Adjustments/BillAdjustment.cs,
    IBillAdjustmentRepository.cs, PostgresBillAdjustmentRepository.cs ve
    tests/Modules/Billing/Adjustments/** (V1-BIL-003 sahipliğinde) —
    IdempotencyKey alanı eklendi (yalnızca CreateDiscountPercentage/
    CreateDiscountAmount'a; CreateServiceFee/CreateTip'in bugün HTTP'den
    hiç çağrılan bir yolu yok, dokunulmadı), 2 yeni test.
  - src/Host/Experience/Billing/BillingSplitStore.cs ve
    tests/Host/Experience/Billing/BillingSplitHttpTests.cs (V1-RMD-103
    sahipliğinde) — ApplyDiscountAsync zaten tuttuğu FOR UPDATE kilidi
    içinde aynı idempotency anahtarını taşıyan mevcut bir ayarlama olup
    olmadığını kontrol ediyor; varsa onu döndürüyor, yeni satır eklemiyor.

## In scope

1. `database/migrations/V1/V1-RMD-112/074-*.up/down.sql`:
   `ix_inbox_messages_claimable` (kısmi indeks, yalnızca
   `pending`/`in_flight`) + `identity.denial_events`'in kullanıcı FK'sini
   `ON DELETE CASCADE`'den `ON DELETE RESTRICT`'e çeviriyor.
2. `database/migrations/V1/V1-RMD-112/075-*.up/down.sql`:
   `billing.bill_adjustments.idempotency_key` (nullable) +
   `(bill_id, idempotency_key) WHERE idempotency_key IS NOT NULL` kısmi
   benzersiz indeks — 074'ten ayrı migrasyon, çünkü dar
   `ALKAROS.Billing.Adjustments.Tests` fixture'ı identity şemasını hiç
   yüklemiyor (074 identity.denial_events'e dokunduğu için oraya
   eklenemezdi).
3. `BillAdjustment`/repository: `IdempotencyKey` alanının okuma/yazma
   bağlantısı.
4. `BillingSplitStore.ApplyDiscountAsync`: mevcut kilit içinde
   idempotency-anahtar eşleşmesi kontrolü — eşleşme varsa mevcut
   ayarlamayı (ve ondan hesaplanan özeti) döndürür, yeni satır eklemez.

## Out of scope

- Raporun geri kalan bulguları (submit-draft'ın mutfak bileti
  oluşturmaması, Bearer token tutarsızlığı, Production/Purchasing'in
  Inventory şemasına kaçak yazması, 5 modülün mimariye kaydı, arayüz
  bulguları) — sıradaki dalgalarda, ayrı görevlerde.
- Sipariş-döngüsel FK (`table_mgmt.tables.current_order_id` ↔
  `orders.orders`) — TRACEABILITY C50 yorumuyla doğrulandı, bilinçli
  tasarım kararı, defekt değil, dokunulmadı.
- `Kitchen->Orders`/`Billing->Orders` doğrudan proje referansları —
  `ModuleBoundaryTests.cs`'in `ApprovedEdges` sözlüğünde onaylı, test
  edilen, belgelenmiş kenarlar; raporun "yasak" çerçevelemesi yanlış,
  defekt değil, dokunulmadı.

## Dependencies

- V11-RMD-001

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 uyarı / 0 hata.
- `docker compose -f compose.yaml -f compose.test.yaml run --rm test`:
  **79/79 test projesi, sıfır başarısız** (`ALKAROS.Billing.Adjustments.Tests`
  16/16, 2 yeni; `ALKAROS.Host.Experience.Billing.Tests` 11/11, 1 yeni;
  `ALKAROS.Host.Tests` MigrationComposition 74 pozisyon dahil yeşil).
- Revert-and-confirm: `ApplyDiscountAsync`'deki eşleşme kontrolü geçici
  olarak `false &&` ile devre dışı bırakılıp
  `RetryingADiscountWithTheSameIdempotencyKeyDoesNotDuplicateTheAdjustment`
  çalıştırıldı — beklendiği gibi veritabanının kendi benzersiz indeksi
  ihlal edilip 503 ile başarısız oldu (uygulamanın kendi kontrolü devre
  dışıyken bile veri bütünlüğü korunuyor); kod geri yüklenip tam süit
  yeniden yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate`,
  `validate-coverage`: sıfır hata.
- `python tools/consistency-audit/consistency_audit.py`: bu görevin
  değiştirdiği hiçbir dosyada ihlal yok.

## Handoff

- V1-GOV-101
