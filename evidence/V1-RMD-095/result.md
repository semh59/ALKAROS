# V1-RMD-095 - WAL archiving and point-in-time recovery result

- Date: 2026-09-01
- Owner: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Approved by Semih (PO:2026-09-01): pull the WAL/PITR core forward into V1 and
  adopt the competitively calibrated RPO/RTO targets.

## Deliverable

PostgreSQL WAL archiving + point-in-time recovery for the ALKAROS compose stack.

### Configuration — `deploy/docker/postgresql.tuned.conf`

```text
wal_level = replica
archive_mode = on
archive_command = 'test ! -f /wal-archive/%f && cp %p /wal-archive/%f'
archive_timeout = 300      # segment at least every 5 min -> RPO <= 5 min
hba_file = '/etc/postgresql/pg_hba.conf'
```

- `deploy/docker/pg_hba.conf` (new): explicit HBA; `samenet` replication rows so
  `pg_basebackup` can connect from the internal compose network, every non-local
  connection still needs the `alkaros` password (`scram-sha-256`).
- `compose.yaml`: `alkaros-wal-archive` named volume mounted at `/wal-archive`; a
  root entrypoint wrapper (`install -d -o postgres -g postgres /wal-archive`)
  hands the root-created volume to the postgres user; new `basebackup` service
  under `profiles: ["ops"]`.

### Scripts (`deploy/docker/`)

| Script | Purpose |
| --- | --- |
| `basebackup.sh` | `pg_basebackup --format=tar --gzip` physical base backup + `SHA256SUMS`; relaxes the tar member mode so a later restore can read it |
| `restore-pitr.sh` | checksum-gate the base (exit 4), refuse a non-empty target (exit 2), missing base (exit 3); expand to a side data dir; match the primary's `max_connections` etc. from `pg_controldata`; write `restore_command` + `recovery_target_time`/`latest`; never touch the live cluster |
| `pitr-selfcheck.sh` | disposable-cluster end-to-end proof |

## Evidence

### `pitr-selfcheck.sh` (disposable PostgreSQL 18) — `pitr-selfcheck.log`

```text
=== PASS ===
baseline+keep rows recovered : 150
post-target rows dropped      : 50 (ids 151-200 absent)
```

Recovered to the exact `recovery_target_time`; rows written after the target are
correctly excluded.

### Live compose stack, end to end — `live-stack-pitr-e2e.log`

```text
basebackup: ok dir=alkaros_base_20260901T174716Z bytes=342125026 seconds=31
restore-pitr: base backup checksum verified
... starting point-in-time recovery to <target>
... recovery stopping before commit of transaction 16907
in_recovery=f
total_rows=2  lost_rows=0
PITR_E2E=PASS
```

`basebackup` ops service → 342 MB base backup; two "keep" rows + WAL switch +
target time captured; two "lost" rows written after the target; `restore-pitr.sh`
replayed WAL to the target into `/tmp/restored` on a spare port; the recovered
cluster has the 2 pre-target rows and 0 post-target rows.

### WAL archiving live

```text
SHOW archive_mode         -> on
SHOW archive_timeout      -> 5min
pg_stat_archiver          -> archived_count=3, failed_count=0
/wal-archive              -> 3 segments and growing
```

## RPO/RTO calibration

`docs/recovery/rpo-rto-targets.md` (surface handed over from `V0-BKP-002`)
rewritten with: a competitor benchmark table (Toast / Square / Lightspeed /
Oracle MICROS Simphony / Turkish SMB / industry guidance), the calibrated
target table, the Semih approval block, the PITR operator procedure and WAL
archive retention guidance.

| Data class | RPO | RTO | Mechanism |
| --- | --- | --- | --- |
| In-progress orders (client) | ~0 | keeps operating | offline queue + local-first sync |
| Financial, fiscal, audit | 5 min | 2 h | WAL archiving + base backup -> PITR |
| Orders, kitchen, inventory | 1 h | 4 h | hourly `pg_dump` + off-host copy |
| Settings | 24 h | 8 h | daily `pg_dump` |

## Governance

`V0-BKP-001` and `V0-BKP-002` closed as Done with an `## Onay` block; removed
from the `V0_DEFERRED_TASKS` set in `plan/GATES.md`, `plan_audit_tool.py`,
`task_scope_tool.py` and `test_task_scope.py` (11 → 9). `TRACEABILITY.md` C73.
`test_task_scope.py` surface handed from `V0-GOV-063` to `V1-GOV-066`.

## Out of scope

Streaming replication / warm standby (`V15-BKP`); encrypted off-site automation
and scheduled resumable drills (`V15-BKP-001/002`, `V20-DRL-001`). No
application code change.
