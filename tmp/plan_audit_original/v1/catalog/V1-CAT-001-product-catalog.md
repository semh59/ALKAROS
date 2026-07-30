# V1-CAT-001 - Implement the product catalog

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Implement category, tax profile, product, modifier group and modifier management.

## Owned surface

- `src/Modules/Catalog/ProductCatalog/**`, `tests/Modules/Catalog/ProductCatalog/**`, `database/migrations/V1/V1-CAT-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Validated product types, stock modes, tax-profile requirement and modifier compatibility.

## Out of scope

- Effective-dated pricing and daily menu availability.

## Dependencies

- V1-FND-001,V0-DAT-002

## Deliverables

- V1-CAT-001 için production implementation.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Invalid stock/type/tax combinations are rejected; SKU and modifier constraints are enforced in PostgreSQL and tests.

## Handoff

- V1-CAT-002, V1-ORD-001 and V11-MNU-001.

