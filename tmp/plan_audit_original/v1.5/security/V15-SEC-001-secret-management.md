# V15-SEC-001 - Implement secret management boundary

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Move provider credentials, encryption keys and signing secrets out of code and general settings storage.

## Owned surface

- `src/Modules/Security/Secrets/**`, `tests/Modules/Security/Secrets/**`, `deployment/secrets/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Secret provider abstraction, least-privilege access, rotation, startup failure and redacted diagnostics.

## Out of scope

- User password hashing and payload encryption policies.

## Dependencies

- V1.5 entry gate,V0-CMP-003

## Deliverables

- V15-SEC-001 için production implementation veya executable test asset.
- Başarı, ret/failure ve recovery testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Repository/database/general logs contain no raw secret; rotation test changes active secret without corrupting pending work.

## Handoff

- V20-SEC-001.

