# V1-IAM-001 - Implement user authentication

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Implement password verification, active-user checks, login/logout and secure session issuance.

## Owned surface

- `src/Modules/Identity/Authentication/**`, `tests/Modules/Identity/Authentication/**`, `database/migrations/V1/V1-IAM-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- User credential storage, password hashing policy, login/logout and lock-safe failure responses.

## Out of scope

- Role permissions, device enrollment and password-reset workflow.

## Dependencies

- V1-FND-001

## Deliverables

- V1-IAM-001 için production implementation.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Valid login succeeds; invalid/inactive user fails without credential leakage; stored values are salted password hashes.

## Handoff

- V1-IAM-002 and V1-IAM-003.

