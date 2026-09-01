#!/usr/bin/env python3
"""ALKAROS V1 go-live load baseline harness (V1-RMD-087).

Zero third-party dependencies: http.client + concurrent.futures + statistics.

Model
-----
V15-PER-001 defines the critical-path target as "20 concurrent terminals,
p95 < 500 ms, p99 < 1 s" for the busy-hour scenario (menu view + order submit +
payment). Payment is closed in V1, so this baseline drives the read-dominant part
that every terminal runs continuously:

    GET /api/v1/terminals/{tid}/catalog        (menu view)      weight 0.75
    GET /api/v1/terminals/{tid}/orders/active  (active poll)    weight 0.25

Each simulated terminal has its own GUID and its own session cookie, matching the
per-terminal rate-limit partitioning in DualScreenApplication
(`terminal-read` = 240/min per {client,terminal}).

Usage
-----
    python load_test.py \
        --base-url http://alkaros-host-1:5080 \
        --login admin:<password> \
        --terminals 1,2,5,10,20 \
        --duration 20 \
        --json evidence/V1-RMD-087/run.json

The write path (seat -> add items -> submit) is exercised by `--scenario write`
with a `--fixture` file from `tools/load-test/seed-loadtest.sh` (V1-RMD-093).

"""

from __future__ import annotations

import argparse
import http.client
import json
import random
import ssl
import statistics
import sys
import threading
import time
import uuid
from concurrent.futures import ThreadPoolExecutor, as_completed
from urllib.parse import urlparse

CATALOG = "catalog"
ACTIVE = "active"
SCENARIO_WEIGHTS = {CATALOG: 0.75, ACTIVE: 0.25}


def _conn(base_url: str, insecure: bool) -> http.client.HTTPConnection:
    parts = urlparse(base_url)
    host = parts.hostname
    port = parts.port or (443 if parts.scheme == "https" else 80)
    if parts.scheme == "https":
        ctx = ssl._create_unverified_context() if insecure else ssl.create_default_context()
        return http.client.HTTPSConnection(host, port, timeout=30, context=ctx)
    return http.client.HTTPConnection(host, port, timeout=30)


def _request(conn, method, path, headers, body=None):
    start = time.perf_counter()
    conn.request(method, path, body=body, headers=headers)
    resp = conn.getresponse()
    payload = resp.read()
    elapsed_ms = (time.perf_counter() - start) * 1000.0
    return resp.status, elapsed_ms, resp.getheader("Set-Cookie"), payload


def login(base_url: str, username: str, password: str, terminal_id: str,
          forwarded_proto: str, insecure: bool) -> str:
    """Return the raw cookie header value for a terminal session."""
    conn = _conn(base_url, insecure)
    headers = {
        "Content-Type": "application/json",
        "X-Forwarded-Proto": forwarded_proto,
        "X-Forwarded-For": "10.100.0.9",
    }
    body = json.dumps({
        "terminalId": terminal_id,
        "username": username,
        "password": password,
    })
    status, _, set_cookie, payload = _request(conn, "POST", "/api/v1/auth/login", headers, body)
    conn.close()
    if status != 200:
        raise RuntimeError(f"login failed for {terminal_id}: HTTP {status} {payload[:200]!r}")
    if not set_cookie:
        raise RuntimeError(f"login for {terminal_id} returned no Set-Cookie")
    # Collapse every Set-Cookie name=value pair into one Cookie header.
    jar = []
    for chunk in set_cookie.split("\n"):
        first = chunk.split(";", 1)[0].strip()
        if first and "=" in first:
            jar.append(first)
    return "; ".join(jar)


def bootstrap_terminals(base_url, username, password, count, forwarded_proto,
                        insecure, login_budget_per_min=9):
    """Log in `count` terminals, batching to respect the 10/min login limiter."""
    terminals = [str(uuid.uuid4()) for _ in range(count)]
    sessions = {}
    for i, tid in enumerate(terminals):
        if i and i % login_budget_per_min == 0:
            wait = 61
            print(f"  login limiter: sleeping {wait}s after {i} logins", flush=True)
            time.sleep(wait)
        sessions[tid] = login(base_url, username, password, tid, forwarded_proto, insecure)
        print(f"  terminal {i + 1}/{count} ready", flush=True)
    return terminals, sessions


