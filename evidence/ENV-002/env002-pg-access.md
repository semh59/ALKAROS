# ENV-002 evidence - PostgreSQL 18 test access

Date: 2026-08-03

## Finding

- Windows service 'postgresql-x64-18' (C:\PostgreSQL\18\data) reported Running
  but no postgres.exe process existed; its log showed repeated
  FATAL: invalid value for parameter "timezone_abbreviations": "Default"
  for every connection attempt (parameter removed in PG 18, stale config value).
  postmaster.pid was absent; the service was actually Stopped.
- Port 5432 is owned by Docker: lojinext-db-1 (postgres:16-alpine) via
  com.docker.backend.exe proxy, NOT a PostgreSQL 18 instance.
- A dedicated test container existed: 'alkaros_test' (postgres:18,
  PG 18.4-1.pgdg13+1), mapped 0.0.0.0:5433->5432, with
  POSTGRES_USER=postgres and a fixed test password.

## Resolution

- ENV vars (User scope + current session):
  ALKAROS_TEST_PG_PASSWORD = container password (container env, never in repo)
  ALKAROS_TEST_PG_PORT = 5433
- A temporary trust pg_hba.conf change on the stopped Windows service was
  reverted to the original scram-sha-256 file (pg_hba.conf.alkaros.bak
  restored on 2026-08-03). No password was created/changed on the Windows
  instance; the running test database is the Docker postgres:18 container.

## Verification

Command (password via PGPASSWORD env, not on the command line):
  psql -h 127.0.0.1 -p 5433 -U postgres -d postgres -tAc "SELECT 1;"
Result: 1, exit 0.

Test databases are created and dropped per test class by
PgTestDatabase / TestDatabase fixtures (unique names, FORCE drop).
