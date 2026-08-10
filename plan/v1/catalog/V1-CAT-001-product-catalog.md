# V1-CAT-001 - Implement the product catalog

- Task ID: V1-CAT-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.7-I.10
- PDF:II.2.2
- PDF:III.4

## Goal

Category, TaxProfile, Product, ModifierGroup ve Modifier yönetimini domain kısıtlarıyla uygulamak.

## Owned surface

- `src/Modules/Catalog/ProductCatalog/**`, `tests/Modules/Catalog/ProductCatalog/**`,
  `database/migrations/V1/V1-CAT-001/**`
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

- Geçersiz stok/tür/vergi kombinasyonları reddedilir; SKU ve değiştirici kısıtlamaları PostgreSQL ve testlerde
  uygulanır.

## Handoff

- V1-CAT-002
- V1-ORD-001
- V11-MNU-001
