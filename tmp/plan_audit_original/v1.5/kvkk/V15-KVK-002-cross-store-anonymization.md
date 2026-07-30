# V15-KVK-002 - Implement cross-store anonymization

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Remove or tokenize approved PII from customer, orders, provider payloads and audit views while preserving legal references.

## Owned surface

- `src/Modules/Privacy/Anonymization/**`, `tests/Modules/Privacy/Anonymization/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Per-field action, referential integrity, searchable token policy, audit entry and failure rollback.

## Out of scope

- Retention scheduling and encryption key rotation.

## Dependencies

- V15-KVK-001,V13-CST-002,V15-SEC-003,V1-OPS-001

## Deliverables

- V15-KVK-002 için production implementation veya executable test asset.
- Başarı, ret/failure ve recovery testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Seeded subject data disappears from every in-scope store after one transaction/workflow; financial totals and legal IDs remain valid.

## Handoff

- V20-CMP-001.
