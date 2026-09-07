# V1-RMD-117 - Deep audit wave 2: reservation row-version gap and release invariant

- Task ID: V1-RMD-117
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: implementation
- Surface state: Existing

## Goal

Düzeltme planının Dalga 1'i (`deep-tables.md`'nin 2 Critical bulgusu):
rezervasyon yaşam döngüsü uçtan uca çalışamaz durumdaydı. `TableReservationResult`
kendi `row_version`'ını hiç döndürmüyordu ve rezervasyonu tek başına okuyacak
bir `GET` uç noktası hiç yoktu — bu yüzden `Claim`/`Cancel`/`Expire` (üçü de
`ExpectedReservationRowVersion > 0` zorunlu kılıyor) hiçbir uyumlu istemci
tarafından ULAŞILAMAZDI. Ayrıca, tek UI-erişilebilir "no-show masayı serbest
bırak" yolu (genel `POST /tables/{id}/status` ile `SetAvailable`)
`table_mgmt.table_reservations` satırına hiç dokunmuyordu — bu da
`CreateReservationAsync`'in kendi "masada zaten aktif rezervasyon var" koruması
yüzünden o masayı SONSUZA DEK yeniden rezervasyona kapatıyordu.

## Owned surface

- `plan/v1/remediation/V1-RMD-117-reservation-row-version-and-release-invariant.md` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik
  iddiası olarak parse etmesin):
  - src/Modules/Tables/Reservations/TableReservationResult.cs,
    PostgresTableReservationRepository.cs, TableReservationService.cs
    (V1-TBL-007 sahipliğinde) — ReservationRowVersion alanı, GetByIdAsync,
    RETURNING row_version.
  - src/Host/Experience/Tables/TableManagementApplication.cs
    (V1-IAM-024 sahipliğinde) — yeni GET /reservations/{id} route'u.
  - src/Host/Experience/Tables/TableManagementStore.cs (V1-RMD-026
    sahipliğinde) — ChangeStatusAsync'in Reserved'den çıkışta rezervasyonu
    serbest bırakması.
  - tests/Modules/Tables/Reservations/PostgresTableReservationTests.cs
    (V1-TBL-007 sahipliğinde), tests/Host/Experience/Tables/TableManagementHttpTests.cs
    (V1-RMD-013 sahipliğinde) — her ikisine bu görevin bulgularına
    karşılık gelen regresyon testleri.

## In scope