def worker(base_url, terminal_id, cookie, forwarded_proto, insecure, stop_at,
           rng_seed, per_terminal_rate):
    """One simulated terminal.

    `per_terminal_rate` > 0 paces requests to that many per second (closed-loop
    with think-time), modelling a real POS terminal polling cadence and staying
    under the 240/min `terminal-read` limiter. `per_terminal_rate` == 0 is an
    unpaced closed loop (stress mode: will trip the limiter, kept for that
    measurement).
    """
    rng = random.Random(rng_seed)
    conn = _conn(base_url, insecure)
    base_headers = {
        "X-Forwarded-Proto": forwarded_proto,
        "X-Forwarded-For": "10.100.0.9",
        "Cookie": cookie,
        "Accept": "application/json",
    }
    samples = []  # (op, status, elapsed_ms)
    ops = list(SCENARIO_WEIGHTS)
    weights = [SCENARIO_WEIGHTS[o] for o in ops]
    interval = (1.0 / per_terminal_rate) if per_terminal_rate > 0 else 0.0
    while time.perf_counter() < stop_at:
        cycle_start = time.perf_counter()
        op = rng.choices(ops, weights=weights, k=1)[0]
        if op == CATALOG:
            path = f"/api/v1/terminals/{terminal_id}/catalog"
        else:
            path = f"/api/v1/terminals/{terminal_id}/orders/active"
        try:
            status, elapsed_ms, _, _ = _request(conn, "GET", path, base_headers)
            samples.append((op, status, elapsed_ms))
        except Exception:  # noqa: BLE001 - a dropped connection is a failed sample
            try:
                conn.close()
            except Exception:  # noqa: BLE001
                pass
            conn = _conn(base_url, insecure)
            samples.append((op, -1, 0.0))
        if interval:
            slack = interval - (time.perf_counter() - cycle_start)
            if slack > 0:
                time.sleep(slack)
    conn.close()
    return samples


def write_worker(base_url, terminal_id, cookie, forwarded_proto, insecure, stop_at,
                 per_terminal_rate, table_ids, product_id):
    """One simulated terminal running the order-write critical path:
    seat a table -> add 2 line items -> submit. Each cycle uses the next table
    from this terminal's disjoint slice. Samples are tagged
    ('seat' | 'item' | 'submit', status, elapsed_ms) so submit latency can be
    reported on its own against the V15-PER-001 target.
    """
    conn = _conn(base_url, insecure)
    headers = {
        "X-Forwarded-Proto": forwarded_proto,
        "X-Forwarded-For": "10.100.0.9",
        "Cookie": cookie,
        "Accept": "application/json",
        "Content-Type": "application/json",
    }
    samples = []
    interval = (1.0 / per_terminal_rate) if per_terminal_rate > 0 else 0.0
    base = f"/api/v1/terminals/{terminal_id}/orders"

    def call(method, path, body):
        try:
            status, ms, _, payload = _request(conn, method, path, headers,
                                              json.dumps(body) if body is not None else None)
            data = {}
            if payload:
                try:
                    data = json.loads(payload)
                except Exception:  # noqa: BLE001
                    data = {}
            return status, ms, data
        except Exception:  # noqa: BLE001
            return -1, 0.0, {}

    for table_id in table_ids:
        if time.perf_counter() >= stop_at:
            break
        cycle_start = time.perf_counter()

        status, ms, data = call("POST", f"{base}/table", {"tableId": table_id})
        samples.append(("seat", status, ms))
        if not (200 <= status < 300) or "orderId" not in data:
            _reconnect(conn, base_url, insecure)
            conn = _conn(base_url, insecure)
            continue
        order_id = data["orderId"]
        revision = data.get("revision", 1)

        aborted = False
        for _ in range(2):
            status, ms, data = call(
                "POST", f"{base}/{order_id}/items",
                {"productId": product_id, "quantity": 1, "expectedRevision": revision})
            samples.append(("item", status, ms))
            if not (200 <= status < 300):
                aborted = True
                break
            revision = data.get("revision", revision + 1)
        if aborted:
            continue

        op_id = str(uuid.uuid4())
        status, ms, _ = call("POST", f"{base}/{order_id}/submit",
                             {"operationId": op_id, "expectedRevision": revision})
        samples.append(("submit", status, ms))

        if interval:
            slack = interval - (time.perf_counter() - cycle_start)
            if slack > 0:
                time.sleep(slack)

    conn.close()
    return samples


