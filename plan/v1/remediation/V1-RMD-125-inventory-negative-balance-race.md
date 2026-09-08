# V1-RMD-125 - Independent audit: Inventory negative-balance TOCTOU race

- Task ID: V1-RMD-125
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız denetimin (2026-09-09, tüm kod tabanı taraması) en kritik bulgusu:
`InventoryAdjustmentService.AdjustInventoryAsync` ve
`WasteRecordingService.RecordWasteAsync`, "olumsuz olmayan bakiye" kuralını
mevcut bakiyeyi okuyup (`GetByItemAndLocationAsync`), kontrol edip, sonra
**ayrı, kilitsiz bir round trip'te** (`IStockBalanceProjector.ApplyMovementAsync`
→ `ApplyOnHandDeltaAsync`) uyguluyordu. İki eşzamanlı azaltma isteği aynı
bayat bakiyeye karşı kontrolü geçip ikisi de uygulanabiliyor, bu da
`inventory.stock_balances.on_hand_quantity`'yi gerçek negatif değere
düşürebiliyordu. Ayrıca `WasteRecordingService`'in üç yazımı (ledger
append + waste record insert + bakiye güncelleme) ortak bir transaction'da
değildi — kısmi hata sonrası kalıcı, silinemez bir yetim ledger satırı
kalabiliyordu; ve idempotency-key unique-violation hiçbir yerde
yakalanmıyordu.

## Owned surface

