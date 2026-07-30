# V14-MAP-001 - Implement provider product mapping

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Map provider product/modifier identifiers to active internal catalog items with explicit unmapped behavior.

## Owned surface

- `src/Modules/OnlineOrdering/Yemeksepeti/ProductMapping/**`, `tests/Modules/OnlineOrdering/Yemeksepeti/ProductMapping/**`, `database/migrations/V14/V14-MAP-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Mapping uniqueness, active dates, modifier validation and unmapped rejection.

## Out of scope

- Catalog export/update and status synchronization.

## Dependencies

- V1-CAT-001,V0-YSP-001

## Deliverables

- V14-MAP-001 için production implementation.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- One external product resolves to one active internal product; missing/ambiguous mapping cannot create an Order.

## Handoff

- V14-ONL-002.

