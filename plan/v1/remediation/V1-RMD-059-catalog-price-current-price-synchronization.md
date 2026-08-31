# V1-RMD-059 - Catalog price current price synchronization

- Task ID: V1-RMD-059
- Status: Done
- Assignee: 1dec2ab0-b9bc-4bc0-84d5-c4cabf3e4a6a
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-31

## Goal

`CatalogManagementStore.cs` içinde yeni fiyat eklendiğinde `catalog.products.current_price` sütununun güncellenmesini sağlamak; POS terminali ve sipariş motorunun güncel fiyatları görmesini temin etmek.

## Owned surface

- `plan/v1/remediation/V1-RMD-059-catalog-price-current-price-synchronization.md`
- PO:2026-08-31 kararıyla src/Host/Experience/Catalog/** yüzeyi V1-RMD-063'e devredildi; bu historical task closed kalır.
- `evidence/V1-RMD-059/**`

## In scope

- `CatalogManagementStore.cs` içinde `CreatePriceAsync` metodunda geçerli fiyatın `catalog.products.current_price` alanına yazılması.
- Fiyat güncelleme ve deaktive etme yardımcı metodlarının eklenmesi.

## Out of scope

- İstemci arayüzlerini değiştirmek.

## Dependencies

- V1-RMD-058

## Deliverables

- Fiyat değişikliklerini anında `current_price` sütununa yansıtan katalog yönetim deneyimi.

## Acceptance evidence

- `dotnet build ALKAROS.slnx` 0 hata verir.

## Handoff

- V1-RMD-060
