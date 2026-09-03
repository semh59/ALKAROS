# ALKAROS V1 Desktop POS and Container Acceptance

> **Superseded in part (2026-09-03, `V1-RMD-098`).** The single-container `host` +
> `proxy` layout described here was replaced by the A1 frontend/backend split:
> `web` (Caddy: static bundles + TLS + reverse proxy) and `api`
> (`ALKAROS.Host serve --api-only`, plain HTTP). The `Dockerfile` moved to
> `deploy/docker/Dockerfile`; ops services moved to `compose.ops.yaml`. See
> `deploy/docker/README.md` and `evidence/V1-RMD-098/`.

- Task: `V1-RMD-032`
- Review date: 2026-08-28
- Reviewer: `/root` (independent validation pass)
- Base commit: `a03d02146961c29a8b847a7b0c472c6c8dd42c9f`
- Base tree: `39e9bb79d3d6f3e099e15a7ffafcc801f727843a`
- Tested surface: the current uncommitted remediation worktree built by the root `Dockerfile` and `compose.yaml`
- Final verdict: **NOT PRODUCTION READY**

## Executive decision

The repository now provides a real, local HTTPS container stack with PostgreSQL persistence, first-run manager
provisioning, a functional cashier order path, customer-display pairing, live order presentation and bounded revoke.
That is meaningful progress, but it is not a V1 restaurant release.

The acceptance gate is rejected because a submitted order is persisted to `kitchen-main` while the production UI is
hard-coded to query `hot-line`; the live kitchen screen therefore reports zero tickets. Bill splitting exists as an
isolated component but is not connected to any production route. A fresh installation exposes no usable floor-plan
creation path, merge/unmerge cannot be completed in the exercised valid-looking table state, and reservation capture
does not preserve the party size or a bounded reservation time. Catalog controls are clipped at narrow widths and at
the 400% reflow equivalent. Mandatory external release evidence is also absent.

Designer acceptance is also rejected. The visual language is internally more consistent than the earlier prototype,
touch targets and the customer display are credible, but the cashier/management surfaces still read as a narrow,
card-stacked mobile administration UI placed inside a wide desktop shell. Large desktop canvases are not converted
into spatial awareness, high-speed scanning or restaurant-operation density. The visual work does not meet the
competitive quality bar requested for ALKAROS V1.

## What passed

| Area | Result | Evidence |
| --- | --- | --- |
| Container topology | PASS | PostgreSQL 18, migration, provisioning, Host and Caddy services exist in one Compose project; long-running services recovered healthy after restart. |
| Locked UI build | PASS | Digest-pinned Node 24.6 build stage, pnpm 11.19.0 frozen install and production build exited 0. |
| Frontend tests | PASS | 13 files and 77 tests passed; component-level axe checks reported no critical/serious finding. |
| First-run login | PASS | Provisioned manager logged into the real Host; no mock transport was used. |
| Cashier order | PASS | Product `BURGER-01` was ordered and submitted as `POS-20260828-110553-7B6C9E7483E5`. |
| Customer display | PASS | A separate browser tab produced its own pairing code, paired, displayed the submitted order and total, then returned to pairing after revoke. |
| Pairing recovery | PASS | Invalid code produced an actionable error in 588 ms; retry remained available. |
| Stale financial clearing | PASS | Revoke removed the paired order/total and returned to pairing in 1,864 ms, below the 10,000 ms limit. |
| Restart persistence | PASS | HTTPS readiness recovered; the database still held 1 zone, 3 tables, 1 product, 1 order and 1 kitchen ticket. |
| Console | PASS | The exercised route snapshots produced zero browser warning/error entries. |
| Base viewport containment | PARTIAL PASS | No document-level horizontal overflow was measured in the base matrix and measured interactive targets were at least 44×44 px; some controls are nevertheless clipped by overflow containment. |
| Modal semantics | PARTIAL PASS | Accessible dialog name, `aria-modal`, Escape close and focus restoration passed; a complete live Tab-order transcript was not reproduced. |

