# V13-QNB-001 - Implement QNB registered-user query

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Query and cache time-bounded e-Fatura registration status using the validated QNB contract.

## Owned surface

- `src/Modules/Invoicing/Qnb/RegisteredUser/**`, `tests/Modules/Invoicing/Qnb/RegisteredUser/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Authentication, query mapping, cache expiry, provider error and stale-cache policy.

## Out of scope

- Invoice submission and incoming invoice retrieval.

## Dependencies

- V0-QNB-001,V13-CST-001

## Deliverables

- V13-QNB-001 için production implementation.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Contract tests cover registered/unregistered/error; expired cache does not silently select document type.

## Handoff

- V13-QNB-002.

