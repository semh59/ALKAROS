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
  *(Superseded by the "Decision update (2026-11-07)" at the end of this file.)*

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
*(Superseded by the "Decision update (2026-11-07)" at the end of this file: the webhook route is now required.)*

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
  *(Superseded by the "Decision update (2026-11-07)" at the end of this file: the webhook is chosen.)*

## Invariants for consumers

- `V0-HUG-001`/future `V13-HUG-*`/`V13-PAY-*` tasks target TokenX Connect
  Cloud (REST) as the primary integration surface for the kasa flow.
- No V1.3 task builds the wired (IntegrationHub.dll) bridge or the Android
  in-app client without a separate, explicit decision reopening this one.
- Default result-retrieval strategy is polling; a webhook receiver is not
  assumed to exist unless a later task explicitly adds the Cloudflare
  Tunnel route for it.
  **Superseded by the "Decision update (2026-11-07)" at the end of this
  file: the webhook is the result channel and the Tunnel route is required.**

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

## Research note (2026-10-17, V1-RMD-881)

This note does not change the approved decision (2026-09-17); the text above reflects what was known that day. Source: the 2026-10-17 external-dependency research of the Token developer portal (`developer.tokeninc.com`), recorded in `plan/v0/hugin-t300/V0-HUG-001-integration-contract.md` (V1-RMD-584 note). Finding F0089.

- The portal's HoReCa page (`x-platform/baslangic/horeca`) states "Polling is not done, webhook is mandatory" (events BASKET_COMPLETED/LOCKED/UNLOCKED); whether the page means this only for Cloud was not verified (webhook is meaningless for the wired mode). This conflicts with "Result retrieval defaults to polling". Whether `Get Basket Details`/`Get Open Baskets` polling is supported at all was not verified.
- The portal's Cloud introduction mentions only the X30 TR; the 300 TR is wired-only. "300 TR / X30 TR" above is therefore not confirmed for Cloud.
- If webhook is mandatory, the "no new infrastructure" argument no longer holds: an inbound route would be needed, which would need a new Cloudflare Tunnel route (`docs/architecture/qr-relay-topology.md`).
- Process facts from the portal: contract first, then test device, test queue about 2 months (V0-HUG-001 note).
- Open decision (product owner): keep Cloud with webhook and a new tunnel route, or reopen the wired/in-app options. Nothing in the decision or the invariants was changed here.

## Decision update (2026-11-07, Semih)

The product owner closed the open decision of the research note above: the Token path is **TokenX Connect Cloud with the webhook and a new Cloudflare Tunnel route**. The wired and in-app options stay rejected for the kasa scope.

- "Result retrieval defaults to polling" in the selected model no longer holds. The webhook (events `BASKET_COMPLETED`, `BASKET_LOCKED`, `BASKET_UNLOCKED`) is the result channel. Whether polling is also supported stays unverified and is not relied on.
- "No new infrastructure" no longer holds. Token's cloud calls ALKAROS from outside, so an inbound route is required: a new Cloudflare Tunnel route to the Host, in the same way as the QR relay (`docs/architecture/qr-relay-topology.md`). Inbound ports on the local network stay closed.
- Scope narrows to the X30 TR, the only model the portal presents for Cloud. The 300 TR is wired-only and is not covered by this path.
- No code is written by this update. The webhook receiver (inbound authentication, idempotent inbox, replay handling) and the tunnel route are separate future tasks and must be planned before `V13-HUG-001` is coded. The contract and test-device process (contract first, test queue about two months) is unchanged.
- Still unverified and to be settled with Token during the contract step: whether webhook is mandatory only for Cloud, how a webhook is authenticated, and the production and test addresses.

Requirements the future webhook-receiver task has to meet (recorded here so they are not forgotten; nothing is built yet):

- The tunnel does not buffer: while the Host or the tunnel is down a webhook delivery can be lost (`docs/architecture/qr-relay-topology.md` §2b). The contract step must establish whether Token retries a failed delivery. If it does not, the kasa needs a visible "result pending" state and an operator alert for a basket whose result never arrived, instead of silently losing a paid card sale.
- Authenticate every request (the method is to be confirmed with Token), compare signatures in constant time, reject stale timestamps, limit body size and request rate, and answer quickly with a 2xx once the event is durably stored.
- Store events in an idempotent inbox (`docs/architecture/idempotency-inbox-outbox.md`); treat `BASKET_COMPLETED`, `BASKET_LOCKED` and `BASKET_UNLOCKED` as order-independent; log and safely ignore unknown event types; keep poisoned messages apart from the main queue.
- Never log secrets or card data; keep sandbox and production credentials and addresses separate.
- The new route exposes the webhook path only. The tunnel isolation allow-list (`V1-RMD-688`) must stay in force so the route cannot reach login, management or kasa endpoints.