def _reconnect(conn, base_url, insecure):
    try:
        conn.close()
    except Exception:  # noqa: BLE001
        pass


def pct(sorted_values, p):
    if not sorted_values:
        return 0.0
    k = (len(sorted_values) - 1) * (p / 100.0)
    lo = int(k)
    hi = min(lo + 1, len(sorted_values) - 1)
    frac = k - lo
    return sorted_values[lo] + (sorted_values[hi] - sorted_values[lo]) * frac


def _latency_block(samples):
    ok = [s for s in samples if 200 <= s[1] < 300]
    lat = sorted(s[2] for s in ok)
    return {
        "count": len(samples),
        "ok": len(ok),
        "p50": round(pct(lat, 50), 1),
        "p90": round(pct(lat, 90), 1),
        "p95": round(pct(lat, 95), 1),
        "p99": round(pct(lat, 99), 1),
        "max": round(lat[-1], 1) if lat else 0.0,
    }


def run_level(base_url, terminals, sessions, forwarded_proto, insecure, duration,
              per_terminal_rate, scenario="read", fixture=None):
    stop_at = time.perf_counter() + duration
    started = time.time()
    all_samples = []
    with ThreadPoolExecutor(max_workers=len(terminals)) as pool:
        if scenario == "write":
            table_ids = fixture["tableIds"]
            product_id = fixture["productId"]
            n = len(terminals)
            slices = [table_ids[i::n] for i in range(n)]
            futures = [
                pool.submit(write_worker, base_url, tid, sessions[tid], forwarded_proto,
                            insecure, stop_at, per_terminal_rate, slices[idx], product_id)
                for idx, tid in enumerate(terminals)
            ]
        else:
            futures = [
                pool.submit(worker, base_url, tid, sessions[tid], forwarded_proto,
                            insecure, stop_at, idx, per_terminal_rate)
                for idx, tid in enumerate(terminals)
            ]
        for fut in as_completed(futures):
            all_samples.extend(fut.result())
    wall = time.time() - started

    total = len(all_samples)
    ok = [s for s in all_samples if 200 <= s[1] < 300]
    status_breakdown = {}
    for _, status, _ in all_samples:
        key = "conn_error" if status < 0 else str(status)
        status_breakdown[key] = status_breakdown.get(key, 0) + 1

    result = {
        "terminals": len(terminals),
        "wall_seconds": round(wall, 2),
        "requests": total,
        "rps": round(total / wall, 1) if wall else 0.0,
        "ok": len(ok),
        "error_pct": round(100.0 * (total - len(ok)) / total, 2) if total else 0.0,
        "status_breakdown": status_breakdown,
        "latency_ms": _latency_block([(o, s, m) for (o, s, m) in all_samples]),
    }
    if scenario == "write":
        result["submit_latency_ms"] = _latency_block(
            [(o, s, m) for (o, s, m) in all_samples if o == "submit"])
        result["seat_latency_ms"] = _latency_block(
            [(o, s, m) for (o, s, m) in all_samples if o == "seat"])
    return result


