# V1-KIT-002 - Implement deterministic printer routing

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Resolve each kitchen item to exactly one configured station/printer route or an explicit configuration error.

## Owned surface

- `src/Modules/Kitchen/Routing/**`, `tests/Modules/Kitchen/Routing/**`, `database/migrations/V1/V1-KIT-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Product/category/daily-item precedence, ambiguity detection and inactive printer handling.

## Out of scope

- Print queue retry and device protocol.

## Dependencies

- V1-KIT-001,V1-CAT-001

## Deliverables

- V1-KIT-002 için production implementation.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Ambiguous routes are rejected at configuration time; every routable item resolves deterministically in tests.

## Handoff

- V1-KIT-003.

