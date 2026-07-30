# PostgreSQL Backup and Restore Proof

> **Task:** V0-BKP-001
> **Status:** InProgress
> **Assignee:** codex-v0-bkp-001
> **Work type:** validation
> **Source basis:** PDF:II.2.23, PDF:III.25, EXT:POSTGRESQL-18.4
> **Date:** 2026-07-30

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

## 4. Blocker
- Actual PostgreSQL 18 instance required for execution
- This evidence package documents the procedure; execution requires disposable instance

## 5. Affected Tasks
- V1-OPS-002, V15-BKP-001, V15-BKP-002