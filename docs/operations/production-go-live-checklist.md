# V1 production go-live checklist

> Source basis: PO:2026-09-01
> Companion docs: `deploy/docker/README.md`, `docs/recovery/backup-restore-runbook.md`,
> `docs/recovery/rpo-rto-targets.md`, `docs/qa/device-browser-test-plan.md`,
> `docs/performance/critical-path-load-v1.md`, `docs/compliance/kvkk-retention-runbook.md`

`GATE-V1-EXIT` is sealed: all 238 V1 tasks are `Done` or approved
`NotApplicable`, the full test suite is green, and the deliberate V1 scope
(no payment/fiscal) is complete. This checklist covers the operational steps
that live **outside** the repository and must be walked once per deployment
before a pilot location goes live.

Legend: **DONE** — verified in-repo · **MANUAL** — needs the physical site /
real infra · **DECISION** — needs a named business owner.

---

## 1. Physical device pass — MANUAL

Automated coverage is in place (`vanilla-clients-a11y.test.ts`, PosTerminal axe +
breakpoint tests, `device-browser-test-plan.md` matrix). The physical checklist
in `docs/qa/device-browser-test-plan.md` still has to be walked on real hardware:

- [ ] Cashier PC in kiosk mode, real receipt printer, real cash drawer kick
- [ ] Kitchen display on the real panel, real kitchen printer, reprint recovery
- [ ] Waiter phone on the site WiFi: install root CA, confirm padlock, seat a
      table, disable WiFi, enter an order, confirm
      `Çevrimdışı • İşlemler Güvenli Kuyrukta`, re-enable WiFi, confirm sync
- [ ] Waiter phone over plain `http://<lan-ip>:5080` shows the
      `Çevrimdışı mod kapalı • Güvenli bağlantı (HTTPS) gerekli` warning
- [ ] Real screen reader smoke on Cashier + PosTerminal
- [ ] 200% browser zoom reflow on every surface, no horizontal scroll
- [ ] Touch targets >= 44 px on the tablet surfaces

## 2. Secrets and TLS — DONE (repo) / MANUAL (site)

Repository state, verified 2026-09-01:

- `deploy/docker/db_password` and `deploy/docker/admin_password` are **not**
  tracked and never appear in git history; `deploy/docker/.gitignore` allows
  only the `*.example` files.
- Compose passes both through Docker **secrets** (`/run/secrets/*`), read via the
  `_FILE` convention; no secret is in the Compose environment or in logs.
- PostgreSQL 18 and Caddy images are digest-pinned.

Per-deployment, on the site host:

- [ ] `deploy/docker/db_password` — a unique 32+ char random string, file mode
      `600`, owned by the deploy user. Never reused from another environment.
- [ ] `deploy/docker/admin_password` — a unique 12-256 non-whitespace manager
      password; hand it to the manager out-of-band, then rotate after first login.
- [ ] Set `ALKAROS_PROXY_HOST` to the LAN IP / hostname the devices use **before**
      `docker compose up`. It is the SAN of Caddy's internal-CA cert for both the
      main and `display.` virtual hosts (V1-RMD-098).
- [ ] Export and install Caddy's internal root CA on every waiter device and
      customer display (steps in `deploy/docker/README.md`).
- [ ] **DECISION** — external go-live (public domain) requires an approved public
      certificate: replace `tls internal` in `deploy/docker/Caddyfile`. A LAN-only
      pilot may stay on the internal cert.

**E1 (host listened only on plain HTTP).** After the A1 frontend/backend split
(V1-RMD-098) TLS terminates only at `web` (Caddy). The `api` service is headless
HTTP on `:5080`, unpublished, and returns 400 `HTTPS_REQUIRED` to any non-loopback
request without a trusted `X-Forwarded-Proto: https`. The host-terminated HTTPS
fallback on `8444` added by V1-RMD-096 was removed with the split — the container
no longer runs a second TLS listener. **Operational consequence:** if Caddy is
down the stack is down; run a standby proxy for HA rather than relying on the app
to self-serve TLS.

- [ ] Rotation: re-write the secret file, then
      `docker compose up -d --force-recreate migrate provision api`. The DB
      password rotation also needs `ALTER ROLE alkaros WITH PASSWORD ...` on the
      server in the same window.

## 3. Restore drill — DONE (mechanism) / MANUAL (production-sized)

Re-verified 2026-09-01 against the current schema (evidence:
`evidence/V1-GOV-065/restore-drill-2026-09-01.md`):