## Confirmed findings

### RMD032-F001 — P0 blocker — Production kitchen screen queries the wrong station

- Reachable chain: submit cashier order → Host creates a ticket using `ALKAROS_KITCHEN_STATION_ID=kitchen-main` →
  `/kitchen` creates its client with hard-coded `hot-line` → active-ticket query returns an empty list.
- Code evidence: `src/Clients/PosTerminal/src/App.tsx:775` hard-codes `hot-line`; Compose defaults the Host to
  `kitchen-main`; `src/Host/DualScreen/DualScreenApplication.cs:33` owns the environment contract.
- Runtime evidence: PostgreSQL contains ticket `150f10ec-6064-4022-ba42-ec96abf32741` in `Queued` state at
  `kitchen-main`, while the screenshot and DOM identify `hot-line istasyonu` and show zero active tickets.
- Impact: a successfully submitted order is invisible to the production kitchen workflow.
- Required remediation: remove the UI constant, expose one authoritative station assignment/configuration contract,
  and prove submit → visible kitchen ticket → preparing → ready in the real container.

### RMD032-F002 — P1 high — Operational bill splitting is not connected to the product

- `BillSplitWorkspace`, models, API client and unit tests exist under `src/Clients/PosTerminal/src/features/billing/`.
- The production route composition at `src/Clients/PosTerminal/src/App.tsx:656-658` mounts only tables, catalog and
  kitchen; there is no bill-split import, route or control. PostgreSQL contained zero bills after the exercised order.
- Impact: the promised V1 flow for seat ownership, split by item/amount and payment preparation cannot be reached.
- Required remediation: connect the component to the active table/order/bill context and test save, conflict, clear,
  reload and restart behavior against the Host.

### RMD032-F003 — P1 high — Fresh-system floor-plan and chair setup is unreachable

- The source contains `FloorPlanWorkspace`, but `TableWorkspace` renders it only when a floor plan already exists.
- A fresh database held `floor_plan_count=0`; the real UI showed only the list/cards and no discoverable create-plan or
  chair-layout bootstrap action.
- Impact: restaurant staff cannot build the spatial table/chair model promised by the V1 scope.
- Required remediation: provide an explicit empty-state creation path, persist the initial plan, add/move chairs and
  tables, and prove reload/restart retention.

### RMD032-F004 — P1 high — Table merge/unmerge cannot be completed in the exercised workflow

- Transfer A-01 → A-02 succeeded. Merge attempts with A-02/A-03 produced only
  `Bağlı kayıtlar nedeniyle işlem tamamlanamadı.` even after A-03 was placed in an occupied state.
- PostgreSQL remained at `merge_count=0`; therefore no unmerge path could be exercised.
- Impact: a core restaurant correction workflow is unavailable and the error does not explain how the operator can
  recover.
- Required remediation: align allowed commands and preconditions across UI/API/domain, return a specific recovery
  message, and prove merge → reload → unmerge with order/bill ownership preserved.

### RMD032-F005 — P1 high — Reservation UI loses operational meaning

- The UI accepted only a free-text reason. The operator entered `Saat 19:30 · 4 kişi`.
- PostgreSQL persisted `party_size=1`, `expires_at=NULL` and the text only in `reason`.
- Impact: capacity, arrival time and expiry cannot be trusted or queried, causing inaccurate floor state.
- Required remediation: expose guest/party size, reservation time and expiry fields; validate capacity/time; persist
  typed values and verify cancellation/restart behavior.

### RMD032-F006 — P1 high — Catalog controls become unreachable at narrow widths and zoom reflow

- At 768 px the price tab is outside the viewport. At 430/431 px modifier and price tabs are outside it. At 429 px
  and below tax, modifier and price tabs are outside it.
- The document reports no horizontal overflow because overflow is clipped, so the missing controls cannot be reached
  through page scrolling.
