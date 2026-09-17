# Token Integration Path Decision — approved decision record

> **Task:** V13-GOV-005
> **Status:** Done
> **Work type:** decision
> **Source basis:** PO:2026-09-17, CORR:C98
> **Access date:** 2026-09-17
> **Approver:** Semih — 2026-09-17
> **Decision type:** Business decision (named approver, live doc research)

## Selected model

For V1.3's cashier (kasa) payment flow, ALKAROS integrates with the Token
terminal via **TokenX Connect Cloud (REST)**:

- `ALKAROS.Host` authenticates with `client-id`/`client-secret` → Bearer
  token, then calls `Add Basket`/`Instant Basket` over plain outbound
  HTTPS to Token's cloud. The physical terminal receives the request
  through Token's own cloud sync — no direct network path between
  `ALKAROS.Host` and the terminal is required.
- Result retrieval defaults to **polling** (`Get Basket Details`/`Get Open
  Baskets`) rather than the webhook callback Token's API also offers.

## Why (three real options compared)

`developer.tokeninc.com` documents three distinct integration surfaces for
Token/Beko devices (300 TR / X30 TR), each verified directly against the
portal's own pages:

1. **IntegrationHub.dll (wired/USB)** — a Windows-only C#/.NET (or C++)
   DLL; the portal's own Quick Start requires Visual Studio, the Visual
   C++ Redistributable, and a mandatory reboot. `ALKAROS.Host` runs in a
   Linux (Debian) Docker container (`deploy/docker/Dockerfile`); this DLL
   cannot be loaded by that process regardless of the underlying
   physical machine's OS. Using it would require an entirely new native
   Windows bridge service — real, additional infrastructure with no
   current task or justified need.
2. **TokenX Connect Cloud (REST)** — a plain HTTPS API; works from any
   backend including a Linux container. **Selected.**
3. **In-App Integration (Android)** — a custom Android app installed
   directly on the terminal's own "Satış Uygulamaları" menu, using
   Android Intents to hand off a basket to Token's certified payment app
   and receive the result via `onActivityResult`. Technically confirmed
   possible (X30 TR is a genuine Android device, 432g, WiFi/Ethernet
   capable — market material explicitly advertises it for tableside/
   doorstep payment). This is the right path **only** if the terminal
   itself must run ALKAROS's own ordering UI standalone (e.g. a waiter
   carrying it table-to-table) — not needed for a fixed cashier-counter
   flow, since the existing `PosTerminal` web client already drives the
   sale; the terminal only needs to receive a payment amount and return a
   result.

Given today's scope is the fixed kasa counter (not a carried device),
option 2 delivers everything needed with no new infrastructure and no new
native client to build and maintain.

### Network topology note

`docs/architecture/qr-relay-topology.md` locks a "no public inbound ports
on local network" rule; the only existing exception (Cloudflare Tunnel for
the QR relay) is deliberately outbound-initiated. Token's cloud webhook
(`BASKET_COMPLETED`) is an inbound push and would need the same kind of
new tunnel route to receive safely. Polling avoids this entirely — matches
`dual-screen-pos-topology.md`'s own established pattern of periodic HTTP
reconciliation as the source of truth rather than trusting a push
notification alone. This is a default, not a permanent lock: a future
task may add the webhook path alongside polling if latency requires it.

## Rejected / deferred alternatives

- **Wired (IntegrationHub.dll) with a new native Windows bridge** —
  rejected for now: real additional infrastructure cost with no offsetting
  benefit over the cloud path for a fixed-counter flow.
- **In-App Integration (Android, tableside)** — deferred, not rejected.
  Semih's own words: "Belki sonra" (maybe later). Requires a new native
  Android client (or a WebView wrapper around WaiterPwa) satisfying
  Token's own `AndroidManifest.xml`/AIDL/Intent conventions — real,
  distinct engineering work, not something to design speculatively ahead
  of an actual decision to pursue it.
- **Webhook-based result delivery** — not rejected, deferred as the
  non-default: polling is simpler and needs no new network exposure;
  webhook remains available if a future task justifies the added
  complexity.

## Invariants for consumers

- `V0-HUG-001`/future `V13-HUG-*`/`V13-PAY-*` tasks target TokenX Connect
  Cloud (REST) as the primary integration surface for the kasa flow.
- No V1.3 task builds the wired (IntegrationHub.dll) bridge or the Android
  in-app client without a separate, explicit decision reopening this one.
- Default result-retrieval strategy is polling; a webhook receiver is not
  assumed to exist unless a later task explicitly adds the Cloudflare
  Tunnel route for it.

## Affected tasks

- Depends on: `V0-GOV-064` (Hugin → Token/Beko device decision).
- Consumers: `V0-HUG-001` (integration contract validation should target
  TokenX Connect Cloud specifically), future `V13-HUG-*`/`V13-PAY-*`
  implementation tasks.

## Acceptance evidence

- Decision record grounded in live `developer.tokeninc.com` research
  (IntegrationHub Quick Start, TokenX Connect Cloud developer doc, In-App
  Integration Quick Start) cross-checked against ALKAROS's own locked
  architecture docs (`deploy/docker/Dockerfile`, `qr-relay-topology.md`,
  `dual-screen-pos-topology.md`).
- Approver's own words recorded above for the scope boundary (kasa now,
  tableside/Android deferred).
