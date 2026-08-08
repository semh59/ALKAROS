# V1-IAM-004 - Make authentication lockout concurrency safe

- Task ID: V1-IAM-004
- Status: InProgress
- Assignee: opencode-v1-iam-004
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:II.2.1
- PDF:III.3.1
- CORR:C34

## Goal

Paralel basarisiz login denemelerinin failure counter'ini kaybetmesini
engellemek ve lockout esigini atomik olarak uygulamak.

## Owned surface

- `src/Modules/Identity/Authentication/IUserStore.cs`
- `src/Modules/Identity/Authentication/PostgresUserStore.cs`
- `tests/Modules/Identity/Authentication/PostgresUserStoreTests.cs`
- `tests/Modules/Identity/Authentication/Fixtures/AuthTestDatabase.cs`
- `evidence/V1-IAM-004/**`
- AuthenticationService.cs ve AuthenticationServiceTests.cs sahipliği
  V1-IAM-005'e devredilmiştir (C42); bu görev artık bu path'leri yazamaz.

## In scope

- Store contractini atomik failed-attempt increment ve lockout sonucunu
  dondurecek sekilde degistirmek.
- PostgreSQL'de tek UPDATE/RETURNING veya esdeger satir-duzeyi atomik islem
  kullanmak.
- Paralel yanlis parola denemeleri, lockout esigi, locked account ve basarili
  login reset testleri.

## Out of scope

- Role/permission, device session, password reset, MFA veya password policy
  degisikligi.

## Dependencies

- V0-GOV-003
- V1-FND-012
- V1-IAM-001

## Deliverables

- Atomik lockout persistence davranisi ve PostgreSQL concurrency integration
  testleri.

## Acceptance evidence

- Eszamanli hatali denemeler basarisiz giris sayacini kaybetmez ve kilidi kurar.
- Esik gecildiginde lockout atomik olarak atanir; basarili login lockout
  aktif degilken counter'i sifirlar.
- Ilgili testler exit code 0 verir.

## Handoff

- V1-IAM-002
- V1-IAM-003
