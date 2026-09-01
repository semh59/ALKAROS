# V1-RMD-093 - V1 critical-path write load test result

- Date: 2026-09-01
- Owner: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Approved by Semih (PO:2026-09-01) to pull the V1-relevant core of `V15-PER-001`
  forward into V1.

## Seed

`tools/load-test/seed-loadtest.sh` into the live `alkaros` database:
600 tables (`LT-####`), 400 products (`LT-P####`), **1,000,000 orders +
3,000,000 items** (`order_number LIKE 'LT-%'`, spread over 180 days). Verified
counts: `tables=600 products=400 orders=1000000 items=3000000`.

## Write-path load (`tools/load-test/load_test.py --scenario write`)

Clean run: 20 terminals, 40 s, rate 0.3 cycles/s/terminal (seat -> 2 items ->
submit), fresh 600-table pool.

```
--- level: 20 terminal(s), 40s ---
rps=24.0 err%=0.0 submit p50=25.8 p90=38.6 p95=45.9 p99=73.9 max=81.0 n=240
statuses={'201': 240, '200': 720}
V15-PER-001 target at 20 terminals (submit_latency_ms): MEETS
(p95=45.9ms p99=73.9ms err%=0.0)
```

240/240 submits succeeded. **Meets the 20-terminal / p95<500ms / p99<1s target
with a ~10x / ~13x margin**, at 1M background order rows.

## Lock / deadlock observation

- `pg_stat_database.deadlocks`: 0 before, 0 after the run.
- `pg_stat_activity` polled every 3 s for the full 40 s run (`lock-poll.log`,
  60 samples): **0 lock waiters in every sample.**

## First attempt — informational, harness artifact

An earlier 1/10/20-terminal sweep against the same un-reset table pool showed a
15.79% error rate at 20 terminals, entirely on `seat` (409 Conflict) because
level 5/10's terminals had already occupied tables that level 20's slice
overlapped. `item` and `submit` calls were 100% successful in every level of
that run too (`seat_latency_ms`/`submit_latency_ms` breakdown in
`write-load.json`). This is `V1-RMD-090`'s intent validation correctly
rejecting a re-seat of an occupied table — not a defect. See
`docs/performance/critical-path-load-v1.md` for the full breakdown.

## Cleanup

All `LT-`/`LT-P` tagged rows, the kitchen tickets and terminal bindings the
write test produced, and their cascaded children were deleted
(`cleanup.log`). Verified restored to the exact pre-seed state:

```
LT tables=0 LT products=0 LT orders=0
total tables=3 total products=1 total orders=2
```

No permanent schema or data change.

## Files

- `evidence/V1-RMD-093/seed.log`, `write-load.log`, `write-load.json`,
  `lock-poll.log`, `locks.log`, `cleanup.log`, `loadtest-fixture.json`.
- `docs/performance/critical-path-load-v1.md` — full write-up.
