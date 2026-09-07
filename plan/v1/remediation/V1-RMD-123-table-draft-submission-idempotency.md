# V1-RMD-123 - Table-draft submission idempotency (two-phase retry fix)

- Task ID: V1-RMD-123
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: implementation
- Surface state: Existing

## Goal

Semih onayıyla ("Taze denetim yap, sonra düzeltmeleri yap" — Dalga 4,
"Orders'ın two-phase retry Critical'ı"), taze bir bağımsız denetimde bulunan
ve doğrulanan Critical defekti kapatır: WaiterPwa'nın (çevrimdışı kuyruk) ve
Cashier'ın "sipariş gönder" akışı, tablo başına iki ayrı HTTP çağrısından
oluşan bir "two-phase" işlemdir — `POST .../table-draft` (Draft sipariş
oluştur/güncelle) ve ardından `POST .../submit-draft` (Submitted'a geçir,
mutfağa gönder). `submit-draft` tam olarak başarılı olduktan (sipariş
Submitted, mutfak bileti oluşturulduktan) SONRA yanıt istemciye ulaşmadan
bağlantı koparsa (yaygın bir "ambiguous failure" senaryosu — tam olarak
istemcilerin zaten "retry-safe" olduğunu varsaydığı durum), istemci TÜM
iki-fazlı işlemi baştan tekrar dener. `table-draft`'ın mevcut sipariş
araması yalnızca `status = 'Draft'` filtreliyordu — artık Submitted olan
siparişi hiç görmüyordu — bu yüzden tekrar deneme, aynı masa için TAMAMEN
YENİ, ikinci bir sipariş yaratıp onu da mutfağa gönderiyordu (gerçek
çift-mutfak-dispatch riski). Her iki istemci de zaten bu tam senaryo için
üretilmiş, tekrar denemeler arasında sabit bir istemci-üretimi kimliği
(`orderPayload.id`) gönderiyordu — sunucu bunu hiç okumuyordu.

## Owned surface

- `plan/v1/remediation/V1-RMD-123-table-draft-submission-idempotency.md` (yeni)
- `database/migrations/V1/V1-RMD-123/**` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası
  olarak parse etmesin):
  - src/Host/Experience/Orders/OrderManagementContracts.cs,
    OrderManagementStore.cs (V1-RMD-111 sahipliğinde) — `CreateTableDraftRequest`'e
    opsiyonel `Id` alanı; `CreateOrUpdateTableDraftAsync` önce submission id'ye
    göre mevcut siparişi arıyor (durumu ne olursa olsun) ve varsa onu
    replay ediyor; yeni sipariş oluştururken `sourceReferenceId: submissionId`
    kaydediyor; birleştirme dalında `currentOrder.SourceReferenceId`'i
    koruyor; eşzamanlı benzersizlik ihlalini yakalayıp kazananın siparişini
    döndürüyor.
  - database/MigrationComposition/order.json,
    src/Host/Composition/Migrations/MigrationManifest.cs (V1-RMD-103
    sahipliğinde) — migration 076 eklendi, PhaseBMax "075" -> "076".
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs (V1-FND-004
    sahipliğinde) — 75 pozisyonluk yeni sayım/son giriş/aralık-dışı örnek
    (076 artık geçerli olduğu için "outside range" örneği 077'ye kaydı).
  - tests/Host/Experience/Orders/TableDraft/OrderManagementTableDraftHttpTests.cs
    (V1-RMD-111 sahipliğinde) — bu görevin iki regresyon testi.
  - src/Clients/Cashier/wwwroot/cashier-app.js (V1-RMD-102 sahipliğinde) —
    artık yanlış olan "X-Idempotency-Key bu rotada uygulanmıyor" yorumu
    güncellendi (rota artık gövdedeki `id`'yi kontrol ediyor; bu tıklama
    her seferinde taze bir `id` ürettiği için çift-tıklama'ya yardımı
    olmuyor, `dispatchInFlight` hâlâ gerçek koruma — davranış değişmedi).

## In scope

1. **Migration 076**: `orders.orders (table_id, source_reference_id)` üzerinde
   `source_reference_id IS NOT NULL` kısmi benzersiz indeks.
2. **`CreateTableDraftRequest.Id`** (opsiyonel `Guid?`): her iki üretim
   istemcisinin de zaten gönderdiği (`orderPayload.id`, JSON `"id"` alanı,
   case-insensitive binding ile otomatik eşleşiyor — istemci tarafında hiçbir
   değişiklik gerekmedi) submission-kimliği artık okunuyor.
3. **`CreateOrUpdateTableDraftAsync`**: `request.Id` varsa, Draft-only
   aramadan ÖNCE `(table_id, source_reference_id)` ile mevcut siparişi arar
   — bulunursa (Draft veya Submitted veya ötesi fark etmez) onu olduğu gibi
   döndürür, yeni sipariş oluşturmaz/mutfağa tekrar göndermez. Yeni sipariş
   oluştururken `sourceReferenceId`'i kaydeder; birleştirme dalında
   mevcut siparişin `SourceReferenceId`'ini korur (aksi halde `Order`'ı
   yeniden kurarken sessizce sıfırlanırdı). Eşzamanlı iki ilk-deneme
   yarışını (aynı submission id, ikisi de "bulunamadı" görüp ekleme
   dener) veritabanının benzersiz indeksi çözer; kaybeden tarafın
   `PostgresException`'ı yakalanıp kazananın siparişi döndürülür.

