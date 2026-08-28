# V1-RMD-013 - Table management production API

- Task ID: V1-RMD-013
- Status: Done
- Assignee: /root
- Work type: integration
- Surface state: Existing

## Goal

Mevcut table domain ve persistence davranışlarını, capability-based authorization ve optimistic concurrency sınırlarıyla
production HTTP sözleşmesine açmak. Görev yeni masa iş kuralı üretmez; yalnız doğrulanmış repository servislerini tek
table-management API sınırında compose eder.

## Owned surface

- `src/Host/Experience/Tables/ZoneConcurrencyStore.cs`
- `tests/Host/Experience/Tables/ALKAROS.Host.Experience.Tables.Tests.csproj`
- `tests/Host/Experience/Tables/packages.lock.json`
- `tests/Host/Experience/Tables/TableManagementHttpTests.cs`
- `tests/Host/Experience/Tables/TableManagementRegistrationTests.cs`
- `evidence/V1-RMD-013/**`
- PO:2026-08-28 desktop floor kararıyla table application/contracts/store ve yeni floor API test yüzeyi
  V1-RMD-026'ya devredildi; bu historical task closed kalır.

## Dependencies

- V1-GOV-010
- V1-RMD-009

## Acceptance evidence

- Yetkili zone/table read, create ve repository'nin desteklediği update işlemleri; status transition, reservation,
  transfer, merge/unmerge ve current pointer read endpoint'leri versioned contract ve gerçek HTTP contract testleriyle
  geçer. Transfer, merge, reservation ve current-pointer servisleri Host Experience registration yüzeyinde endpoint
  çözümlemesinden önce gerçek DI kaydıyla doğrulanır; gizli mock veya hard-coded success kullanılmaz.
- Her mutation current row version ister; stale version `409` conflict envelope döndürür ve kısmi değişiklik bırakmaz.
  Missing/expired session `401`, authenticated fakat yetkisiz principal `403` verir.
- `dotnet build ALKAROS.slnx -c Release` ve ilgili table/API testleri exit code `0` verir. Migration değişikliği gerekirse
  ayrı migration task'ı açılır; bu görev mevcut migration dosyalarını değiştirmez.
- Semih, gerçek Host ve PostgreSQL üzerinde zone/table oluşturur, masayı occupied yapar, ödenmemiş siparişi transfer ve
  merge eder; ardından stale row version ile `409` alıp hiçbir kısmi değişiklik olmadığını gözlemler.

## Handoff

- V1-RMD-017