1. **`TableReservationResult.ReservationRowVersion` (Critical).**
   `CreateReservationAsync`'in INSERT'i artık `RETURNING row_version` kullanıyor
   ve gerçek değeri sonuca ekliyor (önceden yalnızca `NewTableRowVersion` vardı,
   rezervasyonun kendi versiyonu hiç dışarı çıkmıyordu — hardcoded `1` bir SQL
   literal'i olarak kalıyordu, hiçbir yanıt alanında görünmüyordu).
2. **`GET /reservations/{reservationId}` (Critical, eksik uç nokta).**
   `ITableReservationRepository.GetByIdAsync` zaten vardı ama hiçbir HTTP
   yüzeyine bağlı değildi. Yeni `ITableReservationService.GetByIdAsync` +
   yeni `GET` route + yeni `TableReservationDto` read model (var olan
   `TableReservationResult`/`TableReservationReleaseResult` desenine
   uygun, `RequireReadAsync` ile herhangi bir kimliği doğrulanmış rol
   okuyabilir, `ReservationNotFoundException` zaten 404'e eşlenmişti).
3. **`ChangeStatusAsync`'in Reserved'den çıkışta rezervasyonu serbest bırakması
   (Critical).** `TableManagementStore.ChangeStatusAsync` artık masa
   `Reserved` durumundayken hedef `Available`/`Occupied` ise, önce aktif
   rezervasyonu `ITableReservationRepository.CancelReservationAsync`/
   `ClaimReservationAsync` üzerinden (gerçek, sunucu tarafından okunan
   row version'larla — istemciden hiçbir ek alan istemeden) serbest
   bırakıyor; bu ikisi zaten masa satırını da aynı atomik işlemde
   güncelliyor, bu yüzden eski doğrudan `UpdateStatusAsync` çağrısı yalnız
   Reserved-kaynaklı olmayan geçişlerde çalışıyor. `current.TransitionTo`
   zaten Reserved'i yalnızca Available/Occupied'e izin verecek şekilde
   sınırlıyor, üçüncü bir hedef bu dala hiç girmiyor.
4. Mevcut testlerin (`PostgresTableReservationTests`,
   `TableManagementHttpTests`) kendisi de aynı denetim bulgusunun örneğini
   taşıyordu — `ExpectedReservationRowVersion: 1` hardcoded literal'i,
   gerçek bir istemcinin asla yapamayacağı varsayımın aynısı. Artık
   gerçek `reserveResult.ReservationRowVersion`'ı kullanıyorlar.

## Out of scope

- **PosTerminal'in Claim/Cancel butonlarının gerçekten açılması.**
  `TableWorkspace.tsx:366`'daki "Rezervasyon satır sürümü sunucu yanıtında
  bulunmadığı için işlem güvenli biçimde kapalı" devre dışı bırakma metni
  artık YANLIŞ (backend artık sağlıyor) ama düzeltmek ayrı, karşılaştırılabilir
  büyüklükte bir iş: `TableRecord`/`TableDto`'ya aktif rezervasyon id +
  row version eklemek, `tableApi.ts`'in `execute()`'una `CancelReservation`/
  `ClaimReservation` dallarını eklemek, `isClientExecutableAction`'ı
  güncellemek, yeni FE testleri. Bu görev yalnız backend'in gerçek anlamda
  ulaşılabilir olmasını sağlar; FE kablolaması ayrı bir dalga (1b).
- **Zamanlanmış rezervasyon süresi dolma işi** — `ExpireReservationAsync`
  zaten var ve çalışıyor ama hiçbir zamanlayıcı onu tetiklemiyor; ayrı bir
  operasyonel karar (hangi arka plan iş çatısı, hangi aralık), düzeltme değil.
- **`ITablePointerProjector`'ın rebuild yeteneğinin bir HTTP uç noktasına
  bağlanması** — ayrı, bağımsız bir Medium bulgu, bu Critical çiftiyle
  aynı invariant'ı paylaşmıyor.
- Deep-tables raporunun geri kalan Medium/Low bulguları (POST /tables'ın
  ZoneId hatasını yanlış kodla raporlaması, PUT /tables/{id}'nin durum
  koruması olmayan ölü endpoint'i, çapraz-bölge birleştirmeler,
  `tableApi.ts` Merge fallback'inin her zaman `expectedRowVersion:0`
  göndermesi, Unmerge'in liste görünümünden çalışmaması) — ayrı dalgalara
  bırakıldı.

## Dependencies

- V1-RMD-116

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 uyarı / 0 hata.
- `docker compose -f compose.yaml -f compose.test.yaml run --build --rm test`:
  tüm proje testleri yeşil (yeni/genişletilmiş testler dahil:
  `PostgresTableReservationTests.CreateReservationAndClaimExecutesCleanlyAndProjectsStatus`
  artık gerçek `ReservationRowVersion`'ı doğruluyor ve kullanıyor;
  `TableManagementHttpTests.ReservationTransferMergeAndUnmergeExecuteThroughRealPostgresqlServices`
  yeni `GET /reservations/{id}` çağrısı + gerçek row version'larla genişletildi;
  yeni `ChangingStatusOffAReservedTableReleasesTheReservationAndAllowsReReservation`
  ikinci Critical'ı uçtan uca kanıtlıyor).
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 13 ihlal, hepsi bu
  görevden önce de vardı, dokunulmayan dosyalarda (V1-RMD-116'nın
  kaydettiğiyle birebir aynı).

## Handoff

- V1-GOV-111
