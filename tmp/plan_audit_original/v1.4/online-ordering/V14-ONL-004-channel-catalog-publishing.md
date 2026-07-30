# V14-ONL-004 - Publish channel catalog

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: integration

## Goal

Publish the approved menu/product projection to each enabled online-order channel with deterministic external identifiers.

## Owned surface

- `src/Modules/OnlineOrdering/CatalogPublishing/**`, `tests/Modules/OnlineOrdering/CatalogPublishing/**`, `database/migrations/V14/V14-ONL-004/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Provider capability contract, catalog projection, idempotent publish, external ID persistence, retry and result audit.

## Out of scope

- Stock availability, price ownership, inbound order webhooks and operator UI.

## Dependencies

- V14-MAP-001, V11-MNU-002, V0-YSP-001

## Deliverables

- Provider-specific catalog publisher for every approved channel.
- Contract tests and real sandbox evidence for enabled providers.
- Unsupported provider capabilities recorded as explicit validation failures.

## Acceptance evidence

- Repeating the same publish produces no duplicate external product; mapped names, prices, tax metadata and modifier structure match the approved provider response.

## Handoff

- V14-ONL-005 and V14-OUI-001.
