# ALKAROS deployment infrastructure tuning

> Source basis: PDF:I.38, PDF:I.45.1, EXT:POSTGRESQL-18.4, PO:2026-09-01
> Owner task: V1-RMD-088

The stock `postgres:18` image and the default .NET runtime settings are
development defaults. This page documents the production tuning ALKAROS ships,
why each setting is set, and how to resize it for the actual deployment host.

Files:

- `deploy/docker/postgresql.tuned.conf` — the PostgreSQL config, loaded via
  `postgres -c config_file=...` in `compose.yaml`.
- `compose.yaml` — mounts the config, sets `shm_size`, `.NET` runtime env vars
  and `deploy.resources` limits on `postgres` and `api`.

## PostgreSQL

| Setting | Default | Tuned | Why |
| --- | --- | --- | --- |
| `max_connections` | 100 | 200 | Host runs two Npgsql data sources, each pooling up to 100, plus psql / backup jobs. |
| `shared_buffers` | 128 MB | 384 MB | PG's own page cache. ~25% of RAM on a dedicated host. **Scale with RAM** (see table below). `shm_size` in compose must be ≥ this. |
| `effective_cache_size` | 4 GB | 2 GB | Planner hint for OS+PG cache (not an allocation). Set to 50–75% of RAM. Lower here because the dev box is shared; **raise on dedicated hardware.** |
| `work_mem` | 4 MB | 16 MB | Per sort/hash. 4 MB spills small sorts to temp files. |
| `maintenance_work_mem` | 64 MB | 256 MB | Faster vacuum and index builds. |
| `random_page_cost` | 4.0 | 1.1 | Default assumes a spinning disk and biases the planner to seq scans. ALKAROS runs on SSD/NVMe; 1.1 gives correct plans for its index lookups. |
| `effective_io_concurrency` | 16 | 256 | SSD can service many concurrent reads. |
| `jit` | on | **off** | LLVM JIT helps long analytical queries. ALKAROS critical-path queries are sub-millisecond point lookups where JIT compilation is pure overhead. |
| `wal_compression` | off | zstd | Compress full-page images → less WAL volume and disk I/O per commit. |
| `checkpoint_completion_target` | 0.9 | 0.9 | Spread the checkpoint flush so it does not stall order writes at the top of the rush. |
| `max_wal_size` / `min_wal_size` | 1 GB / 80 MB | 4 GB / 1 GB | Fewer, larger checkpoints under write load. |
| `synchronous_commit` | on | **on (unchanged)** | A POS must not lose a committed order on a power cut. Durability stays on; the checkpoint spread is what protects latency. **Do not set to `off`.** |
| `autovacuum_vacuum_scale_factor` | 0.2 | 0.05 | `orders` / `kitchen_tickets` / `bills` churn constantly; vacuum sooner to limit bloat. |
| `autovacuum_analyze_scale_factor` | 0.1 | 0.02 | Keep planner statistics fresh on churny tables. |
| `idle_in_transaction_session_timeout` | 0 | 60 s | Kill a leaked / crashed-terminal transaction so it cannot pin vacuum or hold row locks. |
| `lock_timeout` | 0 | 10 s | Fail a statement that waits too long for a lock instead of piling up. |
| `track_io_timing`, `track_wal_io_timing` | off | on | Needed for meaningful `pg_stat_*` I/O analysis. |
| `log_min_duration_statement` | -1 | 500 ms | Slow-query log for operations. |
| `log_checkpoints`, `log_lock_waits`, `log_autovacuum_min_duration` | mixed | on / 0 | Operational visibility into checkpoints, lock contention, vacuum work. |

### Resizing for the deployment host

`shared_buffers` and `effective_cache_size` are the two to change. Edit
`deploy/docker/postgresql.tuned.conf`, and raise `shm_size` in `compose.yaml`
to match `shared_buffers`.

| Host RAM (dedicated) | shared_buffers | effective_cache_size | compose shm_size |
| --- | --- | --- | --- |
| 4 GB | 1 GB | 2.5 GB | 1g |
| 8 GB | 2 GB | 5 GB | 2g |
| 16 GB | 4 GB | 11 GB | 4g |

The shipped file uses the smallest safe values (384 MB / 2 GB) so it starts on a
shared or small box without OOM; a real single-restaurant mini-PC should use the
row for its RAM.

## .NET runtime (`api` service env)

| Env var | Value | Why |
| --- | --- | --- |
| `DOTNET_gcServer` | 1 | Server GC: per-core heaps and background collection, suited to a throughput HTTP service. (The ASP.NET SDK usually defaults this on; set explicitly so it is not left to chance.) |
| `DOTNET_GCDynamicAdaptationMode` | 1 | Let the GC right-size the heap to the container memory limit instead of the host's total RAM. |
| `DOTNET_TieredPGO` | 1 | Profile-guided re-JIT of hot methods after warmup. |

## Container resource limits (`deploy.resources`)

| Service | CPU limit | Memory limit | Reservation |
| --- | --- | --- | --- |
| `postgres` | 4.0 | 2 GB | 1.0 CPU / 768 MB |
| `api` | 4.0 | 1.5 GB | 1.0 CPU / 512 MB |

Limits give a predictable ceiling and let `DOTNET_GCDynamicAdaptationMode` size
the heap correctly. Raise both to match the deployment host; they are a safety
ceiling, not a target.

## Before / after measurement

Read scenario (`tools/load-test/load_test.py`, 0.75 catalog GET / 0.25 active
GET), sweep 1/5/10/20 terminals at 3 req/s each, against the live stack.

See `evidence/V1-RMD-088/before-tuning.json` and `after-tuning.json` for the raw
runs and `evidence/V1-RMD-088/comparison.md` for the diff.

At this data volume (near-empty dev database, ~12 MB) the read path is already
served entirely from cache, so the wall-clock numbers move little — the point of
this wave is that the configuration is **correct and deployment-ready** so the
database is not on toy defaults when the restaurant runs it with a real menu and
months of order history. `jit = off` is the one setting expected to trim the
sub-millisecond tail even on the small dataset.

Write-path load, database-lock analysis and production-sized data remain
`V15-PER-001` scope (gated on `GATE-V14-EXIT`).
