# V1-RMD-126 - Independent audit: Tables Reserved-status permission bypass

- Task ID: V1-RMD-126
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız denetimin (2026-09-09) bir başka kritik bulgusu: genel masa durumu
değiştirme uç noktası (`POST .../table-management/tables/{tableId}/status`)
yalnız `tables.status` iznini gerektiriyordu, ama `Table.CanTransitionTo`'nun
izin verdiği hedeflerden biri `Reserved`. Bu yüzden bu genel uç nokta ile
herhangi bir masa, `tables.reserve` iznine hiç bakılmadan `Reserved`
durumuna geçirilebiliyordu — hem yetki atlaması hem de veri bütünlüğü sorunu:
bu, `table_mgmt.table_reservations`'a hiçbir satır eklemeden çıplak bir
tablo satırı güncellemesiydi, oysa sistemin her yerinde (V1-RMD-117/118
dahil) "Reserved ise mutlaka Active bir rezervasyon satırı vardır"
değişmezi varsayılıyor. Kaynaksız bu Reserved durumu, `CreateReservationAsync`
tarafından hiç fark edilmeyecek (aktif rezervasyon kontrolü yalnız
`table_mgmt.table_reservations`'a bakıyor) ama tablo zaten "Available" değil
diye yeni bir gerçek rezervasyon da reddedilecekti — masa, gerçek rezervasyon
uç noktalarının hiçbiriyle asla kurtarılamayan bir duruma kilitleniyordu.

## Owned surface

- `plan/v1/remediation/V1-RMD-126-tables-reserved-status-bypass.md` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır (yollar
  geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası olarak
  parse etmesin):
  - src/Host/Experience/Tables/TableManagementStore.cs (V1-RMD-117/118
    sahipliğinde) — `ChangeStatusAsync`'e hedef `Reserved` olduğunda erken
    reddeden bir guard eklendi; mevcut Reserved'den ÇIKIŞ yolu (V1-RMD-118'in
    kendi düzeltmesi) hiç değişmedi.
  - tests/Host/Experience/Tables/TableManagementHttpTests.cs (aynı
    sahiplikte) — yeni `ChangingStatusToReservedThroughTheGenericEndpointIsRejected`
    testi; mevcut hiçbir test senaryosu değişmedi.

## In scope

1. `TableManagementStore.ChangeStatusAsync`, `request.Status` "Reserved"'e
   çözümlendiğinde artık `TableManagementConflictException` (409
   `DOMAIN_CONFLICT`) fırlatıyor — mevcut durumu veya satır versiyonunu hiç
   okumadan, en ucuz noktada. Bir masayı Reserved yapmanın tek geçerli yolu
   `POST .../reservations` (zaten `tables.reserve` iznini doğru şekilde
   zorunlu kılıyor ve rezervasyon kaydını tablo durum değişikliğiyle aynı
   transaction'da atomik olarak oluşturuyor).
2. `Table.CanTransitionTo`/`TransitionTo` (domain seviyesi geçiş matrisi,
   `src/Modules/Tables/TableLifecycle/Table.cs`) kasıtlı olarak
   değiştirilmedi — bu, masa yaşam döngüsünün genel, soyut modelidir;
   sorun bu genel uç noktanın hangi hedefleri izin kontrolü olmadan kabul
   ettiğiydi, modelin kendisi değil.
3. Doğrulandı: PosTerminal istemcisi (`src/Clients/PosTerminal/src/features/tables/tableApi.ts`)
   bu uç noktaya asla `status: "Reserved"` göndermiyor — yalnız
   `SetOccupied/SetAvailable/SetCleaning/SetOutOfService` eylemleri bu yola
   gidiyor, "Reserve" eylemi zaten doğrudan `POST .../reservations`'a
   gidiyor. Bu düzeltme istemci tarafında hiçbir değişiklik gerektirmiyor.

## Out of scope

- Reserved durumundan ÇIKIŞ (`Available`/`Occupied`'a geçiş) — V1-RMD-118'in
  zaten düzelttiği, rezervasyonu atomik olarak serbest bırakan yol; bu
  görev onu hiç değiştirmedi (bkz. mevcut
  `ChangingStatusOffAReservedTableReleasesTheReservationAndAllowsReReservation`
  testi, değişmeden geçiyor).
- Audit'in aynı ailedeki diğer bulguları (#8 Orders ham İngilizce hata
  mesajları, #9 `is_available` kontrolü, #11-13 WaiterPwa alan uyuşmazlıkları)
  — ayrı remediation görevleri.

## Dependencies

- V1-RMD-117
- V1-RMD-118

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: tüm çözüm için 0 Uyarı, 0 Hata.
- Bu görev sırasında paylaşılan Docker VM belleği başka, ilgisiz projelerin
  konteynerleri tarafından doldurulmuş durumdaydı; tam 89-DLL'lik
  `docker compose ... run --build --rm test` (tüm `ALKAROS.slnx`) denemesi
  bu yüzden OOM ile öldürüldü (Sınırlı ek dışı, bu göreve özgü bir ortam
  koşulu — kodun kendisiyle ilgisi yok). Bunun yerine etkilenen test
  projesi tek başına, gerçek Postgres'e karşı, aynı Docker imajıyla
  çalıştırıldı (boru hattı olmadan, gerçek `$?` yakalanarak):
  `docker compose -f compose.yaml -f compose.test.yaml run --build --rm test
  dotnet test tests/Host/Experience/Tables/ALKAROS.Host.Experience.Tables.Tests.csproj -c Release`
  → `ALKAROS.Host.Experience.Tables.Tests`: 10/10 (yeni
  `ChangingStatusToReservedThroughTheGenericEndpointIsRejected` ve mevcut
  `ChangingStatusOffAReservedTableReleasesTheReservationAndAllowsReReservation`
  dahil), gerçek çıkış kodu `0`.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 13 önceden var olan
  ihlal (değişmedi), yeni ihlal yok.

## Handoff

- None
