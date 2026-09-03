# RPO and RTO Targets

> **Owner task:** V1-RMD-095 (surface handed over from V0-BKP-002 by PO:2026-09-01)
> **Status:** Approved
> **Approver:** Semih — Founder / Product Owner
> **Approval date:** 2026-09-01
> **Source basis:** PDF:II.2.23, PDF:III.25, EXT:POSTGRESQL-18.4, PO:2026-09-01
> **Original draft:** 2026-07-30 (V0-BKP-002)

## 1. Competitive basis

These targets were calibrated against what restaurant POS competitors actually
do (researched 2026-09), not set in a vacuum.

| Product | Offline / local resilience | Published DB RPO/RTO | Uptime |
| --- | --- | --- | --- |
| **Toast** | Local-sync hub buffers orders + encrypted card data on-device; offline mode after 40 s; card auth practically ~24 h | none published; design goal "no order loss" via device buffer | none public |
| **Square for Restaurants** | Offline payments 24 h (declines after 72 h from first offline txn); $100 default cap; auto-sync on reconnect | none published; states it may cover extended-outage losses | none public |
| **Lightspeed Restaurant** | Full offline mode, sales stored locally, auto-sync + backup on reconnect | none published | 99.9% (Lightspeed Systems SaaS) |
| **Oracle MICROS Simphony** | On-prem posting service + local DB per workstation | RTO/RPO + Target Availability defined for Production, **excluded for datacenter-loss / national emergencies** | Oracle SaaS 99.9% |
| **Turkish SMB (Adisyo, Simpra, Menulux, GoPOS)** | "Çevrimdışı mod" + "otomatik yedekleme" advertised as table stakes | none published | none public |
| **Industry guidance** | — | hourly backup "ideal", daily "acceptable"; PITR/WAL → ~5 min RPO; 3-2-1 rule | — |

Conclusions:

1. **The competitive differentiator is local-first offline resilience, not
   database RPO.** ALKAROS matches this: the WaiterPwa offline queue, the
   `local-first-sync-contract` and the idempotency inbox/outbox give an
   in-progress-order RPO of ~0 while the network or cloud is down. Already
   shipped.
2. **No SMB restaurant competitor publishes a numeric DB RPO.** "Automatic
   backup" is the marketed bar; the shipped hourly `pg_dump` meets it for
   orders / kitchen / inventory.
3. **The recognised better tier is PITR (WAL archiving) → ~5 min RPO**, and that
   is the target for the money / fiscal / audit rows. Shipped in V1 by
   `V1-RMD-095` (WAL archiving + `basebackup.sh` + `restore-pitr.sh`).

## 2. Targets

| Data class | RPO (max data loss) | RTO (max downtime) | Mechanism | Priority |
| --- | --- | --- | --- | --- |
| In-progress orders (client side) | ~0 | n/a — POS keeps operating offline | WaiterPwa offline queue + local-first sync + idempotency inbox/outbox | 1 |
| Financial (bills, payments), fiscal documents, audit logs | 5 minutes | 2 hours | PostgreSQL WAL archiving (`archive_timeout = 300`) + physical base backup → point-in-time recovery (`restore-pitr.sh`) | 1 |
| Orders, kitchen, inventory, customer accounts | 1 hour | 4 hours | hourly `pg_dump` (`backup.sh`) + off-host copy | 2 |
| Settings, config | 24 hours | 8 hours | daily `pg_dump` | 3 |
| Service availability (aspiration) | — | — | single-node V1 cannot contract to a number; 99.9% is the north-star matched by adding a warm standby in `V15-BKP` | — |

### Rules

1. RTO is measured from an actual restore, not asserted. The V1 restore path is
   proven by `deploy/docker/backup-restore-selfcheck.sh` (logical) and
   `deploy/docker/pitr-selfcheck.sh` (point-in-time), plus the live-schema
   drills in `evidence/V1-RMD-095/` and `evidence/V1-GOV-065/`.
