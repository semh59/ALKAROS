# V0-QRG-001 - Relay threat/feasibility evidence (2026-09-07)

> Domain and account identity are deliberately redacted (`<test-domain>`)
> per Semih's instruction: the specific domain used for this test belongs
> to ALKAROS's first pilot customer and must not be recorded anywhere in
> the repository. The technical facts below (protocol, status codes,
> timestamps, process/port state) are real and unredacted.

## Setup

- Provider: Cloudflare Tunnel (`cloudflared` 2026.8.3, Windows amd64).
- Domain: a real, registered domain on Cloudflare DNS (nameservers verified
  live: `koa.ns.cloudflare.com` / `reza.ns.cloudflare.com`), referred to
  below as `<test-domain>`.
- Tunnel: named tunnel `alkaros-relay-test`, created via `cloudflared
  tunnel create`; credentials JSON written to the operator's local
  `~/.cloudflared/` (never committed, never leaves the machine).
- DNS route: `relay.<test-domain>` → CNAME to the tunnel, created via
  `cloudflared tunnel route dns`.
- Origin: a local-only probe HTTP server bound to `127.0.0.1:8787`
  (loopback only), returning a fresh UUID nonce + timestamp per request so
  a response can be proven live, not cached.
- `cloudflared tunnel run` connects **outbound only** (4 redundant QUIC
  connections to Cloudflare's edge, `fra18`/`fra08`/`fra19`/`fra03`) — no
  listener was opened for public traffic; `netstat -ano` before and after
  confirms no new `0.0.0.0`/public-interface `LISTEN` socket appeared,
  only the pre-existing loopback ports (the probe on 8787, cloudflared's
  own metrics endpoint on 127.0.0.1:20242).

## 1. Live end-to-end request (no public inbound port)

5 consecutive `curl https://relay.<test-domain>/` calls from this machine,
each over the public internet through Cloudflare's edge and back through
the outbound tunnel to the local probe:

```
HTTP 200
HTTP 200
HTTP 200
HTTP 200
HTTP 200
{"ok": true, "server": "alkaros-relay-probe", "nonce": "abbee708-f323-4368-a788-c705bae29987", "served_at": 1788795821.0684898}
```

Each call returns a distinct nonce (confirmed), proving the response is
generated live by the local origin on every request — not a cached edge
response — while the connecting machine never opened an inbound port.

## 2. Outage behavior (real failure captured)

`taskkill /F /IM cloudflared.exe` (kills every connector process, no
graceful shutdown) → immediate re-probe:

```
HTTP 502
```

Cloudflare's edge reports a clean `502 Bad Gateway` when the outbound
connector is down. It does not expose anything about the local network,
time out indefinitely, or leak an internal error — it fails closed at the
edge.

## 3. Recovery (reconnect)

`cloudflared tunnel run alkaros-relay-test` restarted (fresh process,
same credentials, no config change) → 6s later:

```
HTTP 200
```

Traffic resumed automatically once the connector re-registered; no DNS or
tunnel reconfiguration was needed on the public side — only the local
connector had to come back.

## 4. Revocation (real, not simulated)

`cloudflared tunnel delete -f alkaros-relay-test` (after stopping the
connector) → `cloudflared tunnel list` confirms zero tunnels remain → re-probe:

```
HTTP 530
```

`530` is Cloudflare's own "origin/tunnel not found" response — deleting
the tunnel's credentials at the source immediately and permanently cuts
public access; no stale route or cached authorization remained reachable.

## Acceptance evidence (per V0-QRG-001)

- **"Local network'e public inbound port açmadan çalışan proof mevcut"**:
  satisfied — §1 above, cross-checked against `netstat` showing no new
  inbound listener.
- **Success and at least one real failure/edge-case output**: satisfied —
  §1 (success), §2 (a genuine 502 from killing the real process, not a
  simulated error), §4 (a genuine 530 from real credential deletion).
- **Outbound connector**: satisfied (§1, §3).
- **Revocation**: satisfied (§4) — deleting the tunnel is the credential
  revocation mechanism; it is immediate and verified, not assumed.
- **Authentication**: the tunnel's own registration is inherently
  credential-gated (the `cloudflared tunnel login` OAuth flow issues
  `cert.pem`; `tunnel create` issues a per-tunnel credentials JSON — no
  connector can register without both). Verified functionally: this
  session's connector could only start after `cert.pem` existed.
- **Token rotation, replay, rate limit**: explicitly out of this task's
  acceptance bar (that is `V14-QRS-002`'s scope, layered on top of this
  transport once it exists) — not tested here, not claimed as covered.
- **Threat model**: no critical risk found in the topology itself under
  this test; the residual risks already on record
  (`docs/architecture/qr-relay-topology.md`, `docs/design/modules/qr-nfc-ordering.md`)
  are about the application layer (QR photo-sharing, physical sticker
  tampering), not this transport.

## Not verified today (recorded, not assumed)

- Multi-day/production-scale outage queue behavior (7-day retention per
  `docs/architecture/qr-relay-topology.md`) — this session's outage window
  was seconds, not days. A short outage recovering cleanly is evidence the
  *mechanism* works; the 7-day retention figure itself remains a policy
  target, not something this test exercised.
- mTLS client-certificate authentication at the connector→queue hop
  specifically (`docs/architecture/qr-relay-topology.md`'s trust-boundary
  table) — Cloudflare Tunnel's own connector registration (cert.pem +
  per-tunnel credentials) is the mechanism actually used here, which
  satisfies the same intent (no connector without possession of a
  private credential) but was not decomposed hop-by-hop against that
  table's exact wording.
