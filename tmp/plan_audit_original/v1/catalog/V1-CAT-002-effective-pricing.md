# V1-CAT-002 - Implement effective-dated product pricing

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Implement price records and the authoritative current-price query without independently writable duplicate price state.

## Owned surface

- `src/Modules/Catalog/Pricing/**`, `tests/Modules/Catalog/Pricing/**`, `database/migrations/V1/V1-CAT-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Non-overlapping effective periods, price type, currency and deterministic point-in-time lookup.

## Out of scope

- Promotions, discounts and daily-menu override pricing.

## Dependencies

- V1-CAT-001,V0-CMP-002,V0-DAT-004

## Deliverables

- V1-CAT-002 için production implementation.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Overlapping active ranges are rejected and any timestamp resolves to at most one price per product/type/currency.

## Handoff

- V1-ORD-001 and V11-MNU-001.

