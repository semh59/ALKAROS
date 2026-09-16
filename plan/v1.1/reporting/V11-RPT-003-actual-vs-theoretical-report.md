# V11-RPT-003 - Gerçek vs Teorik (AvT) varyans raporu

- Task ID: V11-RPT-003
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Planned

## Goal

Rakip karşılaştırmasının (Toast/xtraCHEF referansı, 2026-09-16) bulduğu
boşluğun son adımı: satış-bazlı gerçek-vs-teorik (AvT) stok varyans raporu.
Bu görev, zincirin önceki beş görevinin ürettiği veriyi bir araya getiriyor
— V11-RCP-004'ün teorik tüketim gölge kaydı, V11-INV-008'in fiziksel sayım
kayıtları, ve mevcut satın alma teslim alım hareketleri — klasik AvT
formülüyle: `ActualUsage = OpeningCount + PurchaseReceipts − ClosingCount`,
`Variance = ActualUsage − TheoreticalUsage`. Açılış veya kapanış sayımı
olmayan kalemler sahte kesinlik üretmemek için rapordan hariç tutuluyor.

## Owned surface

Bu görevin kendi Owned surface'i yok — tamamen önceki beş görevin kurduğu
altyapıyı birleştiren, paylaşılan dosyalara yapılan eklerden oluşuyor.

Sınırlı ek (yollar geri-tik olmadan):

- src/Modules/Reporting/MenuInventory/ReportingModels.cs,
  src/Modules/Reporting/MenuInventory/IMenuInventoryReportingService.cs,
  src/Modules/Reporting/MenuInventory/PostgresMenuInventoryReportingService.cs
  (V11-RPT-001 sahipliğinde) — yeni rapor tipi eklendi.
- src/Host/Experience/InventoryReporting/InventoryReportingEndpoints.cs
  (V11-RPT-002 sahipliğinde) — yeni rota eklendi.
- tests/Modules/Reporting/MenuInventory/MenuInventoryReportingDatabaseTests.cs,
  tests/Modules/Reporting/MenuInventory/ALKAROS.Reporting.MenuInventory.Tests.csproj,
  tests/Host/Experience/InventoryReporting/InventoryReportingHttpTests.cs
  (paylaşılan) — yeni testler eklendi.

Bu görevin kendi Owned surface'i yok — tamamen önceki beş görevin kurduğu
altyapıyı birleştiren, paylaşılan dosyalara yapılan ekler.

## In scope

1. `ActualVsTheoreticalReportItem/Query/Report` (`ReportingModels.cs`'in
   kurduğu `<X>ReportItem/Query/Report` kalıbı) — `GetPortionConsumptionReportAsync`'in
   aynı "birincil tabloya LEFT JOIN alt-sorgu toplamları" SQL kalıbı, ama
   açılış/kapanış sayımı için `LATERAL` alt-sorgu (her (stok kalemi, konum)
   çifti için dönem başı/sonuna en yakın `stock_physical_counts` satırı).
   - `TheoreticalUsage` = dönem içindeki `recipe.theoretical_consumption_records`
     toplamı (yalnızca stok kalemi bazında — bu tablo konum bilgisi
     taşımıyor; bir kalem birden fazla konumda sayılıyorsa teorik tüketim
     her konum satırına aynen atanır — belgelenen bilinçli bir
     basitleştirme, çoğu kalem tek konumda yaşadığı için kabul edilebilir).
   - `ActualUsage = OpeningCount + PurchaseReceipts − ClosingCount`.
   - Açılış veya kapanış sayımı olmayan (stok kalemi, konum) çiftleri
     rapordan hariç tutulur; kaç tanesinin hariç tutulduğu
     `ExcludedForMissingCountsCount` ile şeffaf.
   - `VarianceQuantity = ActualUsage − TheoreticalUsage`,
     `VariancePercentage` teorik tüketim 0 ise null.
2. `GET /api/v1/management/inventory/reports/actual-vs-theoretical?from=&to=&locationId=`,
   `reports.view` (V11-RPT-002'nin kurduğu aynı filtre altında, yeni bir
   Host alanı gerekmiyor).

## Out of scope

- İstemci arayüzü — plan dosyasının kendi "kapsam dışı" kararı.
- Konum-ayrıştırılmış teorik tüketim (theoretical_consumption_records'a
  konum eklemek) — gelecekte gerçek bir ihtiyaç doğarsa ayrı görev.

## Dependencies

- V11-RCP-004
- V11-INV-008
- V11-RPT-002

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- Gerçek Postgres'e karşı rapor testleri (tam veri → doğru varyans hesabı;
  açılış sayımı eksik kalem → hariç tutulur) → tüm testler yeşil.
- Gerçek Postgres'e karşı HTTP testleri (from/to zorunlu, boş rapor) → tüm
  testler yeşil.
- `dotnet test tests/Host/MigrationComposition` ve
  `tests/Architecture/ModuleBoundaries` → temiz (bu görev migration/edge
  eklemedi, regresyon kontrolü için çalıştırıldı).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- `python tools/project-manifest/project_manifest_tool.py` → VALID.

