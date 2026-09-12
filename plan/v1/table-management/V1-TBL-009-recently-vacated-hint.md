# V1-TBL-009 - Masa temizliği için pasif "az önce boşaldı" ipucu

- Task ID: V1-TBL-009
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in sorusu ve talimatıyla ("Birde ben masa temizlik hazır gibi
durumları nasıl belirliyorum kullanıcı olarak" → "Pratik bişi bulalım iş
yükü artmış", 2026-09-12): personelin bir masanın temizlik/hazır
durumunu, iş yükünü ARTIRMADAN (ekstra tıklama/adım eklemeden) takip
edebilmesi.

**Mevcut durum incelendi:** PosTerminal'in Masalar ekranında bir masa
`Dolu`yken hesap kapanınca personel doğrudan **"Müsait yap"**a basıyor —
aradan bir "temizliğe al" adımı yok (`TableManagementContracts.
AllowedCommands`: `Occupied` durumunda `SetCleaning` hiç listelenmiyor,
yalnız `OutOfService` durumundan çıkarken kullanılıyor). Bu yüzden
seçenekler arasında "her Occupied→Available geçişine yeni bir zorunlu
adım ekle" YOKTU — Semih bunu açıkça reddetti ("iş yükü artmış").

**Seçilen çözüm (AskUserQuestion ile onaylandı): pasif zaman rozeti.**
Personel yine tek tıkla "Müsait yap"a basıyor; sistem, masa Available
olduktan sonraki ilk birkaç dakika kart üzerinde "Az önce boşaldı"
rozeti gösteriyor (V1-WTR-019'un yaş göstergesiyle aynı desen — pasif,
bloklamıyor, kimse dismiss etmiyor, süre dolunca kendiliğinden kayboluyor).

**Neden yeni bir sütun gerekti (read-model trick yeterli olmadı):**
V1-WTR-019'un "yeni sütun değil, mevcut zaman damgasını oku" deseni
(`orders.orders.created_at`, `current_order_id` üzerinden) burada
uygulanamadı — `current_order_id`, bir masa `Available`'a döndüğü TAM
ANDA `NULL`'a temizleniyor (`PostgresTableRepository.UpdateStatusAsync`,
V1-ORD-006), yani masa boşaldıktan sonra okunacak hiçbir zaman damgası
kalmıyor. Yeni `table_mgmt.tables.status_changed_at` sütunu bu yüzden
gerçekten gerekliydi.

**Yol boyunca bulunan ayrı bir gerçek hata (bu görevle birlikte
düzeltildi):** `TableDto.CurrentOrderOpenedAt`'in tel (wire) adı hiçbir
zaman PosTerminal'in `TableRecord.occupiedSince` alanıyla eşleşmiyordu
(varsayılan camelCase politikası "currentOrderOpenedAt" üretiyor,
"occupiedSince" değil) — V1-WTR-019'un kendi "masa yaşlanma göstergesi"
özelliği, gemiye bindiği günden beri PosTerminal'de SESSİZCE hiç
çalışmamış (her Dolu masanın "geçen süre" rozeti hep "—" gösteriyordu).
`[property: JsonPropertyName("occupiedSince")]` ekleyerek düzeltildi —
istemci değil, tel adı değişti.

## Owned surface

- `database/migrations/V1/V1-TBL-009/106-tables-status-changed-at.up.sql`
  (yeni) — `table_mgmt.tables.status_changed_at TIMESTAMPTZ NOT NULL
  DEFAULT NOW()`.
- `database/migrations/V1/V1-TBL-009/106-tables-status-changed-at.down.sql`
  (yeni).
