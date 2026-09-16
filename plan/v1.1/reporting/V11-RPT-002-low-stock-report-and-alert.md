# V11-RPT-002 - Düşük stok raporu + canlı uyarı

- Task ID: V11-RPT-002
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Planned

## Goal

`IMenuInventoryReportingService.GetCriticalStockReportAsync` V1.1'den beri
var ama hiçbir Host ucu onu çağırmıyordu, ve `ReportingModule` bu servisi
hiç kaydetmiyordu — greenfield. Bu görev iki şeyi ekliyor: (1) raporun
kendisi için bir yönetim ucu, (2) V11-INV-009'un eklediği kalıcı
`ReorderPoint`'i periyodik tarayıp yeni kritikleşen kalemleri bağlı
yönetici/supervizör oturumlarına canlı yayınlayan bir arka plan servisi.

## Owned surface

- `src/Host/Experience/InventoryReporting/**` (yeni)
- `tests/Host/Experience/InventoryReporting/**` (yeni)

Sınırlı ek (yollar geri-tik olmadan):

- src/Modules/Reporting/ReportingModule.cs (ilgili modülün sahipliğinde) —
  `IMenuInventoryReportingService` ilk kez kaydedildi.
- src/Host/DualScreen/DualScreenApplication.cs (paylaşılan) — yeni
  deneyimin `AddInventoryReportingExperience`/`MapInventoryReportingApi`
  çağrıları.
- ALKAROS.slnx (paylaşılan) — yeni test projesi.

## In scope

1. `ReportingModule`'a `IMenuInventoryReportingService` kaydı (daha önce
   hiç kayıtlı değildi).
2. Yeni Host alanı `src/Host/Experience/InventoryReporting/`:
   - `GET /api/v1/management/inventory/reports/critical-stock?locationId=&criticalThreshold=`
     — `reports.view` izniyle korunan (Kitchen'ın kendi performans
     raporuyla aynı emsal), `inventory.manage` değil — rapor okumak stok
     yapılandırmaktan farklı bir yetki.
   - `LowStockAlertHub` (SignalR, `alkaros.manager` çerezi, `HelpRequestHub`
     ile aynı düz-yayın deseni).
   - `LowStockAlertHostedService` (`BackgroundService`,
     `KitchenPrintDispatchHostedService`'in "scope aç → iş yap →
     exception yut/logla → Task.Delay" deseni, ama 5 saniye değil 5 dakika
     aralıklı): periyodik olarak kritik stok raporunu çeker (eşik her
     zaman V11-INV-009'un kalıcı `ReorderPoint`'inden gelir — bu servis
     asla kendi eşiğini uydurmaz), yalnızca YENİ kritikleşen kalemler için
     (önceki taramaya göre "geçiş" — kendi içindeki bir `HashSet` ile takip
     edilir) `LowStockAlertHub`'a yayın yapar; hâlâ kritik olan bir kalem
     için tekrar tekrar uyarı basmaz, ama düzelip yeniden kritikleşen bir
     kalem için tekrar uyarır.

## Out of scope

- AvT (gerçek vs teorik) varyans raporu — V11-RPT-003'ün kapsamı.
- İstemci arayüzü — plan dosyasının kendi "kapsam dışı" kararı.

## Dependencies

- V11-INV-009
- V11-RPT-001

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- Gerçek Postgres'e karşı HTTP testleri (rapor ucu, yetki reddi) → tüm
  testler yeşil.
- `LowStockAlertHostedService.ScanAsync` için sahte (fake)
  `IMenuInventoryReportingService` + kaydedici `IHubContext` ile
  deterministik testler: ilk tarama yayınlar, hâlâ kritik olan tekrar
  yayınlamaz, düzelip yeniden kritikleşen tekrar yayınlar — en az bir
  testte revert-and-confirm (bu görevde test verisindeki sabit olmayan
  `StockLocationId` yüzünden gerçek bir test hatası bulundu ve düzeltildi).
- `dotnet test tests/Host/MigrationComposition` → temiz (bu görev migration
  eklemedi, ama regresyon kontrolü için çalıştırıldı).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- `python tools/project-manifest/project_manifest_tool.py` → VALID.
