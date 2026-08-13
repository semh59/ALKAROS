# ENV-003 evidence - test matrix

Date: 2026-08-03
All runs with ALKAROS_TEST_PG_PASSWORD / ALKAROS_TEST_PG_PORT=5433 (Docker postgres:18).

## Matrix (dotnet test `<proj>` --no-build, each exit 0)

| Project | Result |
| --- | --- |
| ALKAROS.Architecture.Tests | 5/5 passed |
| ALKAROS.Idempotency.Tests | 60/60 passed |
| ALKAROS.Transactions.Tests | 25/25 passed |
| ALKAROS.SensitiveData.Tests | 23/23 passed |
| ALKAROS.Secrets.Tests | 21/21 passed |
| ALKAROS.TransactionOutboxIntegration.Tests | 11/11 passed |
| ALKAROS.Host.Tests | 55/55 passed |
| ALKAROS.Identity.Authentication.Tests | 34/34 passed |

## Solution build

Command: dotnet build ALKAROS.slnx --no-restore --warnaserror
Result: 0 warnings, 0 errors.

## Note

- tests/Host/MigrationComposition/Program/ProgramArgumentTests.cs was
  corrected on 2026-08-03: the old scenario asserted
  'Rollback refused: no rollback script declares position [001].' while the
  fixture contained no .down.sql, so fail-closed MissingDown validation
  always stopped before the rollback path. The corrected test uses a full
  up+down set and an undeclared position (002), which proves --rollback is
  forwarded into the rollback path (StartupFailed exit, expected message).
  File is owned by V1-SEC-003; this correction is part of its acceptance
  evidence and the ENV-003 matrix gate.
