# ALKAROS V1 critical-path write load test

> Source basis: PDF:I.38, PDF:I.45.1, PO:2026-09-01 (Semih approved pulling this
> forward from `V15-PER-001` into V1)
> Owner task: V1-RMD-093 · Harness: `tools/load-test/load_test.py --scenario write`

The wave-14 baseline (`docs/performance/load-baseline-v1.md`) measured only the
read-dominant part of the critical path (menu view + active-order poll). This
test measures the **write** critical path — seat a table, add line items, submit
the order — under concurrent load with **production-sized background data**, and
observes PostgreSQL lock/deadlock behaviour while it runs.

## Method

- Seed (`tools/load-test/seed-loadtest.sh`, `--clean` reverses it): 600 free
  tables (`LT-####`), 400 sellable products (`LT-P####`), and **1,000,000
  background orders + 3,000,000 items** (`order_number LIKE 'LT-%'`, `table_id
  NULL`, status mostly `Completed`, `created_at` spread over 180 days) inserted
  into the live `alkaros` database — the same data volume used for the
  `V1-RMD-089` scale-index proof.
- Scenario `write`: each simulated terminal owns a disjoint slice of the 600
  seeded tables and repeats `POST .../orders/table` (seat) → 2 ×
  `POST .../orders/{id}/items` → `POST .../orders/{id}/submit`, tracking the
  server's returned `revision` between calls. Paced at 0.3 cycles/s per
  terminal (~1.2 writes/s), under the `terminal-write` limiter (120/min = 2/s).
- Target, per `V15-PER-001`: **20 concurrent terminals, order submit p95 < 500 ms,
  p99 < 1 s.**
- Lock observation: `pg_stat_database.deadlocks` sampled before/after, and
  `pg_stat_activity` polled every 3 s throughout the run for
  `wait_event_type = 'Lock'`.
- After the run, every `LT-` tagged row and the orders it produced were deleted
  (`seed-loadtest.sh --clean`); no permanent schema or data change.

## Result — 20 terminals, 40 s, 1M background orders

| Metric | Value |
| --- | --- |
| Aggregate RPS | 24.0 |
| Error rate | **0.0 %** |
| Submits completed | 240 (240 seats, 480 item adds, 240 submits) |
| Submit p50 / p90 / p95 / p99 / max | 25.8 / 38.6 / **45.9** / **73.9** / 81.0 ms |
| `pg_stat_database.deadlocks` | 0 before -> 0 after |
| Lock waiters (`pg_stat_activity`, 60 samples over 40 s) | 0 in every sample |

**MEETS the V15-PER-001 target with a ~10x margin on p95 and ~13x on p99**, at
1,000,000 background order rows (post `V1-RMD-089` scale indexes) and with zero
lock contention or deadlocks observed.

### First attempt (informational — harness artifact, not a server defect)

An earlier sweep (1/10/20 terminals, one after another against the *same* 600
tables) produced a 15.79 % error rate — but broken down by operation, every
`item` and `submit` call succeeded (240/240, 160/160); all failures were `409`
on **seat**, because level 5's terminals had already occupied ~40 tables before
level 10 and 20 reused overlapping slices. This is the server correctly
rejecting a re-seat of an already-occupied table (the `V1-RMD-090` intent
validation working as designed), not a load-test finding. The clean run above
(single 20-terminal level, fresh tables) is the reportable result.

## Verdict for V1 go-live

- The write critical path (seat / add item / submit) is fast and free of lock
  contention at 1M background order rows: no deadlocks, no observed lock waits,
  submit p99 under 100 ms at the V15-PER-001 concurrency target.
- The `V1-RMD-089` scale indexes plus the existing `FOR UPDATE` discipline in
  `SubmitOrderHandler` and `DualScreenStore.StartOrderAsync` are sufficient at
  this scale; no further write-path tuning is indicated for V1.
- Not covered here (still `V15-PER-001` / later): sustained multi-hour soak,
  production-scale *concurrent* order density (this run submits ~6 orders/s;
  a genuine dinner rush across many more physical terminals would need a wider
  sweep), payment-flow load (closed in V1), and real-device network latency.

## Reproduce

```sh
ALKAROS_DB_PASSWORD="$(tr -d '\r\n' < deploy/docker/db_password)" \
tools/load-test/seed-loadtest.sh > evidence/V1-RMD-093/loadtest-fixture.json

python tools/load-test/load_test.py --base-url http://alkaros-host-1:5080 \
  --login admin:<password> --terminals 20 --duration 40 --rate 0.3 \
  --scenario write --fixture evidence/V1-RMD-093/loadtest-fixture.json \
  --forwarded-proto https --json evidence/V1-RMD-093/write-load.json

ALKAROS_DB_PASSWORD="$(tr -d '\r\n' < deploy/docker/db_password)" \
tools/load-test/seed-loadtest.sh --clean
```
