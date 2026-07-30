# V15-SEC-002 - Implement identity abuse protections

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Add login throttling, lockout policy, session rotation and administrative revocation.

## Owned surface

- `src/Modules/Security/IdentityHardening/**`, `tests/Modules/Security/IdentityHardening/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Brute-force controls, token rotation, revoke-all, suspicious login audit and safe recovery.

## Out of scope

- MFA unless separately approved and provider secret storage.

## Dependencies

- V1-IAM-001,V1-IAM-003,V15-SEC-001

## Deliverables

- V15-SEC-002 için production implementation veya executable test asset.
- Başarı, ret/failure ve recovery testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Automated abuse tests trigger limits without locking unrelated users; revoked sessions fail immediately.

## Handoff

- V20-SEC-001.

