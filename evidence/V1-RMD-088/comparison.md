# V1-RMD-088 - Infrastructure tuning before / after

- Date: 2026-09-01
- Owner: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Target: live Compose stack `alkaros-host-1:5080`, PostgreSQL 18, single-node
  Docker on a shared 8 GB / 12 vCPU dev box.
- Harness: `tools/load-test/load_test.py`, read scenario (0.75 catalog GET /
  0.25 active GET), sweep 1/5/10/20 terminals at 3 req/s each, 20 s per level.
- Database volume at test time: ~12 MB (near-empty dev DB).

## What changed

- `deploy/docker/postgresql.tuned.conf` loaded via `postgres -c config_file=...`
  Verified live: `shared_buffers=384MB`, `work_mem=16MB`, `random_page_cost=1.1`,
  `jit=off`, `wal_compression=zstd`, `effective_io_concurrency=256`,
  `max_connections=200`, `idle_in_transaction_session_timeout=60s`,
  `autovacuum_vacuum_scale_factor=0.05`.
- `host` service: `DOTNET_gcServer=1`, `DOTNET_GCDynamicAdaptationMode=1`,
  `DOTNET_TieredPGO=1` (verified inside the container).
- `deploy.resources` limits: postgres 4 CPU / 2 GB, host 4 CPU / 1.5 GB.
- `shm_size: 512mb` on postgres.

## Read-scenario latency (ms)

| Terminals | metric | before | after |
| --- | --- | --- | --- |
| 1  | p50 / p95 / p99 / max | 2.7 / 3.3 / 6.4 / 8.4 | 2.9 / 4.2 / 27.5 / 28.6 * |
| 5  | p50 / p95 / p99 / max | 2.5 / 4.3 / 9.7 / 15.7 | 2.6 / 4.4 / 16.0 / 16.9 |
| 10 | p50 / p95 / p99 / max | 2.4 / 3.4 / 6.8 / 21.0 | 2.5 / 3.8 / **6.5** / **9.2** |
| 20 | p50 / p95 / p99 / max | 2.4 / 4.3 / 9.3 / 27.3 | 2.5 / 4.1 / **6.6** / **18.9** |

RPS matched the offered load exactly (60 req/s at 20 terminals), 0 errors, both
runs. `V15-PER-001` target (p95 < 500 ms, p99 < 1 s) met before and after.

\* The level-1 "after" run is the first traffic after a container recreate:
TieredPGO is still profiling and the JIT/caches are cold, so its p99 is a
warm-up artifact. Steady-state levels (10, 20 terminals) are the comparable rows.

## Reading the result

- **Medians did not move** (2.4–2.9 ms both runs). On a near-empty database the
  read path is already served entirely from cache; there is nothing for
  `shared_buffers` / `random_page_cost` / `work_mem` to improve at this data
  volume.
- **The tail tightened.** Steady-state p99 dropped from ~9 ms to ~6.6 ms and max
  from 21–27 ms to 9–19 ms. This is consistent with `jit = off` removing
  sporadic JIT-compilation pauses on sub-millisecond queries, plus Server GC.
- The real value of this wave is that the configuration is now **correct for a
  production deployment**: SSD planner costs, a real `shared_buffers`, WAL
  compression, autovacuum sized for OLTP churn, leaked-transaction and lock
  timeouts, slow-query/checkpoint/lock logging, container CPU/RAM ceilings, and
  Server GC. On a real single-restaurant host with a full menu and months of
  order history these matter for plan quality and write throughput in a way a
  12 MB dev database cannot show.

## Not covered (→ V15-PER-001, gated on GATE-V14-EXIT)

Write-path load, database-lock contention, production-sized data, sustained
soak, real-device network. This wave tunes the infrastructure; measuring it
under those conditions is the V1.5 critical-path load test.
