# PostgreSQL Backup and Restore Proof

> **Task:** V0-BKP-001
> **Status:** Blocked
> **Assignee:** codex-v0-bkp-001
> **Work type:** validation
> **Source basis:** PDF:II.2.23, PDF:III.25, EXT:POSTGRESQL-18.4
> **Date:** 2026-07-30
> **Updated:** 2026-08-02 — execution attempt failed; blocked on host capability

## 1. Tool Selection

- Backup: `pg_dump` (custom format, compressed)
- Restore: `pg_restore`
- Checksum: SHA-256 of backup artifact

## 2. Verification Procedure

1. Create disposable PostgreSQL 18 instance
2. Seed verification table with known data + checksum
3. Run `pg_dump` → produce backup artifact
4. Calculate SHA-256 of artifact
5. Drop database, recreate empty
6. Run `pg_restore` from artifact
7. Verify seeded data matches (checksum comparison)
8. Measure backup and restore duration

## 3. Corruption Test

- Corrupt backup artifact (flip bytes)
- Attempt restore → MUST fail with checksum mismatch
- Corrupted artifact MUST NOT produce a valid restore

## 4. Execution Attempt (2026-08-02)

### 4.1 Setup

- PostgreSQL 18.4 (msvc build) at `C:\PostgreSQL\18\bin`, local machine
- Disposable cluster initialized on port 5433 (`initdb` + `pg_ctl start`), trust auth

### 4.2 Result: BLOCKED

- Second PostgreSQL instance could not stay up reliably on this host:
  - `could not reserve shared memory region (addr=...) for child ...: error code 487`
  - `autovacuum worker (PID ...) was terminated by exception 0xC0000142`
  - Windows host shared-memory reservation for a concurrent postmaster failed;
    the cluster briefly reached `database system is ready to accept connections`
    but its worker processes were repeatedly killed, so the seeded-data backup →
    restore transcript cannot be produced on this machine
- Because acceptance requires a real command transcript with exit code 0, a
  restored checksum match and measured duration, this task is `Blocked`, not `Done`.

### 4.3 Artifacts

- `pg-attempt.log`, `pg2-attempt.log` — postmaster logs (error code 487, 0xC0000142)
- `run_bkp_test.ps1`, `run_bkp_test2.ps1` — attempt scripts (kept for reproduction)

## 5. Blocker

- A second PostgreSQL instance must run reliably (container-based PostgreSQL 18
  or a machine without the shared-memory conflict). With that instance available,
  procedure §2–§3 executes and evidence is completed.
- This evidence package documents the procedure; execution requires a
  stable disposable instance.

## 6. Affected Tasks

- V1-OPS-002, V15-BKP-001, V15-BKP-002
