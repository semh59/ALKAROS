# V15-REC-002 - Implement audited reconciliation resolution

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Execute allowed retry, accept-provider, accept-local, compensate, dismiss and escalate actions with permission and audit.

## Owned surface

- `src/Modules/Reconciliation/Resolution/**`, `tests/Modules/Reconciliation/Resolution/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Action eligibility, actor/reason, idempotency, resulting domain command and append-only evidence.

## Out of scope

- Dashboard projection and provider transport internals.

## Dependencies

- V15-REC-001,V1-IAM-002,V1-OPS-001

## Deliverables

- V15-REC-002 için production implementation veya executable test asset.
- Başarı, ret/failure ve recovery testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Unauthorized or invalid action mutates nothing; repeated action is idempotent; financial correction creates compensating record.

## Handoff

- V20-GAT-002.