def main(argv=None):
    ap = argparse.ArgumentParser(description="ALKAROS V1 load baseline harness")
    ap.add_argument("--base-url", required=True)
    ap.add_argument("--login", required=True, metavar="user:password")
    ap.add_argument("--terminals", default="1,5,10,20",
                    help="comma-separated concurrency levels (max is bootstrapped)")
    ap.add_argument("--duration", type=float, default=20.0, help="seconds per level")
    ap.add_argument("--rate", type=float, default=1.0,
                    help="requests/sec per terminal (0 = unpaced stress loop)")
    ap.add_argument("--forwarded-proto", default="https")
    ap.add_argument("--insecure", action="store_true", help="skip TLS verification")
    ap.add_argument("--scenario", default="read", choices=["read", "write"])
    ap.add_argument("--fixture", default=None,
                    help="write scenario: JSON file { productId, tableIds } from seed-loadtest.sh")
    ap.add_argument("--json", dest="json_path", default=None)
    args = ap.parse_args(argv)

    fixture = None
    if args.scenario == "write":
        if not args.fixture:
            ap.error("--scenario write requires --fixture")
        with open(args.fixture, "r", encoding="utf-8") as fh:
            fixture = json.load(fh)
        if not fixture.get("tableIds") or not fixture.get("productId"):
            ap.error("fixture must contain productId and a non-empty tableIds list")

    username, _, password = args.login.partition(":")
    levels = sorted({int(x) for x in args.terminals.split(",") if x.strip()})
    max_terminals = max(levels)

    print(f"bootstrapping {max_terminals} terminal session(s) against {args.base_url}", flush=True)
    terminals, sessions = bootstrap_terminals(
        args.base_url, username, password, max_terminals,
        args.forwarded_proto, args.insecure)

    metric = "submit_latency_ms" if args.scenario == "write" else "latency_ms"
    results = []
    for level in levels:
        print(f"--- level: {level} terminal(s), {args.duration:.0f}s ---", flush=True)
        res = run_level(args.base_url, terminals[:level], sessions,
                        args.forwarded_proto, args.insecure, args.duration, args.rate,
                        scenario=args.scenario, fixture=fixture)
        results.append(res)
        lat = res[metric]
        label = "submit" if args.scenario == "write" else "all"
        print(f"  rps={res['rps']} err%={res['error_pct']} {label} "
              f"p50={lat['p50']} p90={lat['p90']} p95={lat['p95']} p99={lat['p99']} "
              f"max={lat['max']} n={lat['count']} statuses={res['status_breakdown']}", flush=True)

    report = {
        "base_url": args.base_url,
        "scenario": ("write (seat -> 2 items -> submit)" if args.scenario == "write"
                     else "read (0.75 catalog GET / 0.25 active GET)"),
        "per_terminal_rate": args.rate,
        "target": "V15-PER-001: 20 terminals p95<500ms p99<1000ms (order submit)",
        "generated_utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "levels": results,
    }

    print()
    print(f"{'terminals':>9} {'rps':>7} {'err%':>6} {'p50':>7} {'p90':>7} {'p95':>7} {'p99':>7} {'max':>8}")
    for r in results:
        lat = r[metric]
        print(f"{r['terminals']:>9} {r['rps']:>7} {r['error_pct']:>6} "
              f"{lat['p50']:>7} {lat['p90']:>7} {lat['p95']:>7} {lat['p99']:>7} {lat['max']:>8}")

    top = results[-1]
    tl = top[metric]
    verdict = tl["p95"] < 500.0 and tl["p99"] < 1000.0 and top["error_pct"] == 0.0
    print()
    print(f"V15-PER-001 target at {top['terminals']} terminals ({metric}): "
          f"{'MEETS' if verdict else 'DOES NOT MEET'} "
          f"(p95={tl['p95']}ms p99={tl['p99']}ms err%={top['error_pct']})")

    if args.json_path:
        with open(args.json_path, "w", encoding="utf-8") as fh:
            json.dump(report, fh, indent=2)
        print(f"wrote {args.json_path}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
