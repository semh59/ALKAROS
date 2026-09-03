# ALKAROS backup and restore runbook

> Source basis: PDF:II.2.23, PDF:III.25, EXT:POSTGRESQL-18.4, PO:2026-09-01
> Owner task: V1-RMD-086

This runbook covers the **on-demand local** backup and restore of the ALKAROS
PostgreSQL database. Off-site encrypted copies and scheduled automatic restore
drills are out of scope here (`V15-BKP-001`, `V15-BKP-002`).

## What ships

| File | Purpose |
| --- | --- |
| `deploy/docker/backup.sh` | `pg_dump --format=custom` of one database + a `.sha256` sidecar |
| `deploy/docker/restore.sh` | Restore an artifact into a **clean side database**, refusing any artifact whose checksum does not match its sidecar |
| `deploy/docker/backup-restore-selfcheck.sh` | Self-contained round-trip proof against a disposable database |
| `compose.ops.yaml` service `backup` (profile `ops`) | Runs the above inside the digest-pinned `postgres:18` image so the client major version matches the server |

Backups use PostgreSQL's **custom format** (`-Fc`): compressed, and restorable
selectively and in parallel with `pg_restore`.

## Taking a backup

With the Compose stack running:

```sh
docker compose -f compose.yaml -f compose.ops.yaml run --rm backup
```

The artifact and its `.sha256` sidecar are written to the named
`alkaros-backups` volume as `alkaros_alkaros_<UTC-timestamp>.dump`. Copy them off
the host to durable storage:

```sh
docker run --rm -v alkaros_alkaros-backups:/b -v "$PWD:/out" postgres:18 \
  sh -c 'cp /b/alkaros_alkaros_*.dump /b/alkaros_alkaros_*.dump.sha256 /out/'
```

Keep the `.dump` and its `.dump.sha256` together — the sidecar is required to
restore.

### Retention

The mechanism does not delete anything. Recommended starting policy for a single
restaurant (adjust to the approved RPO/RTO in `V0-BKP-002`):

- Hourly backup during trading hours, kept 48 hours.
- One end-of-day backup kept 30 days.
- One end-of-month backup kept 12 months, stored off the restaurant premises.
- PII in old backups follows the retention/disposal rules from the `V0-CMP-003`
  inventory; do not keep customer-identifying data in backups longer than the
  approved window.

A cron entry gives you the schedule:

```cron
# every hour, on the hour
0 * * * * cd /opt/alkaros && docker compose -f compose.yaml -f compose.ops.yaml run --rm backup >> /var/log/alkaros-backup.log 2>&1
```

RPO is then approximately the cron interval.

## Restoring

`restore.sh` **never touches the live `alkaros` database.** It drops and
recreates a *separate* target database (default `alkaros_restore`) and loads the
artifact there. Promotion of a restored copy to live is a deliberate manual step.

```sh
docker compose -f compose.yaml -f compose.ops.yaml run --rm backup \
  /repo/deploy/docker/restore.sh /backups/alkaros_alkaros_<timestamp>.dump alkaros_restore
```

Behaviour:

- Missing `.sha256` sidecar -> exit **3**, nothing restored.
- Checksum mismatch (corrupted / truncated / wrong sidecar) -> exit **4**,
  the target database is **not** created, nothing restored.
- Success -> `pg_restore ... --exit-on-error` completes, exit **0**, duration
  printed.

Verify the restored copy before promoting it, e.g. row counts of key tables and
the most recent `orders` / `reporting.daily_business_days` timestamps.

### Promoting a restored copy to live

1. Stop the `api` service: `docker compose stop api`.
2. Rename databases (psql as a superuser):
   `ALTER DATABASE alkaros RENAME TO alkaros_broken_<date>;`
   `ALTER DATABASE alkaros_restore RENAME TO alkaros;`
3. Start `api`: `docker compose start api`.
4. Once confirmed healthy, drop `alkaros_broken_<date>`.

## Corruption rejection — why it matters

A backup you cannot trust is worse than no backup. `restore.sh` computes the
SHA-256 of the artifact and compares it to the sidecar before `pg_restore` is
invoked, so a bit-rotted or partially-copied file fails fast and loudly instead
of producing a silently incomplete database.

## Proof

See `evidence/V1-RMD-086/roundtrip.md`:

- 500-row seeded table: backup -> restore into a fresh database -> identical row
  count and data checksum.
- Corrupted artifact refused with exit 4, no target database created.
- Live ALKAROS schema (57 tables across all module schemas): backup -> restore ->
  table parity, `pg_restore` exit 0.
- Compose `ops` profile `backup` service: artifact + sidecar to the
  `alkaros-backups` volume, then checksum-gated restore, both exit 0.

Reproduce any time:

```sh
docker run --rm -v "<repo>:/repo:ro" --network alkaros-test \
  -e ALKAROS_DB_HOST=alkaros-pg -e ALKAROS_DB_USER=postgres -e ALKAROS_DB_PASSWORD=<pw> \
  postgres:18 sh /repo/deploy/docker/backup-restore-selfcheck.sh
```

## Measured timings (current dataset ~165 KB)

| Step | Duration |
| --- | --- |
| `pg_dump` | < 1 s |
| `pg_restore` | 0–1 s |

These scale with data volume; re-measure against production-sized data before
signing numeric RPO/RTO targets in `V0-BKP-002`.

## Relationship to V0-BKP-001 / V0-BKP-002

`V0-BKP-001` (validate PostgreSQL backup/restore tooling) was `Blocked` on the
lack of a stable disposable PostgreSQL 18 environment on Windows. That
environment now exists as a Docker `postgres:18` container, and
`backup-restore-selfcheck.sh` reproduces the required transcript (seeded record,
checksum, corruption rejection, clean restore, measured time) on demand. v0
governance can lift that block against this evidence. `V0-BKP-002` (numeric
RPO/RTO targets) still needs a named business approver and is not closed here.
