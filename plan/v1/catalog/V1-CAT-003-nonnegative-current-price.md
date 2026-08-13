# V1-CAT-003 - Independently verify nonnegative current price

- Task ID: V1-CAT-003
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

zero/positive price'ın kabul edildiğini ve negative price'ın domain ile PostgreSQL sınırlarında atomik olarak
reddedildiğini bağımsız doğrulamak.

## Owned surface

- `src/Modules/Catalog/ProductCatalog/Product.cs`
- `src/Modules/Catalog/ProductCatalog/PostgresProductRepository.cs`
- `tests/Modules/Catalog/ProductCatalog/DomainTests.cs`
- `tests/Modules/Catalog/ProductCatalog/PostgresRepositoryTests.cs`
- `database/migrations/V1/V1-CAT-003/014-catalog-current-price-bound.up.sql`
- `database/migrations/V1/V1-CAT-003/014-catalog-current-price-bound.down.sql`
- `evidence/V1-CAT-003/**`

## In scope

- `CODE-014` için current-price nonnegative invariant'ını Product, PostgreSQL repository ve additive migration
  sınırlarında uygulamak.

## Out of scope

- Global MigrationComposition manifest, project, lock veya plan dosyası değiştirmek.

## Dependencies

- V0-GOV-035
- V1-CAT-001
- V1-FND-021

## Deliverables

- Current-price implementation/migration diff'i, domain/repository tests ve raw transcript.

## Acceptance evidence

- Negative current price domain ve database katmanlarında reddedilir; zero/positive kabul edilir.
- Forward/down migration lifecycle, focused tests ve plan validator exit code `0` verir.

## Handoff

- V0-GOV-045
