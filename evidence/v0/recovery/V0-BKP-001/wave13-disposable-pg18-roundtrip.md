# V1-RMD-086 - Backup / restore round-trip evidence

- Date: 2026-09-01
- Owner: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Environment: disposable PostgreSQL 18 in Docker (`postgres:18`, digest pinned),
  `pg_dump` / `pg_restore` 18.6. This is the stable disposable PostgreSQL 18
  environment that `V0-BKP-001` was blocked waiting for (the earlier blocker was
  a Windows second-instance shared-memory error 487).

## Deliverables under test

- `deploy/docker/backup.sh` - `pg_dump --format=custom` + SHA-256 sidecar.
- `deploy/docker/restore.sh` - checksum-gated `pg_restore` into a clean target DB.
- `deploy/docker/backup-restore-selfcheck.sh` - self-contained round-trip proof.
- `compose.yaml` `backup` service under the `ops` profile.

## 1. Self-check script (`backup-restore-selfcheck.sh`)

Command:

```
docker run --rm -v <repo>:/repo:ro --network alkaros-test \
  -e ALKAROS_DB_HOST=alkaros-pg -e ALKAROS_DB_USER=postgres -e ALKAROS_DB_PASSWORD=*** \
  postgres:18 sh /repo/deploy/docker/backup-restore-selfcheck.sh
```

Transcript:

```
=== ALKAROS backup/restore round-trip self-check ===
server: postgres@alkaros-pg:5432  pg_dump: 18.6-1.pgdg13+2)
source: rows=500 data_md5=3300c8dd42d812027651f65eaa007031
--- backup ---
backup: ok artifact=alkaros_bkp_selfcheck_src_20260901T074246Z.dump bytes=5623 seconds=0 \
        sha256=95e218d564e2d23bc827c4ae06e9d8c9a767bbbe76d9253da3007ee0ca45b767
--- negative: corrupted artifact ---
restore: CHECKSUM MISMATCH expected=95e218d5... actual=48bcf991...; refusing to restore
negative: corrupted artifact correctly refused (exit 4, no target database)
--- positive: clean restore ---
restore: checksum verified sha256=95e218d564e2d23bc827c4ae06e9d8c9a767bbbe76d9253da3007ee0ca45b767
restore: ok target=bkp_selfcheck_dst seconds=1
target: rows=500 data_md5=3300c8dd42d812027651f65eaa007031
=== PASS ===
rows             : 500 (src) == 500 (dst)
data md5         : 3300c8dd42d812027651f65eaa007031 (src) == 3300c8dd42d812027651f65eaa007031 (dst)
restore seconds  : 1
corrupted artifact: refused with exit 4, no target database created
```

Exit code: `0`.

Assertions proven:

| Assertion | Result |
| --- | --- |
| Seeded 500-row table survives backup + restore into a fresh DB | row count 500 == 500 |
| Restored data is byte-identical | data md5 `3300c8dd...` == `3300c8dd...` |
| A corrupted artifact is refused before `pg_restore` runs | exit 4, target DB never created |
| Checksum sidecar is mandatory | missing sidecar -> exit 3 (covered by `restore.sh` guard) |

## 2. Real ALKAROS schema (`real-schema-check.log`)

`backup.sh` + `restore.sh` run against the live migrated `alkaros` database
(`alkaros-postgres-1`, PostgreSQL 18, 57 user tables across every module schema):

```
backup: ok artifact=alkaros_alkaros_20260901T074314Z.dump bytes=169159 seconds=0 \
        sha256=73c2375e5e69ba422f4e120ecc25ba6ac42b2ec04ab71e4a1968e3fcfecad75d
restore: checksum verified sha256=73c2375e...
restore: ok target=alkaros_restore seconds=1
user tables: src=57 dst=57
REAL_SCHEMA_PARITY_OK
```

Exit code: `0`. `pg_restore` completed with `--exit-on-error` and no errors.

## 3. Compose `ops` profile end to end

```
docker compose --profile ops run --rm backup
  -> backup: ok artifact=alkaros_alkaros_20260901T074342Z.dump bytes=169159 \
     sha256=2c99c6bebb1693509c1fb8d4030f5f20c71308d102b0d7c486feb51e10d0b892
     (artifact + .sha256 written to the alkaros-backups volume)

docker compose --profile ops run --rm backup /repo/deploy/docker/restore.sh \
     /backups/alkaros_alkaros_20260901T074342Z.dump alkaros_restore
  -> restore: checksum verified
  -> restore: ok target=alkaros_restore seconds=0
  -> restored database: 57 user tables
```

Both exit `0`. (pg_dump custom-format archives embed a timestamp, so the same
database produces a different artifact hash each run; the sidecar guarantees
integrity of each individual artifact for its own restore.)

## Measured RPO / RTO inputs (for V0-BKP-002)

- Backup duration: < 1 s for the current ~165 KB dataset; scales with data volume.
- Restore duration: 0-1 s for the current dataset.
- RPO is bounded by backup frequency, which is an operator decision (this task
  ships the on-demand mechanism, not a schedule). A cron entry running
  `docker compose --profile ops run --rm backup` every N minutes makes RPO ~= N.
- These numbers are inputs for the named-approver RPO/RTO decision in
  `V0-BKP-002`; this task does not approve targets.

## Relationship to V0-BKP-001

`V0-BKP-001` is `Blocked` on "a stable disposable PostgreSQL 18 environment".
That environment now exists (`docker run postgres:18` + `alkaros-test` network),
and `backup-restore-selfcheck.sh` reproduces the full seeded-record / checksum /
corruption-rejection / clean-restore transcript on demand. v0 governance can lift
the block citing this evidence; this V1 task does not change the v0 task status.
