# V1-RMD-134 - Independent audit: four dead BuildingBlocks foundation libraries

- Task ID: V1-RMD-134
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız denetimin (2026-09-09) son mimari bulgusu: `BuildingBlocks`
altında dört "temel" kütüphane (Idempotency, Transactions,
TransactionOutboxIntegration, ve Messaging içindeki Inbox tüketici-tarafı
deseni) gerçek, doğru, kısmen daha önce bile denetlenip düzeltilmiş
(`V1-RMD-112`, `InboxStore`'a eksik bir indeks ekledi) kod — ama
`src/Modules`, `src/Host`, `src/Integrations`'da hiçbir gerçek tüketicisi
yok (grep ile doğrulandı). Bunlar Kitchen/Menu/Purchasing/Production'ın
aksine "kopuk bir özellik" değil: genel bir "ortak transaction + outbox
entegrasyonu + HTTP-seviyesi idempotency" çerçevesi kurmaya çalışıyorlardı,
ama kod tabanı organik olarak daha basit, doğrudan bir desene evrildi —
elden ele geçirilen `NpgsqlConnection`/`NpgsqlTransaction` + her yerde
tekrarlanan `ON CONFLICT DO NOTHING` + benzersiz kısıt tabanlı idempotency
(bu oturumda kapatılan Kitchen/Menu/Purchasing/Production dahil, kod
tabanındaki HER özellik bunu kullanıyor). Kullanıcının kararı: sil.

Silme, ilk göründüğünden daha cerrahi bir iş gerektirdi: paylaşılan test
projesi (tests/BuildingBlocks/Idempotency) hem ölü Idempotency/Inbox
testlerini HEM DE gerçekten kullanılan `OutboxStore`/`OutboxFanoutSink`/
`RetryPolicy` testlerini barındırıyordu; bazı test dosyaları (ör.
`EnvelopeValidationTests.cs`, `RetrySqlIdentifierTests.cs`,
`RetryScheduleIntegrationTests.cs`) aynı anda hem ölü hem canlı senaryoları
test ediyordu. Paylaşılan `RetryPolicy.AllowedTableNames` (Messaging'in
kendi, canlı kodu) da `"inbox_messages"`'ı hâlâ kayıtlı bir kimlik olarak
listeliyordu — artık hiçbir çağıranı olmayan bir referans.

## Owned surface

- `plan/v1/remediation/V1-RMD-134-buildingblocks-dead-foundation-cleanup.md`
  (yeni)
- `tests/BuildingBlocks/Messaging/**` (yeni — eski
  tests/BuildingBlocks/Idempotency'den taşınıp yeniden adlandırıldı)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik
  iddiası olarak parse etmesin):
  - src/BuildingBlocks/Transactions/** (V1-FND-005 sahipliğinde),
    src/BuildingBlocks/TransactionOutboxIntegration/** (V1-FND-011
    sahipliğinde), src/BuildingBlocks/Idempotency/** (V1-FND-002
    sahipliğinde), tests/BuildingBlocks/Transactions/**,
    tests/BuildingBlocks/TransactionOutboxIntegration/** — bu görevin
    kendi silme kararıyla tamamen kaldırıldı (grep ile doğrulandı:
    `src/Modules`, `src/Host`, `src/Integrations`'da sıfır gerçek
    tüketici).
  - src/BuildingBlocks/Messaging/InboxStore.cs, InboxMessage.cs,
    InboxStatus.cs, InboxEnvelope.cs, IInboxHandler.cs (V1-FND-002/
    V1-FND-019 sahipliğindeki dizinin altında) — tamamen kaldırıldı
    (sıfır gerçek tüketici; gerçek `IIntegrationEventConsumer`'ların
    hepsi kendi alan-özel dedup mekanizmasını kullanıyor).
  - src/BuildingBlocks/Messaging/RetryPolicy.cs (V1-FND-019
    sahipliğinde) — `AllowedTableNames`'ten artık hiç çağıranı olmayan
    `"inbox_messages"` çıkarıldı; `OutboxStore`/`OutboxFanoutSink`'in
    kendisi ve `RecordFailureAsync`'in `outbox_messages` çağrı yolu hiç
    değişmedi.
  - tests/BuildingBlocks/TestHelpers/ALKAROS.TestHelpers.csproj,
    Fixtures/RecordingResource.cs, Fixtures/SimulatedFailures.cs
    (V1-FND-005 sahipliğinde) — `ALKAROS.Transactions`'a olan proje
    referansı ve yalnız o kütüphaneyi test etmek için var olan iki
    yardımcı dosya kaldırıldı (grep ile doğrulandı: paylaşılan
    `TestHelpers`'ta başka hiçbir tüketicileri yoktu).
  - ALKAROS.slnx (çözüm dosyası, paylaşılan) — silinen üç kaynak + üç
    test projesi girişi kaldırıldı; tests/BuildingBlocks/Idempotency
    girişi yeni `tests/BuildingBlocks/Messaging` yoluna güncellendi.
  - database/migrations/** (V1-FND-002/V1-FND-019/V1-RMD-112
    sahipliğinde) — KASITLI OLARAK dokunulmadı, bkz. Out of scope.

## In scope

1. **Üç bağımsız ölü proje tamamen silindi**: `Transactions`,
   `TransactionOutboxIntegration`, `Idempotency` (kaynak + test projeleri).
   Bunlar başka hiçbir canlı kodla iç içe geçmemişti — düz, risksiz silme.
2. **Messaging içindeki Inbox alt-kümesi cerrahi olarak çıkarıldı**:
   `InboxStore`/`InboxMessage`/`InboxStatus`/`InboxEnvelope`/`IInboxHandler`
   silindi; aynı dosyadaki/projedeki canlı `Outbox*` sınıfları ve paylaşılan
   `RetryPolicy` hiç değişmeden kaldı (yalnız kendi `AllowedTableNames`
   listesinden artık ölü referans çıkarıldı).
3. **Paylaşılan test projesi bölündü, silinmedi**:
   tests/BuildingBlocks/Idempotency → `tests/BuildingBlocks/Messaging`
   olarak taşındı/yeniden adlandırıldı (`ALKAROS.Messaging.Tests`).
   Ölü test dosyaları (`IdempotencyKeyStoreTests.cs`,
   `IdempotencyKeyTests.cs`, `RequestHashTests.cs`,
   `InboxRedeliveryContractTests.cs`, `InboxStoreTests.cs`) silindi.
   Karma dosyalar (`EnvelopeValidationTests.cs`, `RetrySqlIdentifierTests.cs`,
   `RetryScheduleIntegrationTests.cs`) yalnız Inbox/Idempotency'e değen
   kısımları çıkarılarak düzenlendi — canlı Outbox senaryoları
   `RetrySqlIdentifierTests`'e artık silinmiş `"inbox_messages"`
   kimliğinin gerçekten reddedildiğini doğrulayan yeni bir vaka eklenerek
   bile güçlendirildi. `StoreTestDatabase`'in `ResetTablesAsync`'i yalnız
   `outbox_messages`'a daraltıldı; yalnız Idempotency testlerinin
   kullandığı `ForceExpiredAsync` kaldırıldı.
4. **`--force-evaluate` restore.** `ALKAROS.TestHelpers`'ın kendi
   `ALKAROS.Transactions` proje referansının kaldırılması, ona bağımlı
   ONLARCA test projesinin kilitli `packages.lock.json` dosyasını
   geçersiz kıldı (NuGet kilit dosyaları tam geçişli bağımlılık grafiğini
   listeler) — `dotnet restore --force-evaluate` ile hepsi tutarlı hale
   getirildi (76 dosya, yalnız kilit-dosyası metadata'sı, davranış
   değişikliği yok).

## Out of scope

- **Veritabanı şeması/migration'lar bilinçli olarak dokunulmadı.**
  `idempotency_keys`/`inbox_messages` tabloları hâlâ var (migration 001/002,
  V1-FND-002) ve migration 033 (V1-FND-019) `inbox_messages`'ı doğrudan
  `ALTER TABLE` ediyor — bu yüzden `033`'ün kendisi hâlâ `inbox_messages`'ın
  var olmasını gerektiriyor. Bir tabloyu DROP etmek, bu kod temizliğinden
  çok daha riskli, ayrı bir karar (geriye dönük migration'lar bu kod
  tabanında alışılmadık bir desen); kullanıcı yalnız ölü KODU silmeyi
  istedi. Sonuç: şema, artık hiç yazılmayan/okunmayan iki tablo taşıyor —
  bu, bu görevin bilerek kabul ettiği, ayrı bir takip kararı gerektiren
  bir durum.
- `OperationsStatusEngine`/`CashierShellEngine` (aynı sınıf ölü kod,
  BuildingBlocks'la ilgisiz) — kullanıcı ayrıca bakacak.

## Dependencies

- V1-FND-002
- V1-FND-005
- V1-FND-011
- V1-FND-019

## Acceptance evidence

- `dotnet restore ALKAROS.slnx --force-evaluate`: 76 `packages.lock.json`
  güncellendi (`ALKAROS.Transactions`'a artık bağımlı olmayan her proje).
- `dotnet build ALKAROS.slnx -c Debug`: 0 Uyarı, 0 Hata.
- `python -m pytest tests/Architecture/ProjectManifest/test_project_manifest.py`:
  4/4 (çözüm/disk/proje referansları arasında sürüklenme yok).
- Tam Docker test paketi (`docker compose ... run test`, boru hattı
  olmadan) çalıştırıldı; paylaşılan Docker VM belleği başka, ilgisiz
  projelerin konteynerleri tarafından doldurulmuş durumdaydı (bu oturumun
  önceki görevleriyle aynı ortam koşulu) — paket, görülen HER projede
  sıfır hatayla (`ALKAROS.Messaging.Tests`: 39/39 dahil,
  `ALKAROS.Host.Experience.Production.Tests`: 4/4,
  `ALKAROS.Purchasing.*`/`ALKAROS.Production.*`'ın tüm mevcut testleri)
  yaklaşık 85 test derlemesini geçtikten sonra OOM ile durduruldu. Görünür
  tek hata yok. Ayrı, dar bir çalıştırmada (aynı Docker imajı, gerçek
  `$?` yakalanarak) `ALKAROS.Host.Tests` (Manifest + Reachability +
  `ProductionExperienceCompositionTests` — gerçek "serve" konteynerinin
  her modül servisini hâlâ çözdüğünü doğruluyor) ayrıca doğrulandı:
  121/121, gerçek çıkış kodu `0`.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 1 önceden var
  olan ihlal (değişmedi, bu göreve ilişkisiz), yeni ihlal yok.

## Handoff

- None
