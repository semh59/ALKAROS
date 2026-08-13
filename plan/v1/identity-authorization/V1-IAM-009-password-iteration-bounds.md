# V1-IAM-009 - Independently verify password iteration bounds

- Task ID: V1-IAM-009
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

supported PBKDF2 iteration minimum/maximum sınırlarının round-trip geçtiğini, range dışı ve malformed değerlerin
fail-closed reddedildiğini bağımsız doğrulamak.

## Owned surface

- `src/Modules/Identity/Authentication/PasswordHasher.cs`
- `tests/Modules/Identity/Authentication/PasswordHasherTests.cs`
- `evidence/V1-IAM-009/**`

## In scope

- `CODE-011` için password iteration constructor/verify bounds'ını aynı invariant altında uygulamak ve boundary
  round-trip testlerini eklemek.

## Out of scope

- Owned surface dışındaki authentication, migration, project, lock veya plan dosyası değiştirmek.

## Dependencies

- V0-GOV-035
- V1-IAM-001
- V1-IAM-005

## Deliverables

- Password bound implementation diff'i, exact boundary tests ve raw transcript.

## Acceptance evidence

- Minimum/default/maximum round-trip geçer; maximum üstü constructor fail-closed reddedilir.
- Focused tests ve plan validator exit code `0` verir.

## Handoff

- V0-GOV-045
