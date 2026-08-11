# V1-CAT-001 - Implement the product catalog

- Task ID: V1-CAT-001
- Status: Done
- Assignee: opencode-v1-cat-001
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:I.7-I.10
- PDF:II.2.2
- PDF:III.4

## Goal

Category, TaxProfile, Product, ModifierGroup ve Modifier yönetimini domain kısıtlarıyla uygulamak.

## Owned surface

- `src/Modules/Catalog/ProductCatalog/CatalogModule.cs`
- `src/Modules/Catalog/ProductCatalog/Category.cs`
- `src/Modules/Catalog/ProductCatalog/Enums.cs`
- `src/Modules/Catalog/ProductCatalog/Modifier.cs`
- `src/Modules/Catalog/ProductCatalog/ModifierGroup.cs`
- `src/Modules/Catalog/ProductCatalog/PostgresCategoryRepository.cs`
- `src/Modules/Catalog/ProductCatalog/PostgresModifierGroupRepository.cs`
- `src/Modules/Catalog/ProductCatalog/PostgresModifierRepository.cs`
- `src/Modules/Catalog/ProductCatalog/PostgresProductModifierGroupRepository.cs`
- `src/Modules/Catalog/ProductCatalog/PostgresTaxProfileRepository.cs`
- `src/Modules/Catalog/ProductCatalog/ProductModifierGroup.cs`
- `src/Modules/Catalog/ProductCatalog/Repositories.cs`
- `src/Modules/Catalog/ProductCatalog/TaxProfile.cs`
- `tests/Modules/Catalog/ProductCatalog/Fixtures/CatalogTestDatabase.cs`
- `database/migrations/V1/V1-CAT-001/**`
- C52 current-price source/test surface is transferred to V1-CAT-003; this historical task remains closed.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Doğrulanmış ürün türleri, stok modları, vergi profili gereksinimi ve değiştirici uyumluluğu.

## Out of scope

- Geçerli tarihli fiyatlandırma ve günlük menü kullanılabilirliği.

## Dependencies

- V1-FND-001
- V0-DAT-002
- V0-CMP-002

## Deliverables

- `src/Modules/Catalog/ProductCatalog/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- PDF III.4'te tanımlı olmayan ürün türü, stok modu ve selection_type değerleri ile min_selections >
  max_selections aralığı PostgreSQL check constraint ve domain testlerinde reddedilir.
- SKU, kategori kodu, vergi kodu, değiştirici kodu (global unique) ve product_modifier_groups ikilisi unique
  kısıtlarıyla; bilinmeyen category_id / product_id / modifier_group_id FK doğrulamalarıyla PostgreSQL ve testlerde
  uygulanır.

## Handoff

- V1-CAT-002
- V1-ORD-001
- V11-MNU-001