- `plan/v1/remediation/V1-RMD-125-inventory-negative-balance-race.md` (yeni)
- `database/migrations/V1/V1-RMD-125/**` (yeni)
- `src/Modules/Inventory/Transactions/**` (yeni — `IInventoryTransactionRunner`,
  `PostgresInventoryTransactionRunner`)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır (yollar
  geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası olarak
  parse etmesin):
  - src/Modules/Inventory/BalanceProjection/IStockBalanceRepository.cs,
    PostgresStockBalanceRepository.cs (V11-INV-002 sahipliğinde) — yeni
    `TryApplyGuardedOnHandDeltaAsync` metodu; mevcut hiçbir metot
    değişmedi.
  - src/Modules/Inventory/BalanceProjection/BalanceGuardFailedException.cs
    (yeni dosya, V11-INV-002 sahipliğindeki dizinin altında) — yalnız bu
    görevin kendi transaction orkestrasyonu içinde kullanılan dahili
    kontrol-akışı sinyali; mevcut hiçbir dosyaya dokunulmadı.
  - src/Modules/Inventory/WasteRecording/IWasteRecordRepository.cs,
    PostgresWasteRecordRepository.cs (V11-INV-006 sahipliğinde) — yeni
    `InsertAsync(connection, transaction)` aşırı yüklemesi.
  - src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs
    (V11-INV-005 sahipliğinde) — check-then-act'ten guarded transaction'a
    geçiş.
  - src/Modules/Inventory/WasteRecording/WasteRecordingService.cs
    (V11-INV-006 sahipliğinde) — aynı geçiş + üç yazımın atomikleştirilmesi
    + idempotency-key çakışma yakalama.
  - src/Modules/Inventory/InventoryModule.cs (V1.1 Inventory foundation
    sahipliğinde) — yeni `IInventoryTransactionRunner` DI kaydı.
  - database/MigrationComposition/order.json,
    src/Host/Composition/Migrations/MigrationManifest.cs (`PhaseBMax`),
    tests/Host/MigrationComposition/Manifest/ManifestTests.cs (V1-FND-004
    sahipliğinde) — migration 087 için standart 4 dosyalık desen.
  - tests/Modules/Inventory/{ManualAdjustments,WasteRecording,MovementReversal,
    BalanceProjection,PortionReservations/CancellationEffects}/**'teki
    ilgili Fake* sınıfları ve Database test kurulumları (ilgili görevlerin
    sahipliğinde) — yeni arayüz üyeleri için zorunlu fake implementasyonları
    ve `IInventoryTransactionRunner`'a geçiş; mevcut hiçbir test senaryosu
    değişmedi.
  - compose.test.yaml (paylaşılan test altyapısı) — bu görevin kendi Docker
    doğrulaması sırasında bulunan, **V12-QRO-001'e ait bağımsız bir
    regresyonu** düzeltmek için `ALKAROS_KITCHEN_STATION_ID` eklendi (bkz.
    Acceptance evidence).

## In scope

1. **TOCTOU'yu kapatan atomik guard.** `IStockBalanceRepository.TryApplyGuardedOnHandDeltaAsync`
   — aynı `INSERT ... ON CONFLICT DO UPDATE` upsert deseni, ama UPDATE dalı
   yalnız sonuç `>= 0` olduğunda tetikleniyor (`WHERE` koşulu `EXCLUDED` ile
   mevcut satırın gerçek, o anki değerini karşılaştırıyor — okuma ile yazma
   arasında hiçbir boşluk yok). Guard başarısız olursa `null` döner.
2. **Atomiklik.** `IInventoryTransactionRunner` (yeni, `src/Modules/Inventory/Transactions/**`)
   — ledger append + (waste kaydı için) waste record insert + guarded
   bakiye uygulaması tek transaction'da commit/rollback oluyor. Servisler
   `NpgsqlDataSource`'a değil bu arayüze bağımlı, böylece fake'lerle birim
   testi hâlâ mümkün (gerçek Postgres bağlantısı gerektirmiyor).
3. **DB seviyesinde son savunma hattı.** Migration 087:
   `inventory.stock_balances` üzerinde `AFTER INSERT OR UPDATE` trigger'ı
   (`prevent_negative_on_hand_balance`), `on_hand_quantity < 0` olursa
   `ERRCODE 23514` ile reddediyor. **Önemli bulgu:** düz bir `CHECK`
   kısıtı veya `BEFORE` trigger bu upsert deseniyle uyumsuz — Postgres,
   `INSERT ... ON CONFLICT DO UPDATE`'in CHECK/`BEFORE INSERT` tetikleyicisini
   çatışma UPDATE'e yönlendirilmeden ÖNCE, VALUES cümlesindeki ham (delta)
   değere karşı çalıştırıyor; bu da meşru bir azaltmayı (ör. 10-3=7) salt ham
   delta (-3) negatif olduğu için yanlışlıkla reddediyordu — Docker'a
   gitmeden önce gerçek Postgres'e karşı elle doğrulanıp düzeltildi. `AFTER`
   trigger'ın `NEW`'i her iki dalda da (INSERT ve ON CONFLICT DO UPDATE)
   gerçekten yazılan son değeri yansıtıyor, sorunu ortadan kaldırıyor.
4. **Idempotency-key çakışması.** `WasteRecordingService`, aynı anahtarla
   yarışan eşzamanlı bir isteğin `uq_waste_records_idempotency` unique
   ihlalini artık yakalayıp mevcut kaydı "replay" olarak döndürüyor (önceden
   ham `PostgresException` çağırana sızıyordu).

## Out of scope

- Production/Purchasing modüllerindeki benzer sınıf sorunlar (audit'in ayrı
  bulguları — `PurchaseOrder`'da row_version eksikliği, iki bağımsız parti
  tamamlama yolu) — ayrı bir remediation görevi.
- `IStockBalanceProjector.ApplyMovementAsync`'in kendisi (hâlâ var, başka
  çağıranlar — Production/Purchasing'in mevcut, guard'sız akışları —
  tarafından kullanılıyor; bu görev yalnız ManualAdjustments/WasteRecording'i
  guard'lı yola taşıdı).
- `PortionReservationLifecycleService.CreateReservationAsync`'in bakiye
  kontrolü hiç yapmaması (audit bulgusu #6, ayrı görev — bugün hiçbir
  üretim çağıranı yok).
- Bu görevin Docker doğrulaması sırasında bulunan, kendisiyle ilgisiz bir
  regresyonun kök nedeni (bkz. `compose.test.yaml`'daki Sınırlı ek notu ve
  Acceptance evidence) — `V12-QRO-001`'in kendi `OrdersModule` değişikliği,
  ayrı, önceki bir commit'e ait; bu görev yalnız üretimde kalan yan etkiyi
  (eksik test env var'ı) düzeltti, `V12-QRO-001`'in kendi kapsamına
  girmedi.

## Dependencies

- V11-INV-002
- V11-INV-005
- V11-INV-006

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 Uyarı, 0 Hata.
- Gerçek Postgres'e karşı elle doğrulama (`docker exec ... psql`): guard'lı
  UPSERT'in üç senaryosu da doğrulandı — meşru azaltma (10→7) başarılı,
  aşırı azaltma (7→-13) satırı değiştirmeden bastırılıyor, ilk-kez negatif
  delta (satır yok) trigger tarafından reddediliyor.
- `ALKAROS.Inventory.ManualAdjustments.Tests`: 9/9.
- `ALKAROS.Inventory.WasteRecording.Tests`: 11/11.
- `ALKAROS.Inventory.PortionReservations.CancellationEffects.Tests`: 9/9.
- `ALKAROS.Inventory.BalanceProjection.Tests`: 12/12.
- `ALKAROS.Inventory.MovementReversal.Tests`: 16/16.
- `ALKAROS.Inventory.MovementLedger.Tests`: 18/18.
- `ALKAROS.Inventory.PortionReservations.Concurrency.Tests`: 9/9.
- `ALKAROS.Inventory.PortionReservations.Lifecycle.Tests`: 9/9.
- `ALKAROS.Inventory.ReservationBalanceProjection.Tests`: 13/13.
- `ALKAROS.Inventory.StockMaster.Tests`: 14/14.
- `ALKAROS.Host.Tests` (Manifest + HostModuleReachability): 19/19.
- `docker compose -f compose.yaml -f compose.test.yaml run --build --rm test`
  (boru hattı olmadan çalıştırılıp gerçek `$?` yakalanarak — `| tail` üzerinden
  kontrol etmenin, konteyner `--rm` ile kaldırıldığı için yanıltıcı/eski bir
  konteynerin durumunu gösterebildiği bu görev sırasında keşfedildi): 89 test
  DLL'inin tamamı yeşil, sıfır "Failed: N>0" satırı, gerçek çıkış kodu `0`.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 13 önceden var olan
  ihlal (değişmedi, biri bu göreve ait dosyada ama düzenlenmemiş satırda),
  yeni ihlal yok.

## Handoff

- None
