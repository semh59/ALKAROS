# V1-RMD-063 - Catalog price schema alignment

- Task ID: V1-RMD-063
- Status: Done
- Assignee: 0e4d2094-eff5-490e-9402-056e17c86376
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-31

## Goal

`CatalogManagementStore.CreatePriceAsync` içindeki tanımsız `updated_at` kolon referansını kaldırarak aktif satış fiyatı oluşturma isteklerinin PostgreSQL Error 42703 ile 503 DATABASE_UNAVAILABLE hatası vermesini engellemek.

## Owned surface

- `plan/v1/remediation/V1-RMD-063-catalog-price-schema-alignment.md`
- `src/Host/Experience/Catalog/**`
- `tests/Host/Experience/Catalog/**`
- `evidence/V1-RMD-063/**`

## In scope

- `CatalogManagementStore.cs` içinde `UPDATE catalog.products` sorgusundan `updated_at = now()` ifadesini kaldırmak.
- `ALKAROS.Host.Experience.Catalog.Tests` içindeki HTTP testlerinin 0 hata ile geçmesini sağlamak.

## Out of scope

- Diğer modül veya host uç noktalarını değiştirmek.

## Dependencies

- V1-GOV-036

## Deliverables

- Düzeltilmiş `CatalogManagementStore.cs` ve yeşil geçen katalog HTTP testleri.

## Acceptance evidence

- `dotnet test tests/Host/Experience/Catalog/ALKAROS.Host.Experience.Catalog.Tests.csproj` 0 hata verir.

## Handoff

- V1-RMD-064
