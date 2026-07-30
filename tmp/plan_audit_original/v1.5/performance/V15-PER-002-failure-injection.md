# V15-PER-002 - Implement failure-injection test suite

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Inject process, database, network, provider and printer failures at critical transaction boundaries.

## Owned surface

- `tests/Resilience/FailureInjection/**`, `docs/resilience/V15-PER-002.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Crash windows, timeout, reconnect, retry storm, disk full and recovery invariants.

## Out of scope

- New production recovery behavior not already owned by another task.

## Dependencies

- V15-PER-001,V15-BKP-002,V15-OBS-002

## Deliverables

- V15-PER-002 için production implementation veya executable test asset.
- Başarı, ret/failure ve recovery testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Every injected failure has deterministic safe outcome; no silent success, lost order, duplicate financial effect or negative stock.

## Handoff

- V20-GAT-002 and V20-DRL-001.

