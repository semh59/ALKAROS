# V13-CST-001 - Implement customer PII boundary

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Persist minimum customer identity/tax/contact fields in a PII-owned boundary with field-level access policy.

## Owned surface

- `src/Modules/CustomerData/Profiles/**`, `tests/Modules/CustomerData/Profiles/**`, `database/migrations/V13/V13-CST-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Customer type, tax identity, contact fields, retention metadata and role-based reads.

## Out of scope

- Customer account balances and anonymization execution.

## Dependencies

- V1.3 entry gate,V0-CMP-003,V1-IAM-002

## Deliverables

- V13-CST-001 için production implementation.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Unauthorized roles cannot read protected fields; required invoice identity remains valid; optional PII is nullable/minimized.

## Handoff

- V13-CST-002, V13-ACC-001 and V13-INV-002.

