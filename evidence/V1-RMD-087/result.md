# V1-RMD-087 - V1 go-live load baseline result

- Date: 2026-09-01
- Owner: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Target: live Compose stack `alkaros-host-1:5080` (PostgreSQL 18, single-node Docker)
- Harness: `tools/load-test/load_test.py` (zero-dep Python), run via
  `tools/load-test/run-baseline.sh` from a `python:3.12-slim` container on
  `alkaros_default`.

## Scenario

Read critical path: `GET .../catalog` (0.75) + `GET .../orders/active` (0.25).
One session per simulated terminal. `X-Forwarded-Proto: https` set.
Write/submit/payment paths deferred to `V15-PER-001`.

## Paced sweep (2 req/s per terminal) — `baseline-paced.json` / `.log`

| Terminals | RPS | Err% | p50 | p90 | p95 | p99 | max |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | 2.0 | 0.0 | 2.8 | 3.6 | 4.3 | 4.7 | 4.8 |
| 5 | 10.0 | 0.0 | 2.8 | 4.2 | 4.5 | 17.0 | 23.5 |
| 10 | 20.0 | 0.0 | 2.5 | 3.7 | 4.1 | 7.2 | 12.7 |
| 20 | 39.9 | 0.0 | 2.5 | 3.5 | 4.2 | 18.7 | 33.8 |

**20 terminals: p95 4.2 ms, p99 18.7 ms, 0 errors → MEETS `V15-PER-001`
(p95 < 500 ms, p99 < 1 s).** Latency flat across the sweep.

## Unpaced stress (closed loop) — `stress-unpaced.json` / `.log`

20 terminals, ~2437 req/s offered → 4800 served `200`, 31780 `429`, err 86.9%.
Served responses stayed fast (p50 9.5 ms, p95 21.3 ms, p99 28.6 ms). The
`terminal-read` fixed-window limiter (240/min per terminal) capped throughput at
its budget and rejected the excess; the server did not degrade.

## Commands

```
docker run --rm -v <repo>:/repo --network alkaros_default -w /repo python:3.12-slim \
  python tools/load-test/load_test.py --base-url http://alkaros-host-1:5080 \
  --login admin:*** --terminals 1,5,10,20 --duration 20 --rate 2 \
  --forwarded-proto https --json evidence/V1-RMD-087/baseline-paced.json

... --terminals 20 --duration 15 --rate 0 --json evidence/V1-RMD-087/stress-unpaced.json
```

## Notes / follow-ups (not V1 blockers)

- Full write-path + DB-lock + production-sized-data load is `V15-PER-001`
  (gated on `GATE-V14-EXIT`); this baseline feeds it.
- Limiters are fixed-window: a bursty client can spend a window budget in ~1 s.
  Steady-cadence clients never hit it. Sliding-window / token-bucket is a
  tuning candidate for `V15-PER-001`.
- The harness leaves disposable terminal GUIDs + 12 h sessions in the target DB;
  on a dev stack they expire.
