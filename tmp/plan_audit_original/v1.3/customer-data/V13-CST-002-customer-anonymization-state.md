# V13-CST-002 - Implement customer anonymization state transitions

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Implement request, retention-blocked, pending and anonymized states without deleting legally retained financial references.

## Owned surface

- `src/Modules/CustomerData/AnonymizationState/**`, `tests/Modules/CustomerData/AnonymizationState/**`, `database/migrations/V13/V13-CST-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- State machine, retention check, idempotent field replacement and audit event.

## Out of scope

- Cross-module payload cleanup and scheduled retention execution.

## Dependencies

- V13-CST-001,V1-OPS-001,V0-DOM-001

## Deliverables

- V13-CST-002 için production implementation.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Repeated anonymization is stable; retention-blocked request preserves PII and records reason; allowed request removes configured fields only.

## Handoff

- V15-KVK-001 and V15-KVK-002.

