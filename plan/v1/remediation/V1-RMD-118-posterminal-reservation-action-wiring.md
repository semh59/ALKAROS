# V1-RMD-118 - Deep audit wave 2b: PosTerminal Claim/Cancel reservation wiring

- Task ID: V1-RMD-118
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: implementation
- Surface state: Existing

## Goal

`V1-RMD-117` (Dalga 1) yalnız backend'in Claim/Cancel/Expire için gerçekten
ulaşılabilir olmasını sağladı ve kendi Out of scope bölümünde bunu açıkça not
etti: PosTerminal'in arayüzü hâlâ bu iki butonu kalıcı olarak kapalı
tutuyordu, çünkü ne `TableRecord` (liste/detay görünümü) ne de
`tableApi.ts`'in `execute()`'u rezervasyonun kendi id/row version'ını hiç
taşımıyordu — sunucu artık veriyi sağlasa da istemcinin ona erişecek hiçbir
yolu yoktu. Semih onayıyla ("sıradan devam önce 1b"), bu görev o boşluğu
kapatır: `TableDto`/`TableRecord`'a aktif rezervasyon bilgisini ekler ve
`tableApi.ts` + `TableWorkspace.tsx` + `FloorPlanWorkspace.tsx`'i gerçekten
çağıracak şekilde bağlar.

## Owned surface

- `plan/v1/remediation/V1-RMD-118-posterminal-reservation-action-wiring.md` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik
  iddiası olarak parse etmesin):
  - src/Host/Experience/Tables/TableManagementContracts.cs, TableManagementApplication.cs
    (V1-IAM-024 sahipliğinde) — TableDto.ActiveReservationId/ReservationRowVersion,
    ToDto'nun opsiyonel activeReservation parametresi, GET /tables + GET
    /tables/{id} + POST /tables/{id}/status'ün bunu doldurması.
  - src/Clients/PosTerminal/src/features/tables/models.ts, tableApi.ts,
    TableWorkspace.tsx, FloorPlanWorkspace.tsx (V1-CUI-006 sahipliğinde) —
    TableRecord'a aynı iki alan, execute()'un ClaimReservation/
    CancelReservation dalları, isClientExecutableAction'ın artık
    activeReservationId'yi kontrol etmesi.
  - tests/Host/Experience/Tables/TableManagementHttpTests.cs (V1-RMD-013
    sahipliğinde), src/Clients/PosTerminal/src/features/tables/{tableApi,TableWorkspace,FloorPlanWorkspace}.test.tsx
    (V1-CUI-006 sahipliğinde) — bu görevin bulgusuna karşılık gelen
    regresyon testleri.

## In scope

1. **`TableDto`/`TableContractMapper.ToDto` (backend).** Yeni
   `ActiveReservationId`/`ReservationRowVersion` alanları eklendi (ikisi de
   `null` olabilir); `ToDto` artık opsiyonel bir `TableReservationRecord?`
   parametresi alıyor. `GET /tables`, `GET /tables/{tableId}` ve
   `POST /tables/{tableId}/status` artık masa `Reserved` durumundayken
   (`ITableReservationRepository.GetActiveByTableIdAsync` ile, yalnız bu
   durumda — invariant zaten `V1-RMD-117`'de kurulduğu için diğer
   durumlarda hiç sorgu çalışmıyor) bu alanları dolduruyor. `POST /tables`
   ve `PUT /tables/{tableId}` bilerek dokunulmadı — hiçbiri hiçbir zaman
   Reserved bir masa üretmiyor/beklemiyor.
2. **`TableRecord`/`tableApi.ts`/`TableWorkspace.tsx`/`FloorPlanWorkspace.tsx`
   (frontend).** `TableRecord`'a aynı iki alan eklendi.
   `isClientExecutableAction(action, table?)` artık Claim/Cancel'ı yalnızca
   `table.activeReservationId` doluyken yürütülebilir sayıyor (savunma
   amaçlı — sunucu zaten yalnız Reserved+Active-rezervasyon'da bu komutları
   listeliyor). `tableApi.ts`'in `execute()`'una gerçek
   `POST /reservations/{id}/claim` ve `.../cancel` çağrılarını yapan iki
   yeni dal eklendi (`reservationId`/`rowVersion`'ı `table`'ın kendisinden
   okuyor, hiçbir yeni form alanı gerekmiyor — Cancel zaten `actionNeedsReason`
   listesinde). Rezervasyon id'si hiç yoksa (veri tutarsızlığı) sunucuya
   hiç gitmeden `RESERVATION_MISSING` ile başarısız oluyor.

## Out of scope

- **Zamanlanmış rezervasyon süresi dolma işi ve `ITablePointerProjector`
  rebuild uç noktası** — `V1-RMD-117`'nin kendi Out of scope bölümünde
  zaten aynı gerekçeyle bırakıldı.
- **Genel status-change endpoint'inin bir masayı `Reserved`'e DOĞRUDAN
  (gerçek bir rezervasyon satırı olmadan) geçirebilmesi** — bu görevi
  doğrularken fark edilen, `Table.CanTransitionTo`'nun izin verdiği ayrı
  bir olası tutarsızlık (denetim raporunda yer almıyordu); bu görevin
  enrichment mantığı bu durumda güvenle `null` döndürüyor (yanlış veri
  üretmiyor), ama kök neden düzeltilmedi — ayrı bir karar/bulgu olarak not
  edildi.
- Deep-tables raporunun geri kalan Medium/Low bulguları (POST /tables'ın
  ZoneId hatasını yanlış kodla raporlaması, PUT /tables/{id}'nin durum
  koruması olmayan ölü endpoint'i, çapraz-bölge birleştirmeler, `tableApi.ts`
  Merge fallback'inin her zaman `expectedRowVersion:0` göndermesi, Unmerge'in
  liste görünümünden çalışmaması) — ayrı dalgalara bırakıldı.

## Dependencies

- V1-RMD-117

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 uyarı / 0 hata.
- `docker compose -f compose.yaml -f compose.test.yaml run --build --rm test`:
  tüm proje testleri yeşil (`TableManagementHttpTests` yeni
  `GET /tables`/`GET /tables/{id}` reservation-alan doğrulamaları ve
  `ChangingStatusOffAReservedTableReleasesTheReservationAndAllowsReReservation`'ın
  genişletilmiş null-alan iddiaları dahil).
- `npx tsc --noEmit` (PosTerminal): sıfır hata.
- `npx vitest run` (PosTerminal): 19 dosya, 121 test, hepsi geçti (yeni
  `claims and cancels a reservation using the table's own reservation
  id/row version` ve `refuses to claim/cancel when the table carries no
  active reservation id` dahil).
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata.
- `python tools/consistency-audit/consistency_audit.py`: 13 ihlal, hepsi bu
  görevden önce de vardı, dokunulmayan dosyalarda.

## Handoff

- V1-GOV-113
