# V1-RMD-076 - Catalog product availability suspension

- Task ID: V1-RMD-076
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-31

## Goal

Katalog ürününe silmeden hızlıca "menüden kaldır" imkanı veren kullanılabilirlik durumu eklemek. Ürün domain'ine `IsAvailable` alanı, askıya alma ve geri açma işlemleri, migration, manager toggle endpoint'i ve PosTerminal katalog arayüzünde tek dokunuşla değiştirme eklenir; satış istemcilerine sunulan katalogdan kullanılamayan ürünler dışlanır.

## Owned surface

- `plan/v1/remediation/V1-RMD-076-catalog-product-availability-suspension.md`
- `src/Modules/Catalog/ProductCatalog/Product.cs`
- `src/Modules/Catalog/ProductCatalog/PostgresProductRepository.cs`
- `src/Host/Experience/Catalog/**`
- `src/Clients/PosTerminal/src/features/catalog/**`
- `tests/Modules/Catalog/ProductCatalog/DomainTests.cs`
- `tests/Modules/Catalog/ProductCatalog/PostgresRepositoryTests.cs`
- `tests/Host/Experience/Catalog/**`

## In scope

- `Product` aggregate'inde `IsAvailable` alanı ve `Suspend` / `Restore` davranışları; repository yazma ve okuma yollarında taşınması. (`catalog.products.is_available` sütunu `V1-GOV-040` birleşik `040` migration'ı ile sağlanır.)
- Katalog yönetim endpoint'inde ürünü askıya alma ve geri açma; yalnızca manager yetkisi.
- Satış istemcilerine dönen katalog okuma yolunun kullanılamayan ürünleri dışlaması `V1-RMD-077` görevine devredilir.
- PosTerminal katalog çalışma alanında ürün satırında kullanılabilirlik toggle'ı ve durum göstergesi.
- Domain, repository ve endpoint xUnit testleri ile arayüz Vitest testi.

## Out of scope

- Stok takibi, reçete veya fiyat mantığını değiştirmek.
- Kategori veya modifier düzeyinde kullanılabilirlik.

## Dependencies

- V1-RMD-074

## Deliverables

- `is_available` sütunu, domain davranışları, manager toggle endpoint'i, istemci dışlama ve arayüz toggle'ı ile katalog ürün kullanılabilirliği.

## Acceptance evidence

- `dotnet test` katalog modülü ve host catalog testleri sıfır hata verir.
- Semih; bir ürünü askıya alır, satış istemcisinde görünmediğini, yönetim ekranında geri açılabildiğini doğrular.

## Handoff

- V1-RMD-075
