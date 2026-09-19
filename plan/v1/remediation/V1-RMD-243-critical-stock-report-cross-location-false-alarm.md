# V1-RMD-243 - Stop the critical-stock report from alarming on locations an item was never assigned to

- Task ID: V1-RMD-243
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`GetCriticalStockReportAsync` (`PostgresMenuInventoryReportingService.cs`)
`CROSS JOIN`ed every active `stock_items` row with every active
`stock_locations` row. An item stocked only in "Mutfak" (kitchen) — the
normal case, `StockLocationType` itself models Warehouse/Kitchen/Bar/etc.
as distinct places — got a fabricated `available = 0` row for every OTHER
location (e.g. "Bar") it was never assigned to, and `0 <= threshold`
(any non-negative reorder point, including the `0` default) makes that a
false "critical" alarm. `LowStockAlertHostedService` runs this report
every 5 minutes with no location filter, so this repeats on every scan. A
bağımsız denetim ajanı (2026-09-18, tüm proje kod denetimi, Catalog/Menu/
Recipes/Production/Inventory/Purchasing alanı) bunu tespit etti.

## Owned surface

- `evidence/V1-RMD-243/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Reporting/MenuInventory/PostgresMenuInventoryReportingService.cs
  (V11-RPT-002/003 sahipliğinde kalır) — yalnız `GetCriticalStockReportAsync`'in
  `CROSS JOIN`ı, kalemin gerçekten ilişkili olduğu lokasyonlarla (kendi
  `default_location_id`'si veya zaten bir `stock_balances` satırı olan
  herhangi bir lokasyon) sınırlanır; diğer raporlar/metodlar değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Reporting/MenuInventory/MenuInventoryReportingDatabaseTests.cs
  (V11-RPT-002/003 sahipliğinde kalır) — yeni bir çapraz-lokasyon testi
  eklenir, mevcut testler değişmez (hepsi zaten her kalemin sorgulanan
  lokasyonda gerçek bir bakiye satırı olduğu senaryoyu kullanıyor).

## In scope

- Bir kalem yalnızca A lokasyonunda stoklanıyorsa, B lokasyonu için hiç
  satır üretilmez (ne kritik ne kritik-değil) — B lokasyonu o kalemle
  hiç ilgili değildir.
- Kalemin `default_location_id`'si veya gerçek bir `stock_balances`
  satırı olan her lokasyon hâlâ raporlanır (davranış aynı).

## Out of scope

- `GetActualVsTheoreticalReportAsync` ve diğer rapor metodları.
- `default_location_id` hiç atanmamış VE hiç hareket görmemiş yepyeni bir
  kalemin hangi lokasyonda "kritik" sayılacağı — böyle bir kalem zaten
  hiçbir gerçek lokasyonla ilişkili değil, raporlanmaması doğru davranış.

## Dependencies

- V11-RPT-002

## Acceptance evidence

- Gerçek Postgres testi: bir kalem yalnız Mutfak'ta stoklanıp Bar'da hiç
  bulunmadığında, filtre olmadan çağrılan rapor Bar için hiç satır
  içermez (önceden `available=0, isCritical=true` bir satır üretirdi).
- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `dotnet test` (Reporting.MenuInventory testleri) → yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None
