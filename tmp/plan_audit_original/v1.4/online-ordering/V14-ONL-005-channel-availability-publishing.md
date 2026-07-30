# V14-ONL-005 - Publish channel availability

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: integration

## Goal

Publish sellable or unavailable state to enabled online channels from the single approved availability projection.

## Owned surface

- `src/Modules/OnlineOrdering/AvailabilityPublishing/**`, `tests/Modules/OnlineOrdering/AvailabilityPublishing/**`, `database/migrations/V14/V14-ONL-005/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Availability event consumption, provider throttling, idempotent updates, retry, dead-letter handling and divergence detection.

## Out of scope

- Stock deduction, recipe calculation, catalog content and inbound order acceptance.

## Dependencies

- V14-STK-001, V14-ONL-004

## Deliverables

- Provider-specific availability publisher for every approved channel.
- Contract, retry, rate-limit and stale-event tests.
- Real sandbox evidence for enabled providers.

## Acceptance evidence

- A last-portion transition reaches every enabled sandbox channel once logically; delayed older events cannot overwrite a newer state.

## Handoff

- V14-REC-001 and V14-RPT-001.
