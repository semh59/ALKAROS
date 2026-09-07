# V12-NFC-001 - Implement NFC self-check-in and trusted order intake

- Task ID: V12-NFC-001
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-09-07

## Goal

Bir masanın sabit NFC/yerel bağlantısına yapılan bir dokunuşu, restoranın
yerel ağı üzerinden, garson onayı beklemeden doğrudan Accepted bir Order'a
dönüştürmek; masa Available ise ilk siparişle birlikte kendiliğinden
Occupied yapmak (self-check-in).

## Owned surface

- `src/Host/Experience/NfcOrdering/**` (yeni)
- `tests/Host/Experience/NfcOrdering/**` (yeni)
- `database/migrations/V12/V12-NFC-001/**` (yeni)
- `ALKAROS.slnx` (yalnız yeni test projesi kaydı eklendi).
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası
  olarak parse etmesin):
  - src/Modules/Orders/OrderAggregate/OrderEnums.cs (V1-RMD-064 sahipliğinde)
    — OrderSource'a yalnız yeni Nfc değeri eklendi.
  - database/MigrationComposition/order.json, src/Host/Composition/Migrations/MigrationManifest.cs,
    tests/Host/MigrationComposition/Manifest/ManifestTests.cs (V1-FND-004/V1-IAM-025 sahipliğinde)
    — yalnız migration 077 kaydı eklendi; V1-RMD-123'ün 076 kaydına dokunulmadı.
  - src/Host/DualScreen/DualScreenApplication.cs (V1-IAM-024 sahipliğinde)
    — yalnız AddNfcOrderingExperience/MapNfcOrderingApi çağrıları ve rate
    limiter'a bir nfc-order policy'si eklendi.
  - tools/consistency-audit/consistency_audit.py, docs/CONSISTENCY_AUDIT.md
    (V1-RMD-122 sahipliğinde) — HOST_AREA_SCHEMA/HOST_AREA_EXTRA_SCHEMAS'e
    yalnız Experience/NfcOrdering satırı eklendi.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Table lookup by id, `Available → Occupied` self-check-in (mevcut
  `IOrderRepository`/`Order` aggregate ile aynı transaction'da, tablo
  durumu `OrderManagementStore`'daki gibi tek atomik güncellemeyle),
  Order oluşturma (`OrderSource.Nfc`), mevcut `SubmitOrderHandler` ile
  `Draft → Submitted` + mutfak bileti dispatch'i, ardından
  `Submitted → PendingConfirmation → Accepted` zincirinin (mevcut
  `Order.TransitionTo`, yeni bir geçiş kuralı icat edilmeden) NFC'nin
  güvenilir kanal olması nedeniyle beklemeden art arda uygulanması,
  submission-id tabanlı idempotency (`V1-RMD-123`'teki
  `ux_orders_table_submission` deseniyle aynı).

## Out of scope

- Public/relay erişimi — yalnız restoranın yerel ağı (LAN) üzerinden
  çalışır; QR'ın public-relay yolu ayrıdır (`V12-QRT-*`/`V12-QRS-*`).
- NFC etiketi donanımı/yazımı (işletme kurulum adımı, kod değil).
- Yaş kısıtlı ürün onay kuyruğu — Catalog modülünde bugün
  `is_age_restricted` gibi bir alan yok; bu istisna ayrı bir Catalog
  şema görevi + `V12-NFC-002` çiftini gerektirir, bu görevin kapsamı
  dışındadır.
- QR kanalının kendisi (`V12-QRO-*`, `V12-QRS-*`) — değişmedi.

## Dependencies

- V1-ORD-001
- V1-ORD-002
- V1-TBL-001

## Deliverables

- `src/Host/Experience/NfcOrdering/**` altında Goal kapsamını uygulayan
  production code ve task-specific automated test assets.
- Başarı, idempotent-replay ve masa-durumu-invariant testleri.

## Acceptance evidence

- Semih'in elle deneyebileceği senaryo: Available bir masanın NFC
  bağlantısına gidilir, bir sepetle sipariş gönderilir → masa Occupied
  olur, sipariş Accepted olarak oluşur, mutfak bileti mevcut Order/Kitchen
  entegrasyonuyla gerçekten oluşur.
- Aynı bağlantıya aynı submission id ile tekrar gidilirse (çift
  tıklama/sayfa yenileme) ikinci bir sipariş oluşmaz, mevcut sipariş/masa
  durumu döner.
- Occupied bir masaya NFC bağlantısıyla tekrar gidilirse (ör. ikinci bir
  sipariş turu), masa için **yeni, ayrı** bir Order oluşturulur — mevcut
  Order zaten `Draft` durumunda değildir (`Order.AddItem` yalnız `Draft`
  kabul eder, NFC siparişleri neredeyse anında `Accepted`e geçtiği için
  bir sonraki turun ona kalem olarak eklenmesi domain kuralıyla mümkün
  değildir); masa durumu tekrar değişmez (zaten Occupied).
- Reserved/Cleaning/OutOfService bir masada (ör. manuel rezervasyon veya
  bekleyen bir QR siparişi) NFC ile doğrudan sipariş oluşturulmaz — açık
  bir ret döner ("garsonu çağırın"); bu durumlarda masanın zaten bir açık
  Order'ı olacağı varsayılmaz (manuel rezervasyonun hiç Order'ı yoktur).
- `dotnet build ALKAROS.slnx -c Debug`: 0 uyarı / 0 hata.
- `docker compose -f compose.yaml -f compose.test.yaml up --build test`:
  `docker inspect alkaros-test-1 --format '{{.State.ExitCode}}'` ile gerçek
  container çıkış kodu `0`; `ALKAROS.Host.Experience.NfcOrdering.Tests.dll`
  8/8 test geçti (self-check-in+Accepted+mutfak bileti, aynı submission id
  ile replay, eşzamanlı ilk-gönderim yarışı, Occupied masada ikinci/ayrı
  Order, Reserved masada ret, bilinmeyen masa/ürün/boş sepet ret).
- Revert-and-confirm ile bulunan gerçek hata: ilk uygulamada
  `Order.TransitionTo`'nun row version'ı bellek nesnesinde değiştirmediği
  (yalnız repository commit'i değiştiriyor) gözden kaçırıldığı için ikinci
  zincirleme geçiş (`PendingConfirmation → Accepted`) her zaman stale-version
  hatasına düşüp sessizce `PendingConfirmation`da kalıyordu — iki gerçek test
  bunu real Postgres'e karşı yakaladı (`Expected: "Accepted", Actual:
  "PendingConfirmation"`); düzeltme `TryTransitionAsync`'in her adımdan sonra
  `GetByIdAsync` ile güncel satırı yeniden okumasıdır.
- `python tools/consistency-audit/consistency_audit.py`: 13 ihlal, hepsi bu
  görevden önce de vardı (yalnızca `HOST_AREA_SCHEMA`/`HOST_AREA_EXTRA_SCHEMAS`e
  `Experience/NfcOrdering` satırı eklendi, yeni ihlal yok).
- `python tools/plan-audit/plan_audit_tool.py validate`: sıfır hata, sıfır
  uyarı.

## Handoff

- V12-NFC-002