- The 400% CSS-pixel reflow equivalent reproduces the same loss. Native browser zoom could not be forced by the
  automation backend, so native 200/400% acceptance remains unproven.
- Required remediation: use a wrapping or explicitly scrollable tab pattern with visible affordance and selected-tab
  reveal; re-run native 200/400% browser zoom.

### RMD032-F007 — P2 medium — Desktop composition fails the designer quality bar

- The production shell technically fills the viewport (`#root` 1425 px and table route 1161 px at the nominal
  1440×900 run), but operational content remains concentrated in small cards and a narrow top/left cluster.
- The table surface does not provide an actual spatial floor, chair geometry or at-a-glance service load. The kitchen
  page leaves almost all desktop area unused while critical status appears in small, low-density cards. The catalog
  uses stacked administration panels and a fixed bottom navigation at narrow widths, prioritizing shell chrome over
  the editing task.
- Impact: poor scan speed, weak spatial comprehension and unnecessary interaction depth for a high-frequency POS.
- Required remediation: redesign from the desktop restaurant workflow outward: persistent spatial floor canvas,
  adjacent actionable inspector, dense yet readable operational queues, stronger primary/secondary action hierarchy
  and responsive transformations that preserve every control.

### RMD032-F008 — P2 medium — Some accessibility acceptance claims are not live-proven

- Component-level axe tests passed, but a live axe engine could not be injected into the controlled production tab.
- Dialog Escape and focus restoration passed; the automation backend did not produce a reliable complete Tab-order
  transcript. Reduced-motion CSS exists, but OS-level reduced-motion emulation was not reproduced.
- Impact: the release cannot claim complete WCAG 2.2 AA/browser evidence from this run.
- Required remediation: add an approved live-browser accessibility harness that covers every production state,
  native zoom, reduced motion and keyboard completion without relying only on jsdom component tests.

### RMD032-F009 — P0 release blocker — Mandatory external go-live evidence is absent

- No approved public/enterprise HTTPS certificate evidence, fiscal/payment/provider sandbox or physical device proof,
  backup restore/RPO-RTO exercise, license evidence, security assessment or signed go-live approval is present.
- This is not waivable by repository code. `V1-RMD-031` correctly remains `Blocked`.

## Viewport and state coverage

The cashier, tables, catalog, kitchen and customer display were captured at 1920×1080, 1440×900, 1366×768,
1280×800, 1024×768, 768×1024, 430×932, 390×844 and 320×568. CSS breakpoints were measured at ±1 px for
319/320, 359/360, 389/390, 429/430, 767/768, 899/900, 1023/1024, 1249/1250 and 1279/1280 where applicable.

Captured real states include fresh/empty tables, populated tables, reservation, occupied tables, successful transfer,
failed merge, catalog product/price, submitted cashier order, empty kitchen screen despite a queued database ticket,
customer pairing, paired/submitted customer order, invalid pairing error, revoke and post-revoke pairing. Loading,
offline, unauthorized, conflict, paying and completed variants are covered only by implementation/unit-test artifacts
where not explicitly listed as a live state; they are not promoted to live acceptance evidence.

## Evidence index

- Screenshots: `evidence/V1-RMD-032/browser/`
- Bounding boxes and viewport measurements: `evidence/V1-RMD-032/metrics/`
- DOM/accessibility snapshots and console records: `evidence/V1-RMD-032/transcripts/`
- Command and database verification: `evidence/V1-RMD-032/verification.md`
- Structured findings: `evidence/V1-RMD-032/findings.json`

## Release decision

`V1-RMD-032` validation is complete, but the product is **NOT PRODUCTION READY**. The next repository work must be a
new, single-owner remediation task with an exact owned surface. No release-ready or production-ready label may be
applied until every P0/P1 repository finding is retested and closed, the live accessibility evidence is complete, and
the external blockers in `V1-RMD-031` have real signed evidence.
