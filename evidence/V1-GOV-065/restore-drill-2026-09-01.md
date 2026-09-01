# Restore drill re-verification — 2026-09-01

Re-ran the backup/restore path (`deploy/docker/backup.sh` + `restore.sh`) against
the current schema as part of the wave-21 go-live checklist pass. No code change;
this confirms the wave-13 mechanism still holds on the current 38-migration
schema.

## Disposable round trip (`backup-restore-selfcheck.sh`)

```
server: postgres@alkaros-pg:5432  pg_dump: 18.6
source: rows=500 data_md5=3300c8dd42d812027651f65eaa007031
backup: ok bytes=5628 sha256=e608e2aff50b28823ba83aa7a01ad6559818a216ca2e7cd5c165b51a61ac8954
negative: corrupted artifact correctly refused (exit 4, no target database)
positive: clean restore, seconds=1
target: rows=500 data_md5=3300c8dd42d812027651f65eaa007031
=== PASS ===
```

Row count and data checksum identical source vs target; a mid-byte-flipped
artifact is refused with exit 4 and no target database is created.

## Live ALKAROS schema round trip

Backup of the composed `alkaros` database, restore into `alkaros_restore`,
object-count comparison, target dropped:

```
backup: ok bytes=265924 sha256=91481e0b22db3b5f2307b65e5cd393f6db73dccffdb6178590ec624e22ec3193
restore: checksum verified
restore: ok target=alkaros_restore seconds=1
tables      src=57 dst=57
fkeys       src=69 dst=69
triggers    src=1  dst=1     (AUD-01 audit_events append-only trigger)
migrations  src=38 dst=38
REAL_SCHEMA_DRILL=PASS
cleanup: alkaros_restore dropped
```

## Still outstanding (see `docs/operations/production-go-live-checklist.md` §3)

- A drill against production-sized data on the real site host, to record actual
  `pg_dump` / `pg_restore` timings against `docs/recovery/rpo-rto-targets.md`.
- Hourly backup cron + off-host durable copy.
- `V0-BKP-002` numeric RPO/RTO sign-off by a named business approver.
