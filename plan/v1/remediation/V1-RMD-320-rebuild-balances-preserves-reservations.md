# V1-RMD-320 - `RebuildAllBalancesAsync` aktif rezervasyonları sessizce sıfırlıyordu

- Task ID: V1-RMD-320
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) K8 bulgusu: `StockBalanceProjector.RebuildAllBalancesAsync` önce `inventory.stock_balances` tablosunun TAMAMINI `DELETE` ediyor, sonra hareket defterinden yeniden inşa ediyordu — ama `SetExactBalanceAsync`'in kendi `INSERT`'i her zaman taze bir satır için `reserved_quantity`'yi 0'a varsayıyordu (silinen tablo yüzünden asla `ON CONFLICT` dalına düşmüyor). Sonuç: her rebuild çağrısı her aktif rezervasyonu sessizce sıfırlıyordu. Bu metot şu an hiçbir HTTP endpoint/hosted service'ten çağrılmıyor (ölü kod), ama ileride bir bakım aracına bağlanırsa aktif rezervasyonlar "müsait" görünmeye başlar → gerçek bir çift satış riski.

## Owned surface

- `plan/v1/remediation/V1-RMD-320-rebuild-balances-preserves-reservations.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Inventory/BalanceProjection/IStockBalanceRepository.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Inventory/BalanceProjection/PostgresStockBalanceRepository.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Inventory/BalanceProjection/StockBalanceProjector.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Inventory/BalanceProjection/StockBalanceDatabaseTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Inventory/BalanceProjection/StockBalanceDomainTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Inventory/ManualAdjustments/ManualAdjustmentDomainTests.cs
  (yalnız yeni arayüz üyelerinin sahte/in-memory implementasyonu)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Inventory/MovementReversal/StockMovementReversalDomainTests.cs
  (yalnız yeni arayüz üyelerinin sahte/in-memory implementasyonu)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Inventory/WasteRecording/WasteRecordingDomainTests.cs
  (yalnız yeni arayüz üyelerinin sahte/in-memory implementasyonu)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Inventory/PortionReservations/CancellationEffects/PortionReservationCancellationEffectsDomainTests.cs
  (yalnız yeni arayüz üyelerinin sahte/in-memory implementasyonu)

## In scope

1. `IStockBalanceRepository`'ye iki yeni metot: `GetAllReservedQuantitiesAsync` (sıfırlamadan ÖNCE mevcut tüm rezervasyonları okur) ve `RestoreReservedQuantityAsync` (yeniden inşa sonrası geri yükler, gerekirse taze bir satır ekler).
2. `RebuildAllBalancesAsync`: sıfırlamadan önce rezervasyonları yakalar, hareket defterinden yeniden inşadan SONRA geri yükler — hem yeniden inşanın dokunduğu çiftler hem de hiç hareketi olmayıp yalnız rezervasyonu olan çiftler için.
3. Cross-module bağımlılık (döngüsel referans riski) önlenir: bu fix `ALKAROS.Inventory.ReservationBalanceProjection`'a hiç dokunmaz, tamamen `IStockBalanceRepository`'nin kendi `inventory.stock_balances` tablosu sınırları içinde kalır.

## Out of scope

- `ReservationBalanceProjector.RebuildReservationBalancesAsync`'in (rezervasyonların KENDİ, ayrı, doğru rebuild'i) bu metotla otomatik zincirlenmesi — modüller arası döngüsel bağımlılık riski, ayrı bir orkestratör katmanı gerektirir.
- `RebuildAllBalancesAsync`'in gerçekten bir HTTP endpoint'e bağlanması — bu görevin bulgusu yalnız metodun kendi doğruluğunu kapsıyor, ölü kodun canlandırılması ayrı bir karar.

## Dependencies

- None

## Acceptance evidence

Host/Modules testleri (UTF8 Postgres 18), gerçek bir veritabanına karşı: yeni test `RebuildAllBalancesPreservesAnActiveReservationInsteadOfSilentlyZeroingIt` — gerçek bir satın alma hareketi kaydedilip projekte edilir, `reserved_quantity` doğrudan SQL ile 6'ya ayarlanır (aktif bir rezervasyonu simüle eder), `RebuildAllBalancesAsync` çağrılır; sonuç `OnHandQuantity=20` (yeniden inşa hâlâ doğru çalışıyor), `ReservedQuantity=6` (sıfırlanmadı), `AvailableQuantity=14`. `ALKAROS.Inventory.BalanceProjection.Tests` 13/13 (1 yeni). Beş ayrı sahte repository dosyası yeni arayüz üyelerini uyguladı, ilgili beş test paketi de regresyonsuz yeşil (12, 9, 16, 11, 9 test).

Mutasyon kontrolü: `RebuildAllBalancesAsync`'in geri yükleme adımı geçici olarak kaldırıldı — yeni test gerçekten kırmızı oldu (`Expected: 6, found: 0`); dosya `diff` ile birebir orijinaline geri getirildi, tüm paket tekrar yeşil.

## Handoff

- None
