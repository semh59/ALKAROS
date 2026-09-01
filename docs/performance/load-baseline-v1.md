# ALKAROS V1 go-live load baseline

> Source basis: PDF:I.38, PDF:I.45.1, PO:2026-09-01
> Owner task: V1-RMD-087 · Harness: `tools/load-test/load_test.py`

This is a **go-live baseline**, not the full critical-path load test. The full
suite (database-lock analysis, resource ceilings, write + payment paths) is
`V15-PER-001`, which is gated on `GATE-V14-EXIT`. This baseline exists to
de-risk the V1 restaurant go-live by measuring the read-dominant load every
terminal generates continuously, against the concurrency target `V15-PER-001`
names: **20 concurrent terminals, p95 < 500 ms, p99 < 1 s.**

## Method

- Harness: `tools/load-test/load_test.py` — zero-dependency Python
  (`http.client` + `concurrent.futures`). One thread per simulated terminal,
  each with its own terminal GUID and its own session cookie (matches the
  per-`{client,terminal}` rate-limit partitioning in `DualScreenApplication`).
- Runner: `tools/load-test/run-baseline.sh` launches the harness from a
  throwaway `python:3.12-slim` container on the stack's Docker network so the
  generator does not compete with the app for CPU.
- Target: the live Compose stack (`alkaros-host-1:5080`), PostgreSQL 18,
  single-node Docker on the development machine. Requests carry
  `X-Forwarded-Proto: https` as Caddy would.
- Scenario `read`: weighted `GET /api/v1/terminals/{tid}/catalog` (0.75) and
  `GET /api/v1/terminals/{tid}/orders/active` (0.25) — menu view and active-order
  poll, the two operations every terminal runs on a timer.
- Write path (`/orders/table`, `/orders/.../submit`, `terminal-write` limiter
  120/min) needs seeded tables and products and is deferred to `V15-PER-001`.
  Payment is closed in V1.

## Rate-limit configuration (measured from code + confirmed by the stress run)

| Policy | Limit | Partition | Applies to |
| --- | --- | --- | --- |
| `login` | 10 / min | client | `POST /api/v1/auth/login` |
| `terminal-read` | 240 / min | {client, terminalId} | catalog GET, active GET, runtime-config |
| `terminal-write` | 120 / min | {client, terminalId} | order create / item / submit / table draft |

All fixed-window, 1-minute window, `QueueLimit = 0` (excess is rejected, not
queued). 240/min = 4 req/s per terminal — well above a real POS polling cadence.

## Results

### Paced sweep — realistic cadence (2 requests/sec per terminal)

`--terminals 1,5,10,20 --duration 20 --rate 2`
Raw: `evidence/V1-RMD-087/baseline-paced.json` / `.log`

| Terminals | Aggregate RPS | Error % | p50 ms | p90 ms | p95 ms | p99 ms | max ms |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | 2.0 | 0.0 | 2.8 | 3.6 | 4.3 | 4.7 | 4.8 |
| 5 | 10.0 | 0.0 | 2.8 | 4.2 | 4.5 | 17.0 | 23.5 |
| 10 | 20.0 | 0.0 | 2.5 | 3.7 | 4.1 | 7.2 | 12.7 |
| **20** | **39.9** | **0.0** | **2.5** | **3.5** | **4.2** | **18.7** | **33.8** |

**At 20 terminals: p95 = 4.2 ms, p99 = 18.7 ms, 0 errors.** The `V15-PER-001`
target (p95 < 500 ms, p99 < 1 s) is met with roughly a 100× / 50× margin.
Latency is flat from 1 to 20 terminals — the read path is not contended at this
level on this hardware.

### Unpaced stress — runaway client (closed loop, no think time)

`--terminals 20 --duration 15 --rate 0`
Raw: `evidence/V1-RMD-087/stress-unpaced.json` / `.log`

| Terminals | Offered RPS | Served 200 | 429 | Error % | p50 ms | p95 ms | p99 ms | max ms |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 20 | 2437 | 4800 | 31780 | 86.9 | 9.5 | 21.3 | 28.6 | 65.4 |

Under ~2400 req/s of abusive load the limiter capped throughput at the
configured budget (20 terminals × 240/min ≈ 4800 successful responses/minute) and
rejected the rest with **HTTP 429**. Crucially, the served `200` responses stayed
fast (p95 21 ms, p99 29 ms): **the limiter protects the server rather than
letting it degrade.**

## Verdict for V1 go-live

- The V1 read critical path comfortably meets the 20-terminal latency target on a
  single-node deployment. No read-path tuning is required for go-live.
- The rate limiter is correctly placed and its budget (4 req/s per terminal
  read, 2 req/s write) is generous versus real usage; legitimate traffic never
  hits it.
- Not covered here (→ `V15-PER-001`): write + submit path under concurrency,
  database lock contention on `orders` / `reporting`, behaviour with
  production-sized catalog and order history, and multi-node.
- Minor note for `V15-PER-001` / tuning: the limiters are **fixed-window**, so a
  bursty client can spend a whole window's budget in the first second and then be
  blocked for the rest of the minute. A steady-cadence client never sees this.
  Consider sliding-window or token-bucket if bursty terminals appear in the field.

## Reproduce

```sh
ALKAROS_ADMIN_PASSWORD="$(tr -d '\r\n' < deploy/docker/admin_password)" \
OUT_DIR=evidence/V1-RMD-087 \
tools/load-test/run-baseline.sh
```

The harness creates disposable terminal GUIDs and 12-hour sessions in the target
database; against a dev stack these simply expire.