2. RPO ≈ 0 for fiscal/audit (the original V0-BKP-002 draft) needs synchronous
   streaming replication. That is a `V15-BKP` item; V1 accepts the 5-minute
   WAL-archive RPO for the single-store pilot.
3. Off-host copy: the WAL archive volume and each base backup must be copied to
   storage on a different machine, at least hourly for WAL, after every base
   backup. 3-2-1: live DB + local backup volume + off-host copy.
4. PII in backups follows the retention/disposal rules of the `V0-CMP-003`
   inventory; do not keep customer-identifying data in backups past the
   approved window.
5. Restore priority (1 → 3) is the order in which data classes are recovered.

## 3. Point-in-time recovery procedure (V1)

WAL archiving is configured in `deploy/docker/postgresql.tuned.conf`
(`archive_mode = on`, `archive_command` copies each segment to the
`alkaros-wal-archive` volume, `archive_timeout = 300` forces a segment at least
every 5 minutes). `deploy/docker/pg_hba.conf` allows the replication connection
`pg_basebackup` needs.

1. **Base backup** — after every schema migration and on a daily schedule:

   ```sh
   docker compose -f compose.yaml -f compose.ops.yaml run --rm basebackup
   ```

   Writes `alkaros-backups:/backups/base/alkaros_base_<UTC>` (tar.gz +
   `SHA256SUMS`). Copy it and the growing WAL archive off-host.

2. **Recover to a point in time** — into a side data directory, never the live one:

   ```sh
   docker compose -f compose.yaml -f compose.ops.yaml run --rm basebackup \
     /repo/deploy/docker/restore-pitr.sh /backups/base/<dir> /restore/<datadir> "2026-09-01 17:30:00+00"
   ```

   `restore-pitr.sh` checksum-gates the base (exit 4 on mismatch), refuses a
   non-empty target (exit 2), matches the primary's `max_connections` etc. from
   `pg_control`, writes `restore_command` + `recovery_target_time`, and replays
   WAL to the target. Use `latest` (or omit the time) to replay everything.

3. **Verify** the recovered cluster on a spare port as the postgres user
   (`SELECT pg_is_in_recovery()` returns `f`; check row counts and the newest
   `orders` / `reporting` timestamps).

4. **Promote** only after verification, by swapping data directories (same steps
   as `docs/recovery/backup-restore-runbook.md` → "Promoting a restored copy").

### WAL archive retention

The `alkaros-wal-archive` volume grows continuously. After each verified base
backup, WAL segments older than the second-most-recent base backup can be
pruned from the off-host copy (keep an overlap so you can always recover from at
least the previous base). Never prune segments newer than the oldest base
backup you still keep.

## 4. What is out of scope

- Streaming replication / hot standby for RPO ≈ 0 — `V15-BKP`.
- Encrypted off-site automation and scheduled resumable restore drills —
  `V15-BKP-001`, `V15-BKP-002`, `V20-DRL-001`.
- Application behaviour — unchanged; this is PostgreSQL configuration, operator
  scripts and documentation only.

## 5. Approval

- **Approver:** Semih — Founder / Product Owner
- **Date:** 2026-09-01
- **Decision:** Adopt the section 2 table. Accept for the single-store pilot
  that fiscal/audit RPO is 5 minutes (WAL archiving) rather than 0, and that
  99.9% availability is a north-star the single-node V1 does not contract to.
- **Measured evidence:** `V0-BKP-001` + `V1-RMD-086` disposable PostgreSQL 18
  round-trip; `V1-RMD-095` live end-to-end PITR transcript
  (`evidence/V1-RMD-095/`).
- **Rejected alternative:** streaming replication / warm standby for RPO ≈ 0 —
  unnecessary cost for a one-restaurant pilot; stays in `V15-BKP`.

## 6. Affected tasks

- `V15-BKP-001`, `V15-BKP-002`, `V20-DRL-001` (downstream).
- Closes `V0-BKP-001` and `V0-BKP-002` (2026-09-01, `TRACEABILITY.md` C73).