## Out of scope

- **Cashier'ın manuel yeniden-tıklama senaryosu.** `dispatchOrderToKitchen()`
  her tıklamada taze bir `orderPayload.id` üretiyor (kalıcı bir çevrimdışı
  kuyruğu yok) — bu düzeltme yalnız AYNI payload'ın (aynı id) tekrar
  gönderildiği durumu (WaiterPwa'nın çevrimdışı kuyruğu, veya düz bir ağ
  tekrar denemesi) kapsıyor. Cashier'ın çift-tıklama koruması zaten
  `dispatchInFlight`'a dayanıyor (V1-RMD-102) — bu görev onu değiştirmiyor,
  yalnız artık yanlış olan yorumu düzeltiyor.
- **`table_mgmt.tables` UPDATE'inin kendi compare-and-swap koruması
  olmaması** (`WHERE table_id = @table_id` — DualScreenStore'un masa
  oturtma akışının aksine hiçbir `current_status`/`current_order_id`
  koşulu yok). Gerçek ama ayrı bir bulgu/karar — bu görev yalnız sipariş
  tekilliğini kapsıyor, tablo işaretçisinin kendi eşzamanlılık modelini
  değil.
- Diğer denetim adayları (Dalga 5: Purchasing'in atomiklik sorunu vb.) —
  ayrı görevler.

## Dependencies

- V1-RMD-120

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 uyarı / 0 hata.
- `docker compose -f compose.yaml -f compose.test.yaml up --build test`:
  **ilk koşuda gerçek bir kusur container'ın kendi exit code'uyla
  yakalandı** — eşzamanlılık testi (`ConcurrentIdenticalFirstSubmissionsForATableResolveToTheSameOrder`)
  503 ile başarısız oldu: yeni sipariş dalı `_repository.AddAsync`'i hâlâ
  eski tek-parametreli (kendi ayrı transaction'ını açan) overload'la
  çağırıyordu, bu yüzden eşzamanlı ikinci isteğin benzersizlik ihlali bu
  metodun kendi `connection`/`transaction`'ını hiç etkilemiyordu ve
  `catch` bloğu hiç devreye girmeden ham `PostgresException` 503'e
  düşüyordu; `AddAsync(order, connection, transaction, ct)` overload'una
  geçilince (V1-RMD-120'nin zaten eklediği desen) ikinci koşuda
  **container'ın gerçek exit code'u 0**, 80 test projesi, sıfır başarısız
  — `ALKAROS.Host.Experience.Orders.TableDraft.Tests` 13/13 (yeni
  `RetryingATableDraftAfterTheOrderWasAlreadySubmittedReplaysTheExistingOrderInsteadOfDuplicatingIt`
  ve `ConcurrentIdenticalFirstSubmissionsForATableResolveToTheSameOrder`
  dahil), `ALKAROS.Host.Tests` 121/121 (`ManifestTests`, 75 pozisyon).
- `python tools/consistency-audit/consistency_audit.py`: 13 ihlal, hepsi bu
  görevden önce de vardı, dokunulmayan dosyalarda.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata.
- Migration ileri/geri: `076-*.up.sql` ve `.down.sql` boş bir veritabanında
  denendi (`ALKAROS.Host.Tests`'in migration composition testleri).

## Handoff

- V1-GOV-121
