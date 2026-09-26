# QR Relay Production Topology

> **Task:** V0-ARC-009
> **Status:** Done
> **Assignee:** codex-v0-arc-009
> **Work type:** decision
> **Source basis:** PDF:I.6.5, PDF:I.34-I.35, PDF:II.7.3, CORR:C22
> **Date:** 2026-07-30
> **Approver:** Semih — 2026-08-03
> **Decision type:** Business decision (named business approver)

## 1. Topology

```text
Customer Phone → Public QR Relay (HTTPS) → Durable Queue → Local Outbound Connector → POS Backend
```

- Public relay: Managed cloud service (self-hosted option for enterprise).
- Local connector: Outbound-only connection from POS to relay (no inbound ports opened on LAN).
- Queue: Durable, 7-day retention, at-least-once delivery.
- TLS: End-to-end, relay terminates public TLS, connector uses mTLS for authentication.

## 2. Trust Boundaries

| Hop | Protocol | Auth | Owner |
| ----- | ---------- | ------ | ------ |
| Customer → Relay | HTTPS | QR token (signed, time-limited) | Relay provider |
| Relay → Queue | Internal | Service account | Relay provider |
| Queue → Connector | mTLS outbound | Client certificate | POS (local) |
| Connector → POS | Internal Docker network (compose service DNS) | API key | POS |

## 2a. Amendment (V12-QRT-005, 2026-09-17)

The "Connector → POS" hop's mechanism changed from OS loopback ("Internal
localhost") to the compose bridge network's service DNS
(`http://api:5080`), because the connector was split out of the `api`
container into its own container for fault isolation (a public-relay-side
incident must never share a process with the staff-facing POS). The first
attempt kept "localhost" literally true by sharing `api`'s network
namespace (`network_mode: "service:api"`), but that coupling meant
restarting/redeploying `api` also forced the connector to restart — the
exact blast-radius problem this split exists to fix, just in the other
direction. Moving to compose DNS instead removes that coupling entirely
(verified with real `docker compose restart api`/`restart connector`: each
now leaves the other running undisturbed).

This is a mechanism change, not a trust-boundary change: still no public
inbound port on the LAN, still the same physical host, still API-key
authenticated, still owned entirely by POS. Rule 1 below is unaffected.

## 3. Rules

1. No public inbound ports on local network.
2. QR tokens expire after 4 hours.
3. Queue retention: 7 days max.
4. Outage: connector retries with backoff; queue buffers during outage.

## 2b. Amendment (V1-RMD-327, 2026-09-26) — the Durable Queue was never built, and there is no LAN fallback

Found by an independent 13-agent audit (2026-09-26), finding K16, verified by reading the real
implementation (`src/Integrations/QrRelay/PublicGateway/CloudflareApiClient.cs`,
`RelayProvisioningService.cs`): V12-QRT-001/V12-QRT-005 shipped a direct Cloudflare Tunnel reverse-proxy
ingress (`cloudflared`, `cfd_tunnel` API, a hostname → `http://api:5080` ingress rule) — **not** the
"Durable Queue" this document's own §1 topology diagram and §3 rule 3/4 describe. Cloudflare Tunnel is a
TCP/HTTP reverse proxy, not a message broker; it has no 7-day retention, no at-least-once delivery, and no
buffering of any kind. Rule 4's "queue buffers during outage" is not true of what was actually built: if
the tunnel (or Cloudflare's own edge) is down, a customer's QR order request simply fails right then —
`cloudflared`'s own retry/backoff (§3 rule 4's "connector retries with backoff" half) reconnects the
*tunnel*, but nothing replays a request a customer made while it was down. There is also no LAN/local
fallback path: `RelayProvisioningService`'s only configured origin is reached exclusively through the
public Cloudflare hostname (`LocalOriginService = "http://api:5080"` is the tunnel's INTERNAL target, not
something a customer's phone can reach directly) — a customer sitting in the restaurant on the same LAN
has no alternate URL to fall back to if the public relay is unreachable, even though their phone and the
POS are on the same physical network.

**This gap is a business/architecture decision, not something this task's own Owned surface can silently
resolve.** Two honest options, neither implemented here:

- Accept the simpler reverse-proxy architecture V12-QRT-001 already shipped as sufficient, and formally
  amend §1/§3 to drop the queue/buffering claims (an outage means zero QR orders until the tunnel is back,
  full stop) — the lowest-cost option, already what is running in production today.
- Commission the originally-decided durable queue (and/or a LAN-fallback QR path) as new, separately-scoped
  work — a genuinely large build (a message broker or durable outbox between the public edge and the local
  connector, plus either a dual-URL QR code or local-network service discovery for the fallback path).

Until Semih decides between these, this amendment records the gap so a future reader of §1/§3 does not
mistake the original decision for what is actually running.

## 4. Affected Tasks

- V0-QRG-001, V1-FND-001, V12-QRT-001