- Sınırlı ek:
  - src/Host/Composition/Migrations/MigrationManifest.cs (V0-ARC-001
    sahipliğinde) — `PhaseBMax` "105" → "106", doc comment güncellendi.
  - database/MigrationComposition/order.json (V0-ARC-001 sahipliğinde) —
    yeni "106" girdisi, `phaseBRange.max` güncellendi.
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs (V1-FND-001
    sahipliğinde) — `RuntimeManifestIds`, `LastEntryTables`, beklenen sayı
    güncellendi (order.json'a csproj Link ile bağlı, gerçek dosyayı okuyor).
  - src/Modules/Tables/TableLifecycle/PostgresTableRepository.cs (V1-TBL-001
    sahipliğinde) — tek-bağlantılı `UpdateStatusAsync`'e `status_changed_at
    = NOW()` eklendi (yalnız bu aşırı yükleme — transactional aşırı yükleme,
    `QrTableReservationPolicy`'nin kullandığı, yalnız Reserved'a geçiş için
    kullanılıyor ve bu görevin badge'i yalnız Available durumunu önemsediği
    için dokunulmadı).
  - tests/Modules/Tables/TableLifecycle/ALKAROS.Tables.TableLifecycle.Tests.csproj
    (ilgili proje sahipliğinde) — yeni migration için csproj Link eklendi
    (bu projenin kendi izole migration alt kümesi var, tam manifest'i
    çalıştırmıyor).
  - src/Host/Experience/Tables/TableManagementStore.cs,
    TableManagementContracts.cs, TableManagementApplication.cs (V1-TBL-001/
    V1-RMD-118 sahipliğinde) — `GetAsync`/`GetAllAsync` artık
    `status_changed_at`'i de okuyup dönüyor; `TableDto`'ya `StatusChangedAt`
    eklendi; `CurrentOrderOpenedAt`'in tel adı "occupiedSince"e sabitlendi
    (yukarıdaki ayrı hata düzeltmesi); Create/Update/ChangeStatus uç
    noktaları artık mutasyondan sonra `store.GetAsync` ile gerçek değeri
    geri okuyup dönüyor (`ToDtoWithStatusChangedAtAsync`), `default`
    değerini sızdırmıyor.
  - tests/Host/Experience/Tables/TableManagementHttpTests.cs (ilgili proje
    sahipliğinde) — iki yeni test (tel adı düzeltmesi, `StatusChangedAt`'in
    her gerçek durum değişikliğinde ilerlediği).
  - src/Clients/PosTerminal/src/features/tables/models.ts,
    TableWorkspace.tsx, FloorPlanWorkspace.tsx, tables.css, floorPlan.css
    (PosTerminal sahipliğinde) — `TableRecord.statusChangedAt`,
    `isRecentlyVacated` (her iki dosyada da yerel, `elapsedLabel`'in zaten
    kurduğu kopyalama deseniyle aynı), rozet render'ı, rozet CSS'i.
  - tests/Modules/QrOrdering/PendingOrders, tests/Modules/Inventory/*,
    (Sınırlı ek listelerine dahil değil, dokunulmadı — yalnız regresyon
    için koşuldu).
  - src/Clients/PosTerminal/src/features/tables/TableWorkspace.test.tsx,
    FloorPlanWorkspace.test.tsx, tableApi.test.ts (ilgili test
    sahipliğinde) — yeni `statusChangedAt` alanı fixture'lara eklendi,
    üç yeni rozet testi.

## Out of scope

- `Occupied` durumuna yeni bir "Temizliğe al" butonu eklemek — Semih
  bunu açıkça reddetti (iş yükünü artırır); bu görev SIFIR yeni tıklama
  ekliyor.
- `status_changed_at`'in `QrTableReservationPolicy`'nin kullandığı
  transactional `UpdateStatusAsync` aşırı yüklemesinde de güncellenmesi
  — o yol yalnız Available→Reserved geçişini yazıyor, bu görevin rozeti
  yalnız Available durumunu önemsiyor, dokunulmadı.
- `status_changed_at`'in TableMerge/TableTransfer/Reservations'ın kendi
  ham SQL'lerinde de güncellenmesi — bunlar `current_status`'u
  `PostgresTableRepository.UpdateStatusAsync`'in dışında, kendi
  dosyalarında yazıyor (10 farklı dosya, birçoğu ayrı görev sahipliğinde).
  Rozetin asıl önemsediği yol (bir masayı normal şekilde kapatıp
  müsaitliğe döndürme) zaten tek yoldan (`TableManagementStore.
  ChangeStatusAsync` → `UpdateStatusAsync`) geçiyor; diğer, daha nadir
  yollardan Available'a dönen bir masa rozeti göstermez — zararsız bir
  eksik gösterim, veri bütünlüğü sorunu değil.
- `TableDto`'nun `CurrentOrderTotal`/`CurrentOrderOpenedAt` alanlarının
  Create/Update/ChangeStatus yanıtlarında ÖNCEDEN de yanlış (varsayılan)
  olması — bu görev yalnız `StatusChangedAt` için gerçek değeri garanti
  ediyor; aynı düzeltme (`ToDtoWithStatusChangedAtAsync`) diğer ikisini
  de bir yan etki olarak düzeltti, ayrıca test edilmedi (mevcut
  `TableListAndDetailReportTheCurrentOrdersRealRunningTotal` testi zaten
  yalnız LIST/GET uçlarını kapsıyor).

## Dependencies

- V1-TBL-001
- V1-WTR-019

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres dotnet test`:
  - `tests/Host/MigrationComposition` → 135/135.
  - `tests/Modules/Tables/TableLifecycle` → 59/59.
  - `tests/Host/Experience/Tables` → 14/14 (2 yeni test).
  - Bu görevden önce `V1-TBL-001` migration dosyalarına bağlı KALAN 26
    test projesi de tek tek çalıştırıldı (regresyon taraması) — hepsi
    değişmeden yeşil: `Host.Experience.{HelpRequests,NfcOrdering,Orders.
    {Comp,Confirmation,TableDraft,Void,VoidSent},QrOrdering,RelaySettings}`,
    `Billing.{Adjustments,BillFoundation,SplitDesign}`, `Kitchen.
    {PhysicalPrintRecovery,PrintQueue,TicketLifecycle}`, `Orders.
    {ItemExceptions,OrderAggregate,SubmitOrder}`, `QrOrdering.
    {CustomerSession,PendingOrders,RelayCredential,RelaySecurity,
    TokenLifecycle}`, `Tables.{CurrentPointers,Reservations,TableMerge,
    TableTransfer}`.
- `cd src/Clients/PosTerminal && npx tsc --noEmit` → 0 hata.
- `cd src/Clients/PosTerminal && npx vitest run` → 141/141 (tüm proje —
  26/26 tables özelinde, 3 yeni rozet testi dahil).
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.

## Handoff

- None
