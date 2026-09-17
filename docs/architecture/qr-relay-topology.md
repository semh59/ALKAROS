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

## 4. Affected Tasks

- V0-QRG-001, V1-FND-001, V12-QRT-001
