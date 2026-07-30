# V1-IAM-002 - Implement role and permission enforcement

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Implement roles, permissions, assignments and server-side authorization checks.

## Owned surface

- `src/Modules/Identity/Authorization/**`, `tests/Modules/Identity/Authorization/**`, `database/migrations/V1/V1-IAM-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Permission catalog, role assignments, policy evaluation and denial audit hook.

## Out of scope

- Authentication and device session lifecycle.

## Dependencies

- V1-IAM-001,V0-DAT-002

## Deliverables

- V1-IAM-002 için production implementation.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Each protected command has a named permission; denied actions make no domain mutation; permission tests cover allowed and denied actors.

## Handoff

- All command-handler tasks.