- Disposable 500-row round trip: backup -> corrupted artifact **refused (exit 4,
  no target DB)** -> clean restore -> identical row count and data checksum.
- Live ALKAROS schema (57 tables / 14 schemas / 69 FKs / AUD-01 append-only
  trigger / 38 migrations): backup -> restore into `alkaros_restore` -> full
  object parity, `pg_restore` exit 0, target dropped after compare.

Still MANUAL before go-live:

- [ ] One drill against **production-sized** data on the **real** site host, to
      record actual `pg_dump` / `pg_restore` timings and confirm they fit the
      RTO targets in `docs/recovery/rpo-rto-targets.md` (the ~165 KB dev dataset
      restores in <1 s and proves nothing about a full restaurant's volume).
- [ ] Schedule the hourly backup cron from `docs/recovery/backup-restore-runbook.md`
      and confirm artifacts land on **off-host** durable storage.
- [ ] **DECISION** — `V0-BKP-002` numeric RPO/RTO targets still need a named
      business approver; see the competitive calibration below.

### 3a. Competitive calibration of the RPO/RTO targets

The `V0-BKP-002` table was drafted before any competitor benchmark. What the
market actually does (researched 2026-09):

| Product | Offline / local resilience | Published DB RPO/RTO | Uptime |
| --- | --- | --- | --- |
| **Toast** | Local-sync hub buffers orders + encrypted card data on-device; offline mode after 40 s; card auth practically ~24 h | none published; design goal "no order loss" via device buffer | none public |
| **Square for Restaurants** | Offline payments 24 h (declines after 72 h from first offline txn); $100 default cap; auto-sync | none published; states it may cover extended-outage losses | none public |
| **Lightspeed Restaurant** | Full offline mode, sales stored locally, auto-sync + backup on reconnect | none published | 99.9% (Lightspeed Systems SaaS) |
| **Oracle MICROS Simphony** | On-prem posting service + local DB per workstation | RTO/RPO + Target Availability defined for Production, **excluded for datacenter-loss / national emergencies** | Oracle SaaS 99.9% |
| **Turkish SMB (Adisyo, Simpra, Menulux, GoPOS)** | "Çevrimdışı mod" + "otomatik yedekleme" advertised as table stakes | none published | none public |
| **Industry guidance** | — | hourly backup "ideal", daily "acceptable"; PITR/WAL → ~5 min RPO; 3-2-1 rule | — |

Takeaways:

1. **The competitive differentiator is local-first offline resilience, not
   database RPO.** ALKAROS already matches this: WaiterPwa offline queue +
   `local-first-sync-contract` + idempotency inbox/outbox → in-progress order
   RPO ≈ 0 while the network/cloud is down. This is the parity claim and it is
   already shipped.
2. **No SMB restaurant competitor publishes a numeric DB RPO.** "Automatic
   backup" is the marketed bar. Hourly `pg_dump` (shipped) already meets or
   beats that bar for orders/kitchen/inventory.
3. **The recognised "better" tier is PITR (WAL archiving) → ~5 min RPO**, and
   that is the right target for money + fiscal + audit rows.

Calibrated targets — **approved 2026-09-01 by Semih (Founder / Product Owner)**,
now the authoritative table in `docs/recovery/rpo-rto-targets.md`:

| Data class | RPO | RTO | Mechanism | V1 status |
| --- | --- | --- | --- | --- |
| In-progress orders (client-side) | ~0 | n/a (keeps operating) | offline queue + local-first sync | **shipped** |
| Financial (bills, payments), fiscal, audit | 5 min | 2 h | PostgreSQL WAL archiving + physical base backup → PITR (`V1-RMD-095`) | **shipped** (cron + off-host copy pending) |
| Orders, kitchen, inventory, customer | 1 h | 4 h | hourly `pg_dump` + off-host copy | **shipped** (cron + off-host copy pending) |
| Settings, config | 24 h | 8 h | daily `pg_dump` | **shipped** |
| Uptime aspiration | — | — | — | 99.9% north-star; single-node V1 cannot contract to it (needs warm standby, `V15-BKP`) |

WAL archiving shipped in wave 22 (`V1-RMD-095`): `archive_mode = on` +
`archive_timeout = 300` in `postgresql.tuned.conf`, `deploy/docker/basebackup.sh`,
`deploy/docker/restore-pitr.sh`, `deploy/docker/pitr-selfcheck.sh` (proven
end to end against the live schema — see `evidence/V1-RMD-095/`).

- [ ] Schedule `docker compose -f compose.yaml -f compose.ops.yaml run --rm basebackup`
      after every migration and daily; copy each base backup **and** the growing
      `alkaros-wal-archive` volume to off-host storage (hourly for WAL).
- [ ] Prune the off-host WAL archive after each verified base backup per
      `docs/recovery/rpo-rto-targets.md` §3.

## 4. Monitoring and alerting — PARTIAL

### What the stack already exposes

| Signal | Where | Use |
| --- | --- | --- |
| Liveness/readiness | `GET /health/ready` (`api` `:5080`, `web` `:8443`) — runs `SELECT 1` against PostgreSQL, returns `{"status":"Ready"}` / 503 | external uptime check |
| Container health | Compose `healthcheck` on `postgres`, `api`, `web` (`restart: unless-stopped`) | `docker` self-heal + `docker events` |
| Operational health snapshot | `GET /api/v1/kitchen/terminals/{id}/operations/health/latest` (auth) | staff-facing status |
| Recent backups | `GET /api/v1/kitchen/terminals/{id}/operations/backups/recent` (auth) | backup freshness |
| Alert records | `AlertService` + `alerts` table (`PostgresAlertRepository`) | in-app alert feed |
| Structured logs | Host stdout, redacted by `ObservabilityRedactionHook` | `docker logs` / log shipper |

### To wire before go-live

- [ ] Point an external watchdog (UptimeKuma / Healthchecks.io / a cron + curl on
      a second box) at `https://<ALKAROS_PROXY_HOST>:8443/health/ready`, interval
      <= 1 min, alert to phone/email on 2 consecutive failures.
- [ ] Ship `docker logs` to a file or collector with rotation; alert on
      `ERROR`/`Unhandled` lines. Logs are already secret-redacted.
- [ ] Add a daily check that the newest row in `operations/backups/recent` (or the
      backup volume) is younger than 25 h; page if not.
- [ ] Disk alert on the `alkaros-postgres` and `alkaros-backups` volumes at 80%.
- [ ] Note the tuned Postgres logging already in `postgresql.tuned.conf`
      (`log_min_duration_statement=500ms`, `log_checkpoints`, `log_lock_waits`) —
      forward these to the same collector.
- [ ] **DECISION** — a Prometheus/OTLP metrics exporter is **not** in V1. If the
      operator needs dashboards/trends rather than up/down + log alerts, that is a
      `V1-OBS-001` / `V15-RUN-001` follow-up.

## 5. Load / soak — DONE (single-box) / DEFERRED (multi-node)

- Write critical path at 1M orders / 3M items, 20 concurrent terminals: submit
  p95 45.9 ms, p99 73.9 ms, 0 % errors, 0 deadlocks
  (`docs/performance/critical-path-load-v1.md`).
- [ ] **DEFERRED** — `V15-PER-001` full suite (multi-node, multi-hour soak) is
      out of V1 scope. For a single pilot restaurant the single-box headroom
      (~10x the RPO target) is sufficient; revisit before multi-store rollout.

---

## Go / no-go summary

| Area | State |
| --- | --- |
| Code + governance (`GATE-V1-EXIT`) | sealed |
| Backup/restore mechanism | verified |
| Secrets hygiene (repo) | clean |
| Physical device pass | **pending — site** |
| TLS termination (E1) | Caddy `web` only (`V1-RMD-098` A1 split); the `V1-RMD-096` host self-signed `8444` fallback was removed — Caddy outage = stack outage, run a standby proxy for HA |
| Public-domain TLS cert (if external go-live) | **pending — decision** |
| External uptime + backup-age alert | **pending — ops wiring** |
| Production-sized restore timing | **pending — site** |
| RPO/RTO numeric sign-off (`V0-BKP-002`) | approved 2026-09-01 (Semih); table in §3a and `docs/recovery/rpo-rto-targets.md` |
| Local/offline order RPO ≈ 0 (competitor parity) | shipped |
| PITR / WAL archiving (5 min RPO for money/fiscal/audit) | shipped (`V1-RMD-095`); off-host copy + cron pending |
| Off-host copy of base backups + WAL archive | **pending — ops wiring** |

A LAN-only pilot at one location can go live once the "pending — site / ops
wiring" rows are walked. The "pending — decision" row (public TLS) is only
blocking for an external or multi-store deployment.
